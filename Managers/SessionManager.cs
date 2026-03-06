using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rere.Models;

namespace Rere.Managers
{
    // ─── SessionManager (Thread-safe Singleton) ──────────────────────────────────
    public class SessionManager
    {
        public static readonly SessionManager Shared = new SessionManager();

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, WireGuardSession> _sessions = new();
        private readonly List<ActivityLog> _recentLogs = new();
        private readonly Dictionary<string, GeoInfo> _geoCache = new();
        private readonly HashSet<string> _pendingIPs = new();
        private readonly HashSet<string> _bannedIPs = new();
        private readonly Dictionary<string, DateTime> _lastExitTime = new();
        private readonly Dictionary<string, int> _reentryCount = new();

        private double _timeout = 7.0;
        private double _ghostTimer = 15.0;
        private int _maxReentry = 10;
        private string _potentialHost = "Analyzing...";

        private CancellationTokenSource? _exitCheckCts;
        private DateTime? _startTime;

        // Callbacks — fired on UI thread
        public Action<List<WireGuardSession>, List<ActivityLog>, string>? OnUpdate;

        private SessionManager() { }

        // ─── Initialize ──────────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            LocalDatabaseManager.Shared.LoadAllDatabases();

            _startTime = DateTime.Now;
            StartExitChecker();

            await Task.CompletedTask;
        }

        // ─── Packet Handling ─────────────────────────────────────────────────────

        public async Task HandlePacketAsync(string ip, string port, DateTime timestamp)
        {
            await _lock.WaitAsync();
            try
            {
                if (_bannedIPs.Contains(ip)) return;

                if (_lastExitTime.TryGetValue(ip, out var lastExit))
                {
                    if ((timestamp - lastExit).TotalSeconds < _ghostTimer) return;
                }

                // Update existing session
                if (_sessions.ContainsKey(ip))
                {
                    _sessions[ip].LastSeen = DateTime.Now;
                    if (port != "0" && port != _sessions[ip].Port)
                    {
                        _sessions[ip].Port = port;
                        EmitUpdate();
                    }
                    return;
                }

                if (_pendingIPs.Contains(ip)) return;
                _pendingIPs.Add(ip);
            }
            finally { _lock.Release(); }

            // Fetch GeoIP outside lock
            GeoInfo? geoInfo = null;
            await _lock.WaitAsync();
            _geoCache.TryGetValue(ip, out geoInfo);
            _lock.Release();

            if (geoInfo == null)
            {
                try
                {
                    geoInfo = await SSHManager.Shared.GetGeoInfoAsync(ip);
                    await _lock.WaitAsync();
                    _geoCache[ip] = geoInfo;
                    _lock.Release();
                }
                catch
                {
                    await _lock.WaitAsync();
                    _pendingIPs.Remove(ip);
                    _lock.Release();
                    return;
                }
            }

            await _lock.WaitAsync();
            try
            {
                if (ShouldFilterLocation(geoInfo))
                {
                    _pendingIPs.Remove(ip);
                    return;
                }

                var currentReentry = (_reentryCount.TryGetValue(ip, out var r) ? r : 0) + 1;
                _reentryCount[ip] = currentReentry;

                if (currentReentry >= _maxReentry)
                {
                    _bannedIPs.Add(ip);
                    _pendingIPs.Remove(ip);
                    return;
                }

                if (_sessions.ContainsKey(ip)) { _pendingIPs.Remove(ip); return; }

                var playerName = MatchPlayerName(ip, port, geoInfo);

                var session = new WireGuardSession
                {
                    Ip             = ip,
                    Port           = port,
                    PublicKey      = "",
                    Endpoint       = $"{ip}:{port}",
                    LatestHandshake = timestamp,
                    Country        = geoInfo.Country,
                    City           = geoInfo.City,
                    Isp            = geoInfo.Isp,
                    PlayerName     = playerName,
                    JoinTime       = timestamp,
                    JoinTimeStr    = FormatTime(timestamp),
                    Reentry        = currentReentry,
                    LastSeen       = DateTime.Now
                };

                _sessions[ip] = session;
                _pendingIPs.Remove(ip);
                AddLog(ActivityLog.LogType.Join, session);
                EmitUpdate();
            }
            finally { _lock.Release(); }
        }

        // ─── Location Filtering ──────────────────────────────────────────────────

        private bool ShouldFilterLocation(GeoInfo geo)
            => LocalDatabaseManager.Shared.ShouldFilterLocation(geo.City, geo.Country, geo.Isp);

        private PlayerName? MatchPlayerName(string ip, string port, GeoInfo geo)
        {
            var match = LocalDatabaseManager.Shared.MatchPlayerName(ip, port, geo.City, geo.Isp);
            if (match.HasValue)
                return new PlayerName { Name = match.Value.Name, Score = match.Value.Score, Trusted = match.Value.Trusted };
            return null;
        }

        // ─── Exit Checker ────────────────────────────────────────────────────────

        private void StartExitChecker()
        {
            _exitCheckCts?.Cancel();
            _exitCheckCts = new CancellationTokenSource();
            var token = _exitCheckCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    if (!token.IsCancellationRequested)
                        await CheckExitsAsync();
                }
            }, token);
        }

        private async Task CheckExitsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var now = DateTime.Now;
                if (_startTime == null || (now - _startTime.Value).TotalSeconds <= 7.0)
                {
                    EmitUpdate();
                    return;
                }

                var settings = ConfigManager.Shared.LoadSettings();
                _timeout = settings.ExitTimeout;

                var toRemove = new List<string>();
                foreach (var (ip, session) in _sessions)
                {
                    if ((now - session.LastSeen).TotalSeconds > _timeout)
                    {
                        var stay = FormatDuration((now - session.JoinTime).TotalSeconds);
                        AddLog(ActivityLog.LogType.Exit, session, stay);
                        toRemove.Add(ip);
                        _lastExitTime[ip] = now;
                    }
                }

                foreach (var ip in toRemove)
                    _sessions.Remove(ip);

                UpdatePotentialHost();
                EmitUpdate();
            }
            finally { _lock.Release(); }
        }

        // ─── Potential Host ──────────────────────────────────────────────────────

        private void UpdatePotentialHost()
        {
            if (!_sessions.Any()) { _potentialHost = "None"; return; }

            var now = DateTime.Now;
            double maxScore = 0;
            string? hostIp = null;

            foreach (var (ip, session) in _sessions)
            {
                var score = (now - session.JoinTime).TotalSeconds * 0.97;
                if (score > maxScore) { maxScore = score; hostIp = ip; }
            }

            if (hostIp != null && _sessions.TryGetValue(hostIp, out var s))
            {
                var nameTag = s.PlayerName != null ? $" [{s.PlayerName.Name}]" : "";
                _potentialHost = $"{hostIp}{nameTag} ({s.Country})";
            }
        }

        // ─── Logging ─────────────────────────────────────────────────────────────

        private void AddLog(ActivityLog.LogType type, WireGuardSession session, string? stay = null)
        {
            if (type == ActivityLog.LogType.Join)
            {
                var recent = _recentLogs.FirstOrDefault(l => l.Ip == session.Ip && l.Type == ActivityLog.LogType.Join);
                if (recent != null && (DateTime.Now - recent.Timestamp).TotalSeconds < 2.0) return;
            }

            var log = new ActivityLog
            {
                Type      = type,
                Ip        = session.Ip,
                Port      = session.Port,
                Name      = session.PlayerName?.Name,
                Trusted   = session.PlayerName?.Trusted ?? false,
                Country   = session.Country,
                City      = session.City,
                Isp       = session.Isp,
                Time      = FormatTime(DateTime.Now),
                Stay      = stay,
                Reentry   = session.Reentry,
                Timestamp = DateTime.Now
            };

            _recentLogs.Insert(0, log);
            if (_recentLogs.Count > 10)
                _recentLogs.RemoveRange(10, _recentLogs.Count - 10);

            if (type == ActivityLog.LogType.Exit)
            {
                SaveToHistory(log);
                TRXManager.Shared.CheckQueueOnExit(session.Ip);
                if (TRXManager.Shared.CheckExitAlert(session.Ip))
                {
                    AppState.Shared.ShowToast($"✓ غادر {session.Ip}", ToastType.Success);
                    if (ConfigManager.Shared.LoadSettings().SoundEnabled)
                        System.Media.SystemSounds.Beep.Play();
                }
            }
        }

        private void SaveToHistory(ActivityLog log)
        {
            ConfigManager.Shared.AddToHistory(new SessionHistoryEntry
            {
                Ip      = log.Ip,
                Port    = log.Port,
                Name    = log.Name ?? "",
                Trusted = log.Trusted,
                Country = log.Country,
                City    = log.City,
                Isp     = log.Isp,
                Stay    = log.Stay ?? "",
                Time    = log.Time
            });
        }

        // ─── Emit Updates ────────────────────────────────────────────────────────

        private void EmitUpdate()
        {
            var sessionList = _sessions.Values.OrderByDescending(s => s.JoinTime).ToList();
            var logs = _recentLogs.ToList();
            var host = _potentialHost;

            App.Current?.Dispatcher.BeginInvoke(() =>
                OnUpdate?.Invoke(sessionList, logs, host));
        }

        // ─── Utilities ───────────────────────────────────────────────────────────

        private string FormatTime(DateTime d) => d.ToString("HH:mm:ss");

        private string FormatDuration(double seconds)
        {
            var s = (int)seconds;
            var h = s / 3600; var m = (s % 3600) / 60; var sec = s % 60;
            if (h > 0) return $"{h}h {m:D2}m {sec:D2}s";
            if (m > 0) return $"{m}m {sec:D2}s";
            return $"{sec}s";
        }

        // ─── Public Methods ──────────────────────────────────────────────────────

        public async Task<List<WireGuardSession>> GetSessionsAsync()
        {
            await _lock.WaitAsync();
            try { return _sessions.Values.OrderByDescending(s => s.JoinTime).ToList(); }
            finally { _lock.Release(); }
        }

        public void Cleanup()
        {
            _exitCheckCts?.Cancel();
            _sessions.Clear();
            _recentLogs.Clear();
        }
    }
}

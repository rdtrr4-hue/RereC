using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rere.Models;

namespace Rere.Managers
{
    // ─── Config Manager ──────────────────────────────────────────────────────────
    public class ConfigManager
    {
        public static readonly ConfigManager Shared = new ConfigManager();

        private readonly string _configPath   = Path.Combine(AppData(), "ssh_config.json");
        private readonly string _settingsPath = Path.Combine(AppData(), "app_settings.json");
        private readonly string _historyPath  = Path.Combine(AppData(), "session_history.json");

        private static string AppData()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Rere");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private ConfigManager() { }

        // ─── SSH Config ──────────────────────────────────────────────────────────

        public void SaveConfig(SSHConfig config)
        {
            try { File.WriteAllText(_configPath, JsonSerializer.Serialize(config)); }
            catch (Exception ex) { Console.WriteLine($"Failed to save config: {ex.Message}"); }
        }

        public SSHConfig? LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return null;
                return JsonSerializer.Deserialize<SSHConfig>(File.ReadAllText(_configPath));
            }
            catch { return null; }
        }

        public void ClearConfig()
        {
            try { if (File.Exists(_configPath)) File.Delete(_configPath); } catch { }
        }

        // ─── App Settings ────────────────────────────────────────────────────────

        public void SaveSettings(AppSettings settings)
        {
            try { File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings)); }
            catch (Exception ex) { Console.WriteLine($"Failed to save settings: {ex.Message}"); }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return AppSettings.Default;
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath))
                       ?? AppSettings.Default;
            }
            catch { return AppSettings.Default; }
        }

        public void ResetSettings()
        {
            try { if (File.Exists(_settingsPath)) File.Delete(_settingsPath); } catch { }
        }

        // ─── Session History ─────────────────────────────────────────────────────

        public void SaveHistory(List<SessionHistoryEntry> history)
        {
            try
            {
                var limited = history.Take(500).ToList();
                File.WriteAllText(_historyPath, JsonSerializer.Serialize(limited));
            }
            catch (Exception ex) { Console.WriteLine($"Failed to save history: {ex.Message}"); }
        }

        public List<SessionHistoryEntry> LoadHistory()
        {
            try
            {
                if (!File.Exists(_historyPath)) return new();
                return JsonSerializer.Deserialize<List<SessionHistoryEntry>>(File.ReadAllText(_historyPath))
                       ?? new();
            }
            catch { return new(); }
        }

        public void AddToHistory(SessionHistoryEntry entry)
        {
            var history = LoadHistory();
            history.Insert(0, entry);
            SaveHistory(history);
        }

        public void ClearHistory()
        {
            try { if (File.Exists(_historyPath)) File.Delete(_historyPath); } catch { }
        }
    }

    // ─── Local Database Manager ──────────────────────────────────────────────────
    public class LocalDatabaseManager
    {
        public static readonly LocalDatabaseManager Shared = new LocalDatabaseManager();

        private readonly string _dbPath             = Path.Combine(AppData(), "player_database.json");
        private readonly string _blockedCitiesPath  = Path.Combine(AppData(), "blocked_cities.json");
        private readonly string _blockedISPsPath    = Path.Combine(AppData(), "blocked_isps.json");
        private readonly string _blockedCountriesPath = Path.Combine(AppData(), "blocked_countries.json");
        private readonly string _trustedIPsPath     = Path.Combine(AppData(), "trusted_ips.json");

        private Dictionary<string, LocalPlayerEntry> _playerDb = new();
        private HashSet<string> _blockedCities = new();
        private HashSet<string> _blockedISPs = new();
        private HashSet<string> _blockedCountries = new();
        private HashSet<string> _trustedIPs = new();

        private static string AppData()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Rere");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public class LocalPlayerEntry
        {
            public string Ip { get; set; } = "";
            public string Name { get; set; } = "";
            public string City { get; set; } = "";
            public string Isp { get; set; } = "";
            public string Port { get; set; } = "";
            public string Prefix { get; set; } = "";
            public bool Trusted { get; set; }
            public string? Notes { get; set; }
            public DateTime AddedDate { get; set; }
            public DateTime? LastSeen { get; set; }
        }

        private LocalDatabaseManager() { }

        public void LoadAllDatabases()
        {
            LoadPlayerDatabase();
            LoadBlockedCities();
            LoadBlockedISPs();
            LoadBlockedCountries();
            LoadTrustedIPs();
        }

        // ─── Player Database ─────────────────────────────────────────────────────

        private void LoadPlayerDatabase()
        {
            try
            {
                if (File.Exists(_dbPath))
                    _playerDb = JsonSerializer.Deserialize<Dictionary<string, LocalPlayerEntry>>(
                        File.ReadAllText(_dbPath)) ?? new();
            }
            catch { _playerDb = new(); }
        }

        private void SavePlayerDatabase()
        {
            try { File.WriteAllText(_dbPath, JsonSerializer.Serialize(_playerDb)); }
            catch { }
        }

        public void AddPlayer(string ip, string name, string city = "", string isp = "",
            string port = "", bool trusted = false, string? notes = null)
        {
            var prefix = string.Join(".", ip.Split('.').Take(2));
            _playerDb[ip] = new LocalPlayerEntry
            {
                Ip = ip, Name = name, City = city, Isp = isp, Port = port,
                Prefix = prefix, Trusted = trusted, Notes = notes,
                AddedDate = DateTime.Now
            };
            SavePlayerDatabase();
        }

        public void RemovePlayer(string ip)
        {
            _playerDb.Remove(ip);
            SavePlayerDatabase();
        }

        public LocalPlayerEntry? GetPlayer(string ip)
            => _playerDb.TryGetValue(ip, out var p) ? p : null;

        public List<LocalPlayerEntry> GetAllPlayers()
            => _playerDb.Values.OrderByDescending(p => p.AddedDate).ToList();

        public List<LocalPlayerEntry> SearchPlayers(string query)
        {
            var q = query.ToLower();
            return _playerDb.Values.Where(p =>
                p.Ip.ToLower().Contains(q) || p.Name.ToLower().Contains(q) ||
                p.City.ToLower().Contains(q) || p.Isp.ToLower().Contains(q)).ToList();
        }

        public (string Name, int Score, bool Trusted)? MatchPlayerName(
            string ip, string port, string city, string isp)
        {
            if (_playerDb.TryGetValue(ip, out var exact))
            {
                if (exact.LastSeen == null)
                {
                    exact.LastSeen = DateTime.Now;
                    SavePlayerDatabase();
                }
                return (exact.Name, 4, true);
            }

            var prefix = string.Join(".", ip.Split('.').Take(2));
            var maxScore = 0;
            (string Name, int Score)? best = null;

            foreach (var (dbIp, player) in _playerDb)
            {
                if (player.Prefix != prefix || dbIp.Contains('*')) continue;
                var score = 0;
                if (player.Port == port) score++;
                if (player.City == city) score++;
                if (player.Isp  == isp)  score++;
                if (score > maxScore) { maxScore = score; best = (player.Name, score); }
            }

            if (best.HasValue && maxScore >= 1)
                return (best.Value.Name, best.Value.Score, false);

            return null;
        }

        // ─── Trusted IPs ─────────────────────────────────────────────────────────

        private void LoadTrustedIPs()
        {
            try
            {
                if (File.Exists(_trustedIPsPath))
                    _trustedIPs = JsonSerializer.Deserialize<HashSet<string>>(
                        File.ReadAllText(_trustedIPsPath)) ?? new();
            }
            catch { }
        }

        public void AddTrustedIP(string ip) { _trustedIPs.Add(ip); Save(_trustedIPsPath, _trustedIPs); }
        public void RemoveTrustedIP(string ip) { _trustedIPs.Remove(ip); Save(_trustedIPsPath, _trustedIPs); }
        public bool IsTrustedIP(string ip) => _trustedIPs.Contains(ip);

        // ─── Blocked Cities ──────────────────────────────────────────────────────

        private void LoadBlockedCities()
        {
            try
            {
                if (File.Exists(_blockedCitiesPath))
                    _blockedCities = JsonSerializer.Deserialize<HashSet<string>>(
                        File.ReadAllText(_blockedCitiesPath)) ?? new();
                else
                {
                    _blockedCities = new HashSet<string> { "Athens", "Hong Kong" };
                    Save(_blockedCitiesPath, _blockedCities);
                }
            }
            catch { _blockedCities = new HashSet<string> { "Athens", "Hong Kong" }; }
        }

        public void AddBlockedCity(string city) { _blockedCities.Add(city); Save(_blockedCitiesPath, _blockedCities); }
        public void RemoveBlockedCity(string city) { _blockedCities.Remove(city); Save(_blockedCitiesPath, _blockedCities); }
        public bool IsBlockedCity(string city) => _blockedCities.Contains(city);
        public List<string> GetAllBlockedCities() => _blockedCities.OrderBy(c => c).ToList();

        // ─── Blocked ISPs ────────────────────────────────────────────────────────

        private void LoadBlockedISPs()
        {
            try
            {
                if (File.Exists(_blockedISPsPath))
                    _blockedISPs = JsonSerializer.Deserialize<HashSet<string>>(
                        File.ReadAllText(_blockedISPsPath)) ?? new();
                else
                {
                    _blockedISPs = new HashSet<string> { "dnsat", "take-two", "take two", "rockstar", "2k games" };
                    Save(_blockedISPsPath, _blockedISPs);
                }
            }
            catch { _blockedISPs = new HashSet<string> { "dnsat", "take-two", "take two", "rockstar", "2k games" }; }
        }

        public void AddBlockedISP(string isp) { _blockedISPs.Add(isp.ToLower()); Save(_blockedISPsPath, _blockedISPs); }
        public void RemoveBlockedISP(string isp) { _blockedISPs.Remove(isp.ToLower()); Save(_blockedISPsPath, _blockedISPs); }
        public bool IsBlockedISP(string isp) => _blockedISPs.Any(b => isp.ToLower().Contains(b));
        public List<string> GetAllBlockedISPs() => _blockedISPs.OrderBy(i => i).ToList();

        // ─── Blocked Countries ───────────────────────────────────────────────────

        private void LoadBlockedCountries()
        {
            try
            {
                if (File.Exists(_blockedCountriesPath))
                    _blockedCountries = JsonSerializer.Deserialize<HashSet<string>>(
                        File.ReadAllText(_blockedCountriesPath)) ?? new();
                else
                {
                    _blockedCountries = new HashSet<string> { "HK" };
                    Save(_blockedCountriesPath, _blockedCountries);
                }
            }
            catch { _blockedCountries = new HashSet<string> { "HK" }; }
        }

        public void AddBlockedCountry(string c) { _blockedCountries.Add(c.ToUpper()); Save(_blockedCountriesPath, _blockedCountries); }
        public void RemoveBlockedCountry(string c) { _blockedCountries.Remove(c.ToUpper()); Save(_blockedCountriesPath, _blockedCountries); }
        public bool IsBlockedCountry(string c) => _blockedCountries.Contains(c.ToUpper());
        public List<string> GetAllBlockedCountries() => _blockedCountries.OrderBy(c => c).ToList();

        // ─── Location Filter ─────────────────────────────────────────────────────

        public bool ShouldFilterLocation(string city, string country, string isp)
            => IsBlockedCity(city) || IsBlockedCountry(country) || IsBlockedISP(isp);

        // ─── Import/Export ───────────────────────────────────────────────────────

        public string ExportPlayerDatabase()
        {
            var lines = GetAllPlayers()
                .Select(p => $"{p.Ip}|{p.Name}|{p.City}|{p.Isp}|{p.Port}|{p.Prefix}");
            return string.Join("\n", lines);
        }

        public (int Success, int Failed) ImportPlayerDatabase(string content)
        {
            var success = 0; var failed = 0;
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 2) { failed++; continue; }
                var ip   = parts[0].Trim(); var name = parts[1].Trim();
                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(name)) { failed++; continue; }
                AddPlayer(ip, name,
                    parts.Length > 2 ? parts[2].Trim() : "",
                    parts.Length > 3 ? parts[3].Trim() : "",
                    parts.Length > 4 ? parts[4].Trim() : "",
                    !ip.Contains('*'));
                success++;
            }
            return (success, failed);
        }

        public void ClearAllData()
        {
            _playerDb.Clear(); _blockedCities.Clear(); _blockedISPs.Clear();
            _blockedCountries.Clear(); _trustedIPs.Clear();
            SavePlayerDatabase();
            Save(_blockedCitiesPath, _blockedCities);
            Save(_blockedISPsPath,   _blockedISPs);
            Save(_blockedCountriesPath, _blockedCountries);
            Save(_trustedIPsPath,    _trustedIPs);
        }

        public (int TotalPlayers, int TrustedPlayers, int BlockedCities, int BlockedISPs) GetStatistics()
            => (_playerDb.Count, _playerDb.Values.Count(p => p.Trusted),
                _blockedCities.Count, _blockedISPs.Count);

        private void Save<T>(string path, T obj)
        {
            try { File.WriteAllText(path, JsonSerializer.Serialize(obj)); } catch { }
        }
    }

    // ─── Geo Lookup ──────────────────────────────────────────────────────────────
    public class GeoLookup
    {
        public static readonly GeoLookup Shared = new GeoLookup();
        private GeoLookup() { }
        public bool IsAvailable => false; // Always delegates to SSH

        public Task<GeoInfo> LookupAsync(string ip)
            => SSHManager.Shared.GetGeoInfoAsync(ip);
    }
}

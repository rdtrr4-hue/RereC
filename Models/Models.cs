using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Rere.Models
{
    // ─── WireGuard Session ───────────────────────────────────────────────────────
    public class WireGuardSession : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Ip { get; set; } = "";
        public string Port { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public DateTime LatestHandshake { get; set; }
        public long TransferRx { get; set; }
        public long TransferTx { get; set; }
        public int Keepalive { get; set; }
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public PlayerName? PlayerName { get; set; }
        public DateTime JoinTime { get; set; }
        public string JoinTimeStr { get; set; } = "";
        public int Reentry { get; set; }
        public DateTime LastSeen { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Player Name Match ───────────────────────────────────────────────────────
    public class PlayerName
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public bool Trusted { get; set; }
    }

    // ─── Activity Log ────────────────────────────────────────────────────────────
    public class ActivityLog
    {
        public Guid Id { get; } = Guid.NewGuid();
        public LogType Type { get; set; }
        public string Ip { get; set; } = "";
        public string Port { get; set; } = "";
        public string? Name { get; set; }
        public bool Trusted { get; set; }
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public string Time { get; set; } = "";
        public string? Stay { get; set; }
        public int Reentry { get; set; }
        public DateTime Timestamp { get; set; }

        public enum LogType { Join, Exit }
    }

    // ─── Session History Entry ───────────────────────────────────────────────────
    public class SessionHistoryEntry
    {
        public string Ip { get; set; } = "";
        public string Port { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Trusted { get; set; }
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public string Stay { get; set; } = "";
        public string Time { get; set; } = "";
    }

    // ─── TRX Mode ────────────────────────────────────────────────────────────────
    public enum TRXMode { Normal, Manual, Bypass }

    // ─── TRX Entry ───────────────────────────────────────────────────────────────
    public class TRXEntry : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Ip { get; set; } = "";
        public string Port { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime AddedAt { get; set; }

        private TRXStatus _status;
        public TRXStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string? Method { get; set; }
        public int? CustomDuration { get; set; }
        public TRXMode Mode { get; set; }

        private int _bypassCurrentWave;
        public int BypassCurrentWave
        {
            get => _bypassCurrentWave;
            set { _bypassCurrentWave = value; OnPropertyChanged(); }
        }

        public int BypassTotalWaves { get; set; } = 5;

        private string _bypassWaveMethod = "";
        public string BypassWaveMethod
        {
            get => _bypassWaveMethod;
            set { _bypassWaveMethod = value; OnPropertyChanged(); }
        }

        public enum TRXStatus
        {
            Pending, Sending, Active, Success, Failed
        }

        public string StatusLabel => Status switch
        {
            TRXStatus.Pending => "في الانتظار",
            TRXStatus.Sending => "يُرسل الآن",
            TRXStatus.Active  => "نشط",
            TRXStatus.Success => "نجح",
            TRXStatus.Failed  => "فشل",
            _ => ""
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── TRX Log Entry ───────────────────────────────────────────────────────────
    public class TRXLogEntry : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Ip { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime SentAt { get; set; }
        public int Duration { get; set; }
        public string Method { get; set; } = "";
        public TRXResult Result { get; set; }

        private TRXVerdict _verdict;
        public TRXVerdict Verdict
        {
            get => _verdict;
            set { _verdict = value; OnPropertyChanged(); OnPropertyChanged(nameof(VerdictLabel)); }
        }

        public bool IsBypass { get; set; }
        public int BypassWaves { get; set; }

        public string VerdictLabel => Verdict switch
        {
            TRXVerdict.Pending => "في الانتظار",
            TRXVerdict.Exited  => "غادر ✓",
            TRXVerdict.Stayed  => "بقي ✗",
            _ => ""
        };

        public enum TRXResult { Success, Failed }
        public enum TRXVerdict { Pending, Exited, Stayed }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── App Settings ────────────────────────────────────────────────────────────
    public class AppSettings
    {
        public int QueueDelay { get; set; } = 65;
        public int RereDuration { get; set; } = 120;
        public int RetryDelay { get; set; } = 3;
        public int ExitAlert { get; set; } = 75;
        public int ExitTimeout { get; set; } = 7;
        public int MaxHistory { get; set; } = 500;
        public bool SoundEnabled { get; set; } = true;

        public static AppSettings Default => new AppSettings();
    }

    // ─── SSH Config ──────────────────────────────────────────────────────────────
    public class SSHConfig
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public AuthType AuthenticationMethod { get; set; } = AuthType.Password;
        public string? Password { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? Passphrase { get; set; }

        public enum AuthType { Password, PrivateKey }
    }

    // ─── Geo Info ────────────────────────────────────────────────────────────────
    public class GeoInfo
    {
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
    }

    // ─── Player Info ─────────────────────────────────────────────────────────────
    public class PlayerInfo
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public string Port { get; set; } = "";
        public string Prefix { get; set; } = "";
    }

    // ─── Server Stats ────────────────────────────────────────────────────────────
    public class ServerStats : INotifyPropertyChanged
    {
        private string _hostname = "-";
        public string Hostname { get => _hostname; set { _hostname = value; OnPropertyChanged(); } }

        private string _uptime = "-";
        public string Uptime { get => _uptime; set { _uptime = value; OnPropertyChanged(); } }

        private string _cpuLoad = "--%";
        public string CpuLoad { get => _cpuLoad; set { _cpuLoad = value; OnPropertyChanged(); } }

        private int _sessionCount;
        public int SessionCount { get => _sessionCount; set { _sessionCount = value; OnPropertyChanged(); } }

        private string _potentialHost = "-";
        public string PotentialHost { get => _potentialHost; set { _potentialHost = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Toast Types ─────────────────────────────────────────────────────────────
    public enum ToastType { Success, Error, Warning, Info }
}

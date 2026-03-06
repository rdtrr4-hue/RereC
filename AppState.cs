using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Rere.Managers;
using Rere.Models;

namespace Rere
{
    // ─── Global App State ────────────────────────────────────────────────────────
    public class AppState : INotifyPropertyChanged
    {
        public static readonly AppState Shared = new AppState();

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        private ObservableCollection<WireGuardSession> _sessions = new();
        public ObservableCollection<WireGuardSession> Sessions
        {
            get => _sessions;
            set { _sessions = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ActivityLog> _logs = new();
        public ObservableCollection<ActivityLog> Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        private string _potentialHost = "-";
        public string PotentialHost
        {
            get => _potentialHost;
            set { _potentialHost = value; OnPropertyChanged(); }
        }

        private ObservableCollection<TRXEntry> _trxQueue = new();
        public ObservableCollection<TRXEntry> TrxQueue
        {
            get => _trxQueue;
            set { _trxQueue = value; OnPropertyChanged(); }
        }

        private string? _trxCurrentIP;
        public string? TrxCurrentIP
        {
            get => _trxCurrentIP;
            set { _trxCurrentIP = value; OnPropertyChanged(); }
        }

        private int _trxCountdown;
        public int TrxCountdown
        {
            get => _trxCountdown;
            set { _trxCountdown = value; OnPropertyChanged(); }
        }

        private string? _trxActiveIP;
        public string? TrxActiveIP
        {
            get => _trxActiveIP;
            set { _trxActiveIP = value; OnPropertyChanged(); }
        }

        private int _trxActiveCountdown;
        public int TrxActiveCountdown
        {
            get => _trxActiveCountdown;
            set { _trxActiveCountdown = value; OnPropertyChanged(); }
        }

        private ObservableCollection<TRXLogEntry> _trxLog = new();
        public ObservableCollection<TRXLogEntry> TrxLog
        {
            get => _trxLog;
            set { _trxLog = value; OnPropertyChanged(); }
        }

        private string? _toastMessage;
        public string? ToastMessage
        {
            get => _toastMessage;
            set { _toastMessage = value; OnPropertyChanged(); }
        }

        private ToastType _toastType = ToastType.Info;
        public ToastType ToastType
        {
            get => _toastType;
            set { _toastType = value; OnPropertyChanged(); }
        }

        private List<SessionHistoryEntry> _history = new();
        public List<SessionHistoryEntry> History
        {
            get => _history;
            set { _history = value; OnPropertyChanged(); }
        }

        private ServerStats _serverStats = new();
        public ServerStats ServerStats
        {
            get => _serverStats;
            set { _serverStats = value; OnPropertyChanged(); }
        }

        private SSHConfig? _sshConfig;
        public SSHConfig? SshConfig
        {
            get => _sshConfig;
            set { _sshConfig = value; OnPropertyChanged(); }
        }

        private System.Threading.Tasks.Task? _toastTask;

        private AppState()
        {
            // Wire up SessionManager callbacks
            SessionManager.Shared.OnUpdate = (sessions, logs, host) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Sessions.Clear();
                    foreach (var s in sessions) Sessions.Add(s);

                    Logs.Clear();
                    foreach (var l in logs) Logs.Add(l);

                    PotentialHost = host;
                });
            };

            // Wire up TRXManager callbacks
            TRXManager.Shared.OnQueueUpdate = (queue, currentIP, countdown, activeIP, activeCountdown, log) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    TrxQueue.Clear();
                    foreach (var e in queue) TrxQueue.Add(e);

                    TrxCurrentIP        = currentIP;
                    TrxCountdown        = countdown;
                    TrxActiveIP         = activeIP;
                    TrxActiveCountdown  = activeCountdown;

                    TrxLog.Clear();
                    foreach (var l in log) TrxLog.Add(l);
                });
            };

            TRXManager.Shared.OnToast  = (msg, type) => ShowToast(msg, type);

            // Load history
            LoadHistory();
        }

        // ─── Toast ───────────────────────────────────────────────────────────────

        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ToastMessage = message;
                ToastType    = type;
            });

            _toastTask = Task.Delay(3000).ContinueWith(_ =>
                App.Current.Dispatcher.Invoke(() => ToastMessage = null));
        }

        // ─── History ─────────────────────────────────────────────────────────────

        public void LoadHistory()
        {
            History = ConfigManager.Shared.LoadHistory();
        }

        // ─── PropertyChanged ─────────────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

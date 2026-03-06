using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rere.Managers;
using Rere.Models;

namespace Rere
{
    public partial class MainWindow : Window
    {
        private string _currentPage = "sessions";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppState.Shared;

            // Wire up data events from SSH
            SSHManager.Shared.OnData = OnPacketReceived;
            SSHManager.Shared.OnError = OnSSHError;
            SSHManager.Shared.OnDisconnected = OnSSHDisconnected;

            // Update UI after session updates
            SessionManager.Shared.OnUpdate = (sessions, logs, host) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AppState.Shared.Sessions.Clear();
                    foreach (var s in sessions) AppState.Shared.Sessions.Add(s);

                    AppState.Shared.Logs.Clear();
                    foreach (var l in logs) AppState.Shared.Logs.Add(l);

                    AppState.Shared.PotentialHost = host;

                    TxSessionCount.Text = $"({sessions.Count} متصل)";
                    TxHostHeader.Text   = host;
                    TxTRXCount.Text     = $"({AppState.Shared.TrxQueue.Count} في الطابور)";
                });
            };

            // Load saved config
            var cfg = ConfigManager.Shared.LoadConfig();
            if (cfg != null)
                AppState.Shared.SshConfig = cfg;

            ShowPage("sessions");
            UpdateConnectionStatus();
        }

        // ─── Title Bar ───────────────────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SSHManager.Shared.Disconnect();
            Application.Current.Shutdown();
        }

        // ─── Navigation ──────────────────────────────────────────────────────────

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            ShowPage(btn.Tag?.ToString() ?? "sessions");
        }

        private void ShowPage(string page)
        {
            _currentPage = page;

            PageSessions.Visibility = page == "sessions" ? Visibility.Visible : Visibility.Collapsed;
            PageTRX.Visibility      = page == "trx"      ? Visibility.Visible : Visibility.Collapsed;
            PageContent.Visibility  = Visibility.Collapsed;

            if (page == "history")   ShowHistoryPage();
            else if (page == "players") ShowPlayersPage();
            else if (page == "filters") ShowFiltersPage();
            else if (page == "settings") ShowSettingsPage();
            else if (page == "activity") ShowActivityPage();

            // Update sidebar button highlight
            foreach (var child in ((StackPanel)BtnNavSessions.Parent).Children)
            {
                if (child is Button btn)
                {
                    btn.Background = btn.Tag?.ToString() == page
                        ? new SolidColorBrush(Color.FromArgb(0x30, 0x3B, 0x82, 0xF6))
                        : Brushes.Transparent;
                    btn.Foreground = btn.Tag?.ToString() == page
                        ? (Brush)FindResource("BrAccentBlue")
                        : (Brush)FindResource("BrTextSecondary");
                }
            }
        }

        // ─── Connect / Disconnect ────────────────────────────────────────────────

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.Shared.IsConnected)
            {
                SSHManager.Shared.Disconnect();
                SessionManager.Shared.Cleanup();
                AppState.Shared.IsConnected = false;
                UpdateConnectionStatus();
                return;
            }

            // Show SSH config dialog if not configured
            var config = AppState.Shared.SshConfig;
            if (config == null)
            {
                var dlg = new SSHConfigDialog();
                if (dlg.ShowDialog() == true)
                    config = dlg.Config;
                else
                    return;
            }

            BtnConnect.IsEnabled = false;
            AppState.Shared.ShowToast("⏳ جاري الاتصال...", ToastType.Info);

            try
            {
                await SSHManager.Shared.ConnectAsync(config!);
                AppState.Shared.SshConfig = config;
                ConfigManager.Shared.SaveConfig(config!);

                await SessionManager.Shared.InitializeAsync();
                await SSHManager.Shared.StartTcpDumpMonitorAsync();

                AppState.Shared.IsConnected = true;

                // Fetch server stats
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (hostname, uptime, _) = await SSHManager.Shared.GetSystemInfoAsync();
                        Dispatcher.Invoke(() =>
                        {
                            TxHostname.Text = hostname;
                            TxUptime.Text   = uptime;
                        });
                    }
                    catch { }
                });

                AppState.Shared.ShowToast("✅ متصل بنجاح", ToastType.Success);
                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                AppState.Shared.ShowToast($"✗ فشل الاتصال: {ex.Message}", ToastType.Error);
            }
            finally
            {
                BtnConnect.IsEnabled = true;
            }
        }

        private void UpdateConnectionStatus()
        {
            var connected = AppState.Shared.IsConnected;
            TxConnectionStatus.Text = connected
                ? $"متصل — {AppState.Shared.SshConfig?.Host}"
                : "غير متصل";
            TxConnectionStatus.Foreground = connected
                ? (Brush)FindResource("BrAccentGreen")
                : (Brush)FindResource("BrTextMuted");
        }

        // ─── SSH Callbacks ───────────────────────────────────────────────────────

        private void OnPacketReceived(byte[] data)
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(data).RootElement;
                var ip        = json.GetProperty("ip").GetString() ?? "";
                var port      = json.GetProperty("port").GetString() ?? "";
                var tsMs      = json.GetProperty("timestamp").GetInt64();
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).DateTime;

                _ = SessionManager.Shared.HandlePacketAsync(ip, port, timestamp);
            }
            catch { }
        }

        private void OnSSHError(Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                AppState.Shared.ShowToast($"⚠️ خطأ SSH: {ex.Message}", ToastType.Error);
            });
        }

        private void OnSSHDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                AppState.Shared.IsConnected = false;
                UpdateConnectionStatus();
                AppState.Shared.ShowToast("🔌 انقطع الاتصال", ToastType.Warning);
            });
        }

        // ─── Session Selection ───────────────────────────────────────────────────

        private void GridSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridSessions.SelectedItem is WireGuardSession session)
                ShowSessionContextMenu(session);
        }

        private void ShowSessionContextMenu(WireGuardSession session)
        {
            var menu = new ContextMenu();

            var trxItem = new MenuItem { Header = $"⚡ TRX — {session.Ip}" };
            trxItem.Click += (_, _) =>
            {
                TRXManager.Shared.AddToQueue(
                    session.Ip, session.Port,
                    session.PlayerName?.Name ?? session.Ip);
                ShowPage("trx");
                AppState.Shared.ShowToast($"✓ أُضيف {session.Ip} للطابور", ToastType.Success);
            };

            var bypassItem = new MenuItem { Header = $"⚡⚡ Bypass — {session.Ip}" };
            bypassItem.Click += (_, _) =>
            {
                TRXManager.Shared.AddToQueue(
                    session.Ip, session.Port,
                    session.PlayerName?.Name ?? session.Ip,
                    mode: TRXMode.Bypass);
                ShowPage("trx");
                AppState.Shared.ShowToast($"⚡ Bypass مضاف: {session.Ip}", ToastType.Success);
            };

            var copyItem = new MenuItem { Header = $"📋 نسخ IP" };
            copyItem.Click += (_, _) => Clipboard.SetText(session.Ip);

            menu.Items.Add(trxItem);
            menu.Items.Add(bypassItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(copyItem);
            menu.IsOpen = true;
        }

        // ─── TRX Handlers ────────────────────────────────────────────────────────

        private void BtnAddTRXNormal_Click(object sender, RoutedEventArgs e)
        {
            var ip   = TxTRXIP.Text.Trim();
            var port = TxTRXPort.Text.Trim();
            var name = TxTRXName.Text.Trim();
            if (string.IsNullOrEmpty(ip)) return;

            TRXManager.Shared.AddToQueue(ip, port, name, mode: TRXMode.Normal);
            TxTRXIP.Clear(); TxTRXName.Clear();
        }

        private void BtnAddTRXManual_Click(object sender, RoutedEventArgs e)
        {
            var ip   = TxTRXIP.Text.Trim();
            var port = TxTRXPort.Text.Trim();
            var name = TxTRXName.Text.Trim();
            if (string.IsNullOrEmpty(ip)) return;

            // Show method picker
            var dlg = new TRXMethodDialog();
            if (dlg.ShowDialog() == true)
            {
                TRXManager.Shared.AddToQueue(ip, port, name,
                    method: dlg.SelectedMethod, mode: TRXMode.Manual);
                TxTRXIP.Clear(); TxTRXName.Clear();
            }
        }

        private void BtnAddBypass_Click(object sender, RoutedEventArgs e)
        {
            var ip   = TxTRXIP.Text.Trim();
            var port = TxTRXPort.Text.Trim();
            var name = TxTRXName.Text.Trim();
            if (string.IsNullOrEmpty(ip)) return;

            TRXManager.Shared.AddToQueue(ip, port, name, mode: TRXMode.Bypass);
            TxTRXIP.Clear(); TxTRXName.Clear();
        }

        private void BtnCancelCurrentTRX_Click(object sender, RoutedEventArgs e)
            => TRXManager.Shared.CancelCurrent();

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
            => TRXManager.Shared.ClearQueue();

        private void BtnRemoveTRX_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string ip)
                TRXManager.Shared.RemoveFromQueue(ip);
        }

        // ─── Sub-pages ───────────────────────────────────────────────────────────

        private void ShowHistoryPage()
        {
            PageContent.Visibility = Visibility.Visible;
            PageContent.Content    = new Views.HistoryView();
        }

        private void ShowPlayersPage()
        {
            PageContent.Visibility = Visibility.Visible;
            PageContent.Content    = new Views.PlayersView();
        }

        private void ShowFiltersPage()
        {
            PageContent.Visibility = Visibility.Visible;
            PageContent.Content    = new Views.FiltersView();
        }

        private void ShowSettingsPage()
        {
            PageContent.Visibility = Visibility.Visible;
            PageContent.Content    = new Views.SettingsView();
        }

        private void ShowActivityPage()
        {
            PageContent.Visibility = Visibility.Visible;
            PageContent.Content    = new Views.ActivityView();
        }
    }
}

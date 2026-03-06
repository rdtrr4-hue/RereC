using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rere.Models;

namespace Rere
{
    public partial class SSHConfigDialog : Window
    {
        public SSHConfig? Config { get; private set; }

        public SSHConfigDialog()
        {
            InitializeComponent();

            var saved = Managers.ConfigManager.Shared.LoadConfig();
            if (saved != null)
            {
                TxHost.Text     = saved.Host;
                TxPort.Text     = saved.Port.ToString();
                TxUsername.Text = saved.Username;

                if (saved.AuthenticationMethod == SSHConfig.AuthType.Password)
                {
                    RbPassword.IsChecked = true;
                    TxPassword.Password  = saved.Password ?? "";
                }
                else
                {
                    RbPrivateKey.IsChecked = true;
                    TxKeyPath.Text         = saved.PrivateKeyPath ?? "";
                    PanelPassword.Visibility    = Visibility.Collapsed;
                    PanelPrivateKey.Visibility  = Visibility.Visible;
                }
            }
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void AuthType_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelPassword == null) return;
            PanelPassword.Visibility   = RbPassword.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelPrivateKey.Visibility = RbPrivateKey.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnBrowseKey_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "اختر ملف المفتاح الخاص",
                Filter = "Key files|*.pem;*.key;*|All files|*.*"
            };
            if (dlg.ShowDialog() == true)
                TxKeyPath.Text = dlg.FileName;
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var cfg = BuildConfig();
            if (cfg == null) { TxTestResult.Text = "❌ تحقق من البيانات"; return; }

            BtnTest.IsEnabled = false;
            BtnTest.Content   = "⏳ اختبار...";
            TxTestResult.Text = "";

            var ok = await Managers.SSHManager.Shared.TestConnectionAsync(cfg);

            TxTestResult.Text       = ok ? "✅ الاتصال نجح" : "✗ فشل الاتصال";
            TxTestResult.Foreground = ok
                ? System.Windows.Media.Brushes.LimeGreen
                : System.Windows.Media.Brushes.Red;

            BtnTest.IsEnabled = true;
            BtnTest.Content   = "اختبار الاتصال";
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            Config = BuildConfig();
            if (Config == null) return;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private SSHConfig? BuildConfig()
        {
            var host     = TxHost.Text.Trim();
            var username = TxUsername.Text.Trim();
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username)) return null;

            var port = int.TryParse(TxPort.Text, out var p) ? p : 22;
            var auth = RbPassword.IsChecked == true
                ? SSHConfig.AuthType.Password
                : SSHConfig.AuthType.PrivateKey;

            return new SSHConfig
            {
                Host                 = host,
                Port                 = port,
                Username             = username,
                AuthenticationMethod = auth,
                Password             = auth == SSHConfig.AuthType.Password ? TxPassword.Password : null,
                PrivateKeyPath       = auth == SSHConfig.AuthType.PrivateKey ? TxKeyPath.Text.Trim() : null
            };
        }
    }
}

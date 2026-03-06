using System.Windows;
using System.Windows.Controls;
using Rere.Models;

namespace Rere
{
    public partial class SSHConfigDialog : Window
    {
        public SSHConfig? Config { get; private set; }

        public SSHConfigDialog()
        {
            InitializeComponent();

            // Pre-fill with saved config
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
                }
            }
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var cfg = BuildConfig();
            if (cfg == null) return;

            BtnTest.IsEnabled    = false;
            BtnTest.Content      = "⏳ اختبار...";
            TxTestResult.Text    = "";

            var ok = await Managers.SSHManager.Shared.TestConnectionAsync(cfg);
            TxTestResult.Text      = ok ? "✅ الاتصال نجح" : "✗ فشل الاتصال";
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

        // Placeholder accessors for inline XAML (defined via InitializeComponent)
        private TextBox TxHost       => (TextBox)FindName(nameof(TxHost));
        private TextBox TxPort       => (TextBox)FindName(nameof(TxPort));
        private TextBox TxUsername   => (TextBox)FindName(nameof(TxUsername));
        private PasswordBox TxPassword => (PasswordBox)FindName(nameof(TxPassword));
        private TextBox TxKeyPath    => (TextBox)FindName(nameof(TxKeyPath));
        private RadioButton RbPassword => (RadioButton)FindName(nameof(RbPassword));
        private RadioButton RbPrivateKey => (RadioButton)FindName(nameof(RbPrivateKey));
        private Button BtnTest       => (Button)FindName(nameof(BtnTest));
        private TextBlock TxTestResult => (TextBlock)FindName(nameof(TxTestResult));
    }
}

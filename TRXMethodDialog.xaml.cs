using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Rere
{
    public partial class TRXMethodDialog : Window
    {
        public string SelectedMethod { get; private set; } = "BOTNET-GAME";

        public TRXMethodDialog()
        {
            InitializeComponent();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string method)
            {
                SelectedMethod = method;
                DialogResult   = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
    }
}

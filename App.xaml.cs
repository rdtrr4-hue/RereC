using System.Windows;

namespace Rere
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Pre-initialize AppState singleton (wires up callbacks)
            _ = AppState.Shared;
        }
    }
}

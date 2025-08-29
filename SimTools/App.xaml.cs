using System;       // for StringComparison
using System.Linq;  // for e.Args.Any(...)
using System.Windows;
using SimTools.Services;  // VirtualGamepadService
using SimTools.Views;

namespace SimTools
{
    public partial class App : Application
    {
        private VirtualGamepadService _vjoy;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Optional: HidHide postinstall
            if (e.Args != null && e.Args.Any(a => string.Equals(a, "--postinstall", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    if (DriverBootstrap.IsHidHideInstalled())
                        DriverBootstrap.EnsureHidHideWhitelist();
                }
                catch { /* ignore */ }
            }

            // Start vJoy wrapper (assumes Device #1 is present & configured)
            _vjoy = new VirtualGamepadService();
            _vjoy.TryStart();   // we don't block the app even if it fails; just try

            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _vjoy?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}

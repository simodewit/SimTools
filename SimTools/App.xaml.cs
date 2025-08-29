using System;
using System.Linq;
using System.Windows;
using SimTools.Services;
using SimTools.Views;

namespace SimTools
{
    public partial class App : Application
    {
        private VirtualGamepadService _vjoy;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args != null && e.Args.Any(a => string.Equals(a, "--postinstall", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    if (DriverBootstrap.IsHidHideInstalled())
                        DriverBootstrap.EnsureHidHideWhitelist();
                }
                catch { /* ignore */ }
            }

            _vjoy = new VirtualGamepadService();
            _vjoy.TryStart();

            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _vjoy?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
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

            // Global safety nets to ensure graceful exits in debug/release
            this.DispatcherUnhandledException += (s, ex) =>
            {
                // TODO: log ex.Exception
                ex.Handled = true;
                Shutdown(0);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                // TODO: log ex.ExceptionObject
                Shutdown(0);
            };
            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                // TODO: log ex.Exception
                ex.SetObserved();
                Shutdown(0);
            };

            // --- Pre-launch updater ---
            bool continueToApp = true;
            try
            {
                var upd = new UpdateCheckWindow { Owner = null };
                // ShowDialog returns:
                //   true  => continue into app
                //   false => user chose Close or update is launching
                //   null  => window was closed: treat as false for safety
                continueToApp = upd.ShowDialog() ?? false;
            }
            catch
            {
                // If the popup itself fails, we choose to exit (quietly)
                continueToApp = false;
            }

            if(!continueToApp)
            {
                Shutdown(0);
                return;
            }

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

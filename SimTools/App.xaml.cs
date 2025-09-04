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

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            /*

            // Keep graceful exits if anything goes sideways
            this.DispatcherUnhandledException += (s, ex) => { ex.Handled = true; Shutdown(0); };
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => { Shutdown(0); };
            TaskScheduler.UnobservedTaskException += (s, ex) => { ex.SetObserved(); Shutdown(0); };

            // --- Pre-launch splash/updater (modeless, shows in taskbar) ---
            var splash = new UpdateCheckWindow();
            splash.Show();               // modeless => appears in taskbar/Alt-Tab
            splash.Activate();           // try to foreground
            splash.Topmost = true;       // bring to front once…
            splash.Topmost = false;      // …then return to normal

            bool continueToApp = await splash.Completion; // waits until splash closes

            if(!continueToApp)
            {
                Shutdown(0);
                return;
            }

            */

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

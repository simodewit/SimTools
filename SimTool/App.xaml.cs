using System;
using System.Linq;
using System.Windows;
using SimTools.Services;
using SimTools.Views;

namespace SimTools
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle post-install whitelisting explicitly (HidHide)
            if(e?.Args != null && e.Args.Any(a =>
                    string.Equals(a, "--postinstall", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    if(DriverBootstrap.IsHidHideInstalled())
                        DriverBootstrap.EnsureHidHideWhitelist();
                }
                catch
                {
                    // Swallow — whitelisting is best-effort and can also be done from settings
                }
            }
            else
            {
                // Non-blocking bootstrap (e.g., vJoy + HidHide detection/installation + whitelisting)
                // Runs after the dispatcher is pumping so the UI comes up immediately.
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await DriverBootstrap.EnsureAllAsync();
                    }
                    catch
                    {
                        // Best-effort; show continues even if drivers aren't ready yet
                    }
                }));
            }

            new MainWindow().Show();
        }
    }
}

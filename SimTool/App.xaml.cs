using System;       // for StringComparison
using System.Linq;  // if you use e.Args.Any(...)
using SimTools.Services; // so we can write DriverBootstrap directly
using SimTools.Views;
using System.Windows;

namespace SimTools
{
    public partial class App : Application
    {
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

            new MainWindow().Show();
        }
    }
}
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace SimTools.Services
{
    public static class VJoyBootstrap
    {
        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static string FindVJoyConfExe()
        {
            string[] candidates =
            {
                @"C:\Program Files\vJoy\vJoyConf.exe",
                @"C:\Program Files (x86)\vJoy\vJoyConf.exe"
            };
            foreach (var c in candidates)
                if (File.Exists(c))
                    return c;
            return "vJoyConf.exe";
        }

        /// <summary>
        /// Opens vJoyConf and tells the user how to enable Device 1 with 128 buttons.
        /// Call this if acquisition fails or device isn't configured yet.
        /// </summary>
        public static void EnsureConfigured()
        {
            MessageBox.Show(
                "vJoy Device #1 isn't configured.\n\n" +
                "When vJoyConf opens:\n" +
                "  1) Select Device 1\n" +
                "  2) Tick 'Enable vJoy'\n" +
                "  3) Set 'Number of Buttons' = 128\n" +
                "  4) Click Apply\n\n" +
                "Then relaunch this app.",
                "SimTools — Configure vJoy",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            OpenVJoyConfigurator();
        }

        public static void OpenVJoyConfigurator()
        {
            try
            {
                var exe = FindVJoyConfExe();
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    Verb = IsAdministrator() ? "" : "runas"
                };
                Process.Start(psi);
            }
            catch { /* ignore */ }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;   // Registry

#if HIDHIDE_API
using Nefarius.Drivers.HidHide; // optional, only if you add the NuGet and define HIDHIDE_API
#endif

namespace SimTools.Services
{
    /// <summary>
    /// Minimal bootstrap: detect/install ViGEm + HidHide from a local "Drivers" folder.
    /// Supports MSI or EXE installers. Optional HidHide whitelist via HIDHIDE_API.
    /// </summary>
    public static class DriverBootstrap
    {
        private const string DriversFolderRelative = "Drivers";

        /// <summary>
        /// Call once (startup or when enabling Game Mode).
        /// Prompts and installs missing components from Drivers\*, then whitelists app in HidHide (if present).
        /// </summary>
        public static async Task EnsureAllAsync()
        {
            // 1) ViGEm (required)
            if (!IsViGEmInstalled())
            {
                if (!PromptYes("SimTools needs the ViGEm Bus driver (one-time install). Install now?"))
                    return;

                if (!await TryInstallFromFolderAsync(forViGEm: true))
                {
                    MessageBox.Show("ViGEm install didn’t complete. You can retry from Settings.", "SimTools",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!IsViGEmInstalled())
                {
                    MessageBox.Show("ViGEm still not available after install. Please reboot or install manually.",
                        "SimTools", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // 2) HidHide (optional but recommended)
            if (!IsHidHideInstalled())
            {
                if (PromptYes("Install HidHide to prevent double inputs (hide real devices from games)?"))
                {
                    await TryInstallFromFolderAsync(forViGEm: false);
                    await Task.Delay(800); // let the service initialize
                }
            }

            // 3) Whitelist our EXE in HidHide (if present)
            if (IsHidHideInstalled())
            {
                try { EnsureHidHideWhitelist(); }
                catch { OpenHidHideConfig(); }
            }
        }

        // ---- Convenience wrappers (so your App.xaml.cs compiles) ----
        public static bool IsHidHideInstalled() => IsServiceInstalled("HidHide");
        public static bool IsViGEmInstalled() => IsServiceInstalled("ViGEmBus");

        // ---- Detection via Registry (no System.ServiceProcess reference needed) ----
        public static bool IsServiceInstalled(string name)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                return key != null;
        }

        private static bool PromptYes(string msg)
            => MessageBox.Show(msg, "SimTools", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;

        // ---- Installers (from Drivers\) ----
        private static async Task<bool> TryInstallFromFolderAsync(bool forViGEm)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var folder = Path.Combine(baseDir, DriversFolderRelative);
            if (!Directory.Exists(folder))
            {
                MessageBox.Show($"Missing folder: {folder}\nPlace the installers there and try again.",
                    "SimTools", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryFindInstaller(folder, forViGEm, out var path, out var isMsi))
            {
                MessageBox.Show($"Couldn’t find an installer for {(forViGEm ? "ViGEm" : "HidHide")} in:\n{folder}",
                    "SimTools", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return await RunInstallerAsync(path, isMsi);
        }

        private static bool TryFindInstaller(string folder, bool forViGEm, out string path, out bool isMsi)
        {
            path = null; isMsi = false;
            bool is64 = Environment.Is64BitOperatingSystem;

            string[] tokens = forViGEm
                ? new[] { "ViGEm", "ViGEmBus", "ViGEmBusSetup" }
                : new[] { "HidHide" };

            string[] exts = { "*.msi", "*.exe" };
            string[] archFirst = is64 ? new[] { "x64", "amd64", "win64" } : new[] { "x86", "win32" };

            // Prefer arch-specific names
            foreach (var token in tokens)
                foreach (var ext in exts)
                    foreach (var arch in archFirst)
                    {
                        var match = Directory.GetFiles(folder, $"{token}*{arch}*{ext}", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (!string.IsNullOrEmpty(match))
                        {
                            path = match;
                            isMsi = Path.GetExtension(match).Equals(".msi", StringComparison.OrdinalIgnoreCase);
                            return true;
                        }
                    }

            // Fallback: any matching file
            foreach (var token in tokens)
                foreach (var ext in exts)
                {
                    var match = Directory.GetFiles(folder, $"{token}*{ext}", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (!string.IsNullOrEmpty(match))
                    {
                        path = match;
                        isMsi = Path.GetExtension(match).Equals(".msi", StringComparison.OrdinalIgnoreCase);
                        return true;
                    }
                }

            return false;
        }

        private static async Task<bool> RunInstallerAsync(string path, bool isMsi)
        {
            try
            {
                var psi = isMsi
                    ? new ProcessStartInfo("msiexec.exe", $"/i \"{path}\" /passive /norestart")
                    : new ProcessStartInfo(path, "/quiet /norestart");

                psi.UseShellExecute = true;
                psi.Verb = "runas"; // elevate
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    await Task.Run(() => p.WaitForExit(10 * 60 * 1000)); // 10 min max
                    return p.ExitCode == 0 || p.ExitCode == 3010;        // MSI 3010 = reboot needed
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start installer:\n" + ex.Message, "SimTools",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ---- HidHide whitelist (optional API) ----
        public static void EnsureHidHideWhitelist()
        {
#if HIDHIDE_API
            using (var svc = new HidHideControlService())
            {
                svc.IsActive = true; // idempotent
                var me = HidHideControlService.GetProcessExecutablePath();
                var wl = svc.Whitelist?.ToList() ?? new System.Collections.Generic.List<string>(1);
                if (!wl.Contains(me, StringComparer.OrdinalIgnoreCase))
                {
                    wl.Add(me);
                    svc.Whitelist = wl;
                }
            }
#else
            // If the API isn't compiled in, open the config app so the user can add SimTools manually.
            OpenHidHideConfig();
#endif
        }

        public static bool OpenHidHideConfig()
        {
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var exe = Path.Combine(pf, "Nefarius Software Solutions", "HidHide", "HidHide Configuration Client.exe");
                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}

// Services/DriverBootstrap.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

#if HIDHIDE_API
using Nefarius.Drivers.HidHide; // if you keep the NuGet + define
#endif

namespace SimTools.Services
{
    /// <summary>
    /// Detect/install vJoy + HidHide from local "Drivers" folder and optionally whitelist our EXE in HidHide.
    /// </summary>
    public static class DriverBootstrap
    {
        private const string DriversFolderRelative = "Drivers";

        public static async Task EnsureAllAsync()
        {
            // 1) vJoy (required)
            if(!IsVJoyInstalled())
            {
                if(!PromptYes("SimTools needs the vJoy driver (one-time install). Install now?"))
                    return;

                if(!await TryInstallFromFolderAsync(forVJoy: true))
                {
                    MessageBox.Show("vJoy install didn’t complete. You can retry from Settings.", "SimTools",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if(!IsVJoyInstalled())
                {
                    MessageBox.Show("vJoy still not available after install. Please reboot or install manually.",
                        "SimTools", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // 2) HidHide (optional but recommended)
            if(!IsHidHideInstalled())
            {
                if(PromptYes("Install HidHide to prevent double inputs (hide real devices from games)?"))
                {
                    await TryInstallFromFolderAsync(forVJoy: false);
                    await Task.Delay(800);
                }
            }

            // 3) Whitelist our EXE in HidHide (if present)
            if(IsHidHideInstalled())
            {
                try { EnsureHidHideWhitelist(); }
                catch { OpenHidHideConfig(); }
            }
        }

        public static bool IsHidHideInstalled() => IsServiceInstalled("HidHide");
        public static bool IsVJoyInstalled() => IsServiceInstalled("vjoy");  // vJoy service name

        public static bool IsServiceInstalled(string name)
        {
            using(var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                return key != null;
        }

        private static bool PromptYes(string msg)
            => MessageBox.Show(msg, "SimTools", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;

        private static async Task<bool> TryInstallFromFolderAsync(bool forVJoy)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var folder = Path.Combine(baseDir, DriversFolderRelative);
            if(!Directory.Exists(folder))
            {
                MessageBox.Show($"Missing folder: {folder}\nPlace the installers there and try again.",
                    "SimTools", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if(!TryFindInstaller(folder, forVJoy, out var path, out var isMsi))
            {
                MessageBox.Show($"Couldn’t find an installer for {(forVJoy ? "vJoy" : "HidHide")} in:\n{folder}",
                    "SimTools", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return await RunInstallerAsync(path, isMsi);
        }

        private static bool TryFindInstaller(string folder, bool forVJoy, out string path, out bool isMsi)
        {
            path = null; isMsi = false;
            bool is64 = Environment.Is64BitOperatingSystem;

            string[] tokens = forVJoy
                ? new[] { "vJoy", "vJoySetup" }
                : new[] { "HidHide" };

            string[] exts = { "*.msi", "*.exe" };
            string[] archFirst = is64 ? new[] { "x64", "amd64", "win64" } : new[] { "x86", "win32" };

            foreach(var token in tokens)
                foreach(var ext in exts)
                    foreach(var arch in archFirst)
                    {
                        var match = Directory.GetFiles(folder, $"{token}*{arch}*{ext}", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if(!string.IsNullOrEmpty(match))
                        {
                            path = match;
                            isMsi = Path.GetExtension(match).Equals(".msi", StringComparison.OrdinalIgnoreCase);
                            return true;
                        }
                    }

            // fallback: first matching file
            foreach(var token in tokens)
                foreach(var ext in exts)
                {
                    var match = Directory.GetFiles(folder, $"{token}*{ext}", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if(!string.IsNullOrEmpty(match))
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
                    ? new ProcessStartInfo("msiexec.exe", $"/i \"{path}\" /qn")
                    : new ProcessStartInfo(path, "/quiet /norestart");

                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                var p = Process.Start(psi);
                await Task.Run(() => p.WaitForExit());
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        public static void EnsureHidHideWhitelist()
        {
#if HIDHIDE_API
            var path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            using (var api = new HidHideControlService())
            {
                var list = api.ApplicationPaths;
                if (!list.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(path);
                    api.ApplicationPaths = list;
                }
                api.IsActive = true;
            }
#else
            // If API not referenced, just try to open the config UI so user can do it
            OpenHidHideConfig();
#endif
        }

        private static void OpenHidHideConfig()
        {
            try { Process.Start(new ProcessStartInfo { FileName = "HidHideCfg.exe", UseShellExecute = true }); } catch { }
        }
    }
}

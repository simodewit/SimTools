// Services/DriverBootstrap.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

#if HIDHIDE_API
using Nefarius.Drivers.HidHide;
#endif

namespace SimTools.Services
{
    /// <summary>
    /// Silent bootstrap: detect/install vJoy + HidHide if installers are present.
    /// No prompts, no MessageBoxes, no config windows.
    /// Looks in:
    ///  - the application base folder (e.g., bin\Release next to the EXE)
    ///  - and an optional "Drivers" subfolder.
    /// </summary>
    public static class DriverBootstrap
    {
        private const string DriversFolderRelative = "Drivers";

        public static async Task EnsureAllAsync()
        {
            // vJoy (required for virtual joystick). If not installed and installer is present, try silent install.
            if (!IsVJoyInstalled())
                await TryInstallAsync(forVJoy: true);

            // HidHide (optional). If not installed and installer is present, try silent install.
            if (!IsHidHideInstalled())
                await TryInstallAsync(forVJoy: false);

            // Whitelist our EXE in HidHide if available. Silent; no UI fallback.
            if (IsHidHideInstalled())
            {
                try { EnsureHidHideWhitelist(); } catch { /* silent */ }
            }
        }

        public static bool IsHidHideInstalled() => IsServiceInstalled("HidHide");
        public static bool IsVJoyInstalled() => IsServiceInstalled("vjoy");

        public static bool IsServiceInstalled(string name)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                return key != null;
        }

        /// <summary>
        /// Silent whitelisting; if HidHide API is not referenced, this is a no-op (no UI).
        /// </summary>
        public static void EnsureHidHideWhitelist()
        {
#if HIDHIDE_API
            var path = Process.GetCurrentProcess().MainModule.FileName;
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
            // No UI in silent mode; do nothing if API not available.
#endif
        }

        // ---------------- Installer search & run (silent) ----------------

        private static async Task<bool> TryInstallAsync(bool forVJoy)
        {
            var roots = GetSearchRoots();

            if (!TryFindInstaller(roots, forVJoy, out var path, out var isMsi))
                return false; // no UI, just skip

            return await RunInstallerAsync(path, isMsi);
        }

        private static List<string> GetSearchRoots()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory; // ...\bin\Release\
            var roots = new List<string> { baseDir };

            var drivers = Path.Combine(baseDir, DriversFolderRelative);
            if (Directory.Exists(drivers))
                roots.Add(drivers);

            return roots;
        }

        private static bool TryFindInstaller(IEnumerable<string> roots, bool forVJoy, out string path, out bool isMsi)
        {
            path = null; isMsi = false;

            var tokens = forVJoy
                ? new[] { "vjoy", "vjoysetup" }
                : new[] { "hidhide" };

            var exts = new[] { ".msi", ".exe" };
            bool is64 = Environment.Is64BitOperatingSystem;

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;

                var candidates = Directory.EnumerateFiles(root, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Where(f =>
                    {
                        var name = Path.GetFileName(f);
                        return tokens.Any(t => name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
                    })
                    .Select(f => new
                    {
                        Path = f,
                        IsMsi = Path.GetExtension(f).Equals(".msi", StringComparison.OrdinalIgnoreCase),
                        Score = ArchScore(Path.GetFileName(f), is64) + ExtScore(Path.GetExtension(f))
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var pick = candidates.FirstOrDefault();
                if (pick != null)
                {
                    path = pick.Path;
                    isMsi = pick.IsMsi;
                    return true;
                }
            }

            return false;

            static int ArchScore(string fileName, bool is64Os)
            {
                fileName = fileName.ToLowerInvariant();
                var has64 = fileName.Contains("x64") || fileName.Contains("amd64") || fileName.Contains("64");
                var has86 = fileName.Contains("x86") || fileName.Contains("win32") || fileName.Contains("32");
                if (is64Os && has64) return 20;
                if (!is64Os && has86) return 20;
                if (has64 || has86) return 10;
                return 0;
            }

            static int ExtScore(string ext) => ext.Equals(".msi", StringComparison.OrdinalIgnoreCase) ? 5 : 0;
        }

        private static async Task<bool> RunInstallerAsync(string path, bool isMsi)
        {
            try
            {
                if (isMsi)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{path}\" /qn /norestart",
                        UseShellExecute = true,
                        Verb = "runas", // UAC (cannot be silenced programmatically)
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var p = Process.Start(psi))
                    {
                        await Task.Run(() => p.WaitForExit());
                        return p.ExitCode == 0;
                    }
                }
                else
                {
                    // Try common silent flags; if none succeed, we still remain silent (no UI).
                    var argSets = new[]
                    {
                        "/quiet /norestart",
                        "/SILENT",
                        "/VERYSILENT"
                        // No interactive fallback here (to stay 100% silent)
                    };

                    foreach (var args in argSets)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = path,
                                Arguments = args,
                                UseShellExecute = true,
                                Verb = "runas", // UAC prompt is OS-level
                                WindowStyle = ProcessWindowStyle.Hidden
                            };

                            using (var p = Process.Start(psi))
                            {
                                await Task.Run(() => p.WaitForExit());
                                if (p.ExitCode == 0) return true;
                            }
                        }
                        catch
                        {
                            // Try next argument set; stay silent
                            continue;
                        }
                    }

                    return false;
                }
            }
            catch
            {
                // Silent failure
                return false;
            }
        }
    }
}

// Services/VJoyBootstrap.cs
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using vJoyInterfaceWrap;

namespace SimTools.Services
{
    internal static class VJoyBootstrap
    {
        // Default x64 path; if you support x86 builds, mirror logic as needed.
        private static readonly string[] CandidateConfigPaths = new[]
        {
            // Typical x64 installs
            @"C:\Program Files\vJoy\x64\vJoyConfig.exe",
            // Fallbacks (older installers / custom)
            @"C:\Program Files (x86)\vJoy\x64\vJoyConfig.exe",
            @"C:\Program Files (x86)\vJoy\vJoyConfig.exe",
            @"C:\Program Files\vJoy\vJoyConfig.exe"
        };

        public static void EnsureConfigured(uint deviceId, int requiredButtons)
        {
            var vj = new vJoy();
            if(!vj.vJoyEnabled())
                throw new InvalidOperationException("vJoy driver not detected. Please install the vJoy driver first.");

            // If device exists and already has enough buttons, we're good.
            var status = vj.GetVJDStatus(deviceId);
            var buttons = vj.GetVJDButtonNumber(deviceId);
            if(status != VjdStat.VJD_STAT_MISS && buttons >= requiredButtons)
                return;

            // Find vJoyConfig.exe
            var cfg = FindConfigExe();
            if(cfg == null)
                throw new InvalidOperationException("vJoyConfig.exe not found. Please reinstall vJoy or adjust the path.");

            // Create/ensure device with requiredButtons (no axes/POVs here)
            // Example: "1 -b 128"
            var args = $"{deviceId} -b {requiredButtons}";

            var psi = new ProcessStartInfo
            {
                FileName = cfg,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",               // UAC elevate once
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var p = Process.Start(psi);
            p.WaitForExit();

            if(p.ExitCode != 0)
                throw new InvalidOperationException($"vJoy configuration failed (exit code {p.ExitCode}).");

            // Optional: re-check
            status = vj.GetVJDStatus(deviceId);
            buttons = vj.GetVJDButtonNumber(deviceId);
            if(status == VjdStat.VJD_STAT_MISS || buttons < requiredButtons)
                throw new InvalidOperationException("vJoy device could not be prepared. Please run vJoyConfig manually.");
        }

        private static string? FindConfigExe()
        {
            foreach(var p in CandidateConfigPaths)
                if(File.Exists(p)) return p;

            // Try walking from Program Files if needed
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach(var root in roots)
            {
                var guess = Path.Combine(root, "vJoy");
                if(Directory.Exists(guess))
                {
                    var match = Directory.GetFiles(guess, "vJoyConfig.exe", SearchOption.AllDirectories);
                    if(match.Length > 0) return match[0];
                }
            }
            return null;
        }
    }
}

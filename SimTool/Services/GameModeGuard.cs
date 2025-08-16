using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;

namespace SimTools.Services
{
    /// <summary>
    /// Simple foreground-process guard: when a denylisted process is active,
    /// we suspend outputs. In "Game Mode" we also avoid hooks/injection entirely.
    /// </summary>
    public sealed class GameModeGuard : IDisposable
    {
        private readonly Timer _poll;
        private string _currentExe;

        // Set this true for anti-cheat friendly behavior.
        public bool GameModeEnabled { get; set; } = true;

        // Processes for which we suspend all outputs (edit/add per your library).
        public HashSet<string> Denylist { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Strong anti-cheat or competitive shooters (super safe defaults)
            "valorant.exe","vgc.exe","vgk.exe","fortniteclient-win64-shipping.exe",
            "riotclientservices.exe","easyanticheat_launcher.exe",

            // Add titles here ONLY if you find they dislike virtual devices.
            // "f1_24.exe",
            // "acs.exe", "acc.exe", "ac2-win64-shipping.exe",
            // "iracingsim64dx11.exe",
            // "rfactor2.exe", "rF2.exe",
            // "ams2avx.exe"
        };

        public bool IsSuspended { get; private set; }
        public string SuspendedBy { get; private set; }

        public event Action<bool, string> StatusChanged;

        public GameModeGuard()
        {
            _poll = new Timer(400);
            _poll.AutoReset = true;
            _poll.Elapsed += (_, __) => Tick();
        }

        public void Start() { _poll.Start(); Tick(); }
        public void Stop() { _poll.Stop(); }
        public void Dispose() { Stop(); _poll.Dispose(); }

        public bool ShouldOperateNow()
            => GameModeEnabled && !IsSuspended;

        private void Tick()
        {
            try
            {
                var exe = GetForegroundProcessExe();
                if (exe != _currentExe)
                {
                    _currentExe = exe;
                    var suspended = exe != null && Denylist.Contains(exe);
                    if (suspended != IsSuspended)
                    {
                        IsSuspended = suspended;
                        SuspendedBy = suspended ? exe : null;
                        StatusChanged?.Invoke(IsSuspended, SuspendedBy);
                    }
                }
            }
            catch { /* never throw from poller */ }
        }

        private static string GetForegroundProcessExe()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return null;

            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    var name = (p.MainModule?.FileName ?? p.ProcessName) ?? "";
                    return System.IO.Path.GetFileName(name).ToLowerInvariant();
                }
            }
            catch { return null; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}

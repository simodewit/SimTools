using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SimTools.Services
{
    /// <summary>
    /// Global low-level keyboard hook. Calls back with Key/Modifiers and allows swallowing.
    /// NOTE: Run the app elevated if you need to block keys for elevated processes/games.
    /// </summary>
    public sealed class InputBlockerService : IDisposable
    {
        public delegate bool BlockPredicate(Key key, ModifierKeys mods, bool isDown);

        private readonly BlockPredicate _predicate;
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelProc _proc;

        public InputBlockerService(BlockPredicate predicate) { _predicate = predicate; }

        public void Start()
        {
            if (_hook != IntPtr.Zero) return;
            _proc = HookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        public void Dispose() => Stop();

        // ---- Win32 interop ----
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public KbdLlFlags flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [Flags]
        private enum KbdLlFlags : uint
        {
            LLKHF_EXTENDED = 0x01,
            LLKHF_INJECTED = 0x10,
            LLKHF_ALTDOWN = 0x20,
            LLKHF_UP = 0x80
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static ModifierKeys GetModifiers()
        {
            ModifierKeys mods = 0;
            if ((GetKeyState(0x11) & 0x8000) != 0) mods |= ModifierKeys.Control; // VK_CONTROL
            if ((GetKeyState(0x12) & 0x8000) != 0) mods |= ModifierKeys.Alt;     // VK_MENU
            if ((GetKeyState(0x10) & 0x8000) != 0) mods |= ModifierKeys.Shift;   // VK_SHIFT
            if ((GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0) mods |= ModifierKeys.Windows; // L/R Win
            return mods;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN || (msg != WM_KEYUP && msg != WM_SYSKEYUP);

                var data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                // Ignore injected keyboard events so we don't fight with ourselves (not strictly needed here).
                if ((data.flags & KbdLlFlags.LLKHF_INJECTED) != 0)
                    return CallNextHookEx(_hook, nCode, wParam, lParam);

                var key = KeyInterop.KeyFromVirtualKey((int)data.vkCode);
                var mods = GetModifiers();

                if (_predicate != null && _predicate.Invoke(key, mods, isDown))
                    return (IntPtr)1; // swallow
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}

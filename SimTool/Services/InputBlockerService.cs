using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using SimTools.Debug;
using SimTools.Helpers; // for KeybindHelpers.BuildKeyboardLabel

namespace SimTools.Services
{
    /// <summary>
    /// Global low-level keyboard hook that can swallow key presses based on a predicate.
    /// When swallowing a key on key-down, we also route a synthetic input into the normal pipeline
    /// so vJoy/highlight still fire even though WM_INPUT won't arrive.
    /// </summary>
    public sealed class InputBlockerService : IDisposable
    {
        public delegate bool BlockPredicate(Key key, ModifierKeys mods, bool isDown);

        private readonly BlockPredicate _predicate;
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelProc _proc;

        public InputBlockerService(BlockPredicate predicate)
        {
            _predicate = predicate ?? ((k, m, d) => false);
        }

        public void Start()
        {
            if(_hook != IntPtr.Zero) return;
            _proc = HookCallback;
            using(var curProcess = Process.GetCurrentProcess())
            using(var curModule = curProcess.MainModule)
            {
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
            Diag.Log($"HOOK.Start: WH_KEYBOARD_LL installed -> {_hook != IntPtr.Zero}");
        }

        public void Stop()
        {
            if(_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                Diag.Log("HOOK.Stop: unhooked");
                _hook = IntPtr.Zero;
            }
        }

        public void Dispose() => Stop();

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if(nCode >= 0)
            {
                bool isDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

                if(isDown || isUp)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var injected = (data.flags & KbdLlFlags.LLKHF_INJECTED) != 0;
                    var key = KeyInterop.KeyFromVirtualKey((int)data.vkCode);
                    var mods = GetModifiers();

                    bool swallow = false;
                    try
                    {
                        swallow = _predicate?.Invoke(key, mods, isDown) == true;
                    }
                    catch(Exception ex)
                    {
                        Diag.LogEx("HOOK predicate", ex);
                    }

                    Diag.Log($"HOOK {(isDown ? "DOWN" : "UP  ")} vk=0x{data.vkCode:X} sc=0x{data.scanCode:X} injected={injected} mods={mods} swallow={swallow}");

                    // Never swallow injected events (our own SendInput / test taps)
                    if(injected) swallow = false;

                    if(swallow)
                    {
                        // When we swallow on key-down, the OS won't raise WM_INPUT.
                        // So synthesize the same event label and route it into the normal pipeline.
                        if(isDown)
                        {
                            try
                            {
                                var label = KeybindHelpers.BuildKeyboardLabel(mods, key);
                                if(string.IsNullOrEmpty(label))
                                    label = "VK_" + ((int)data.vkCode).ToString("X2");

                                Diag.Log($"HOOK ROUTE: swallow=True -> '{label}'");
                                // Push into InputCapture; it will marshal to UI thread and then into RawInputMonitor
                                InputCapture.RouteFromHook("Keyboard", label);
                            }
                            catch(Exception ex)
                            {
                                Diag.LogEx("HOOK.RouteFromHook", ex);
                            }
                        }

                        // Block the original event system-wide
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static ModifierKeys GetModifiers()
        {
            ModifierKeys mods = ModifierKeys.None;
            if(IsKeyDown(VK_SHIFT)) mods |= ModifierKeys.Shift;
            if(IsKeyDown(VK_CONTROL)) mods |= ModifierKeys.Control;
            if(IsKeyDown(VK_MENU)) mods |= ModifierKeys.Alt;
            if(IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN)) mods |= ModifierKeys.Windows;
            return mods;
        }

        private static bool IsKeyDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

        // P/Invoke

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

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}

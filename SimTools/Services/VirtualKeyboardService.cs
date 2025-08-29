using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SimTools.Services
{
    /// <summary>
    /// Minimal keyboard sender using SendInput with scan codes.
    /// </summary>
    public sealed class VirtualKeyboardService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_SCANCODE = 0x0008;
        const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public void Tap(ushort scan, bool ctrl = false, bool alt = false, bool shift = false, int downMs = 35)
        {
            // Modifiers down (order: Ctrl, Alt, Shift)
            if (ctrl) KeyDown(0x1D);   // LCtrl scancode
            if (alt) KeyDown(0x38);   // LAlt
            if (shift) KeyDown(0x2A);   // LShift

            KeyDown(scan);
            Thread.Sleep(downMs);
            KeyUp(scan);

            // Modifiers up (reverse order)
            if (shift) KeyUp(0x2A);
            if (alt) KeyUp(0x38);
            if (ctrl) KeyUp(0x1D);
        }

        private static void KeyDown(ushort scan) => Send(scan, false);
        private static void KeyUp(ushort scan) => Send(scan, true);

        private static void Send(ushort scan, bool up)
        {
            var inp = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}

// Services/KeyboardOutputService.cs
using System;
using System.Runtime.InteropServices;

namespace SimTools.Services
{
    /// <summary>
    /// Sends keyboard events via Win32 SendInput using scan codes (layout-safe).
    /// </summary>
    public sealed class KeyboardOutputService
    {
        public void SendScan(ushort scanCode, bool isExtended, bool isKeyDown)
        {
            var input = new INPUT
            {
                type = 1, // INPUT_KEYBOARD
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = KEYEVENTF_SCANCODE | (isExtended ? KEYEVENTF_EXTENDEDKEY : 0) | (isKeyDown ? 0 : KEYEVENTF_KEYUP),
                        dwExtraInfo = IntPtr.Zero,
                        time = 0
                    }
                }
            };

            var arr = new[] { input };
            SendInput(1u, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        // Win32 interop
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}

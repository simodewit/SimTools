#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SimTools.Models;
using SimTools.Helpers; 

namespace SimTools.Services
{
    public static class InputCapture
    {
        public static IDisposable StartMonitor(Window owner, Action<InputBindingResult> onInput)
        {
            if(owner == null) throw new ArgumentNullException(nameof(owner));
            if(onInput == null) throw new ArgumentNullException(nameof(onInput));

            var rim = new RawInputMonitor(owner);

            Action<InputBindingResult> forward = null;
            forward = (res) =>
            {
                if(res == null)
                {
                    return;
                }
                try { onInput(res); }
                catch(Exception ex) { }
            };

            rim.InputReceived += forward;

            return new RimHandle(rim, forward);
        }

        /// <summary>
        /// Capture a single keyboard MAKE, synchronously. Used by your binding UI.
        /// </summary>
        public static InputBindingResult CaptureBinding(Window owner)
        {
            InputBindingResult captured = null;

            var rim = new RawInputMonitor(owner);
            Action<InputBindingResult> handler = null;

            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
            EventHandler timeCb = null;
            timeCb = (s, e) =>
            {
                try { timeout.Stop(); } catch { }
                frame.Continue = false;
            };

            handler = (res) =>
            {
                if(res == null) return;
                captured = res;
                try { rim.InputReceived -= handler; } catch { }
                try { rim.Dispose(); } catch { }
                try { timeout.Stop(); } catch { }
                frame.Continue = false;
            };

            rim.InputReceived += handler;
            timeout.Tick += timeCb;
            timeout.Start();

            try { Dispatcher.PushFrame(frame); }
            finally
            {
                try { timeout.Tick -= timeCb; } catch { }
                try { rim.InputReceived -= handler; } catch { }
                try { rim.Dispose(); } catch { }
            }

            return captured;
        }

        /// <summary>
        /// Parse WM_INPUT (lParam) to InputBindingResult, or null if not a MAKE keyboard event.
        /// </summary>
        public static InputBindingResult ReadInput(IntPtr lParam)
        {
            try
            {
                uint size = 0;
                uint want0 = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                if(want0 != 0 || size == 0)
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint read = GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                    if(read == 0)
                    {
                        return null;
                    }

                    var raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                    if(raw.header.dwType != RIM_TYPEKEYBOARD)
                    {
                        return null;
                    }

                    var k = raw.data.keyboard;

                    if(k.VKey == 0xFF)
                    {
                        return null;
                    }

                    bool isBreak = (k.Flags & RI_KEY_BREAK) != 0;
                    if(isBreak)
                    {
                        return null;
                    }

                    int vk = k.VKey;
                    Key key = Key.None;
                    try { key = KeyInterop.KeyFromVirtualKey(vk); }
                    catch { key = Key.None; }

                    var mods = GetCurrentModifiers();
                    string label = KeybindHelpers.BuildKeyboardLabel(mods, key);

                    if(key == Key.None || string.IsNullOrEmpty(label))
                    {
                        var fb = VkToFallbackLabel(vk, mods);
                        label = fb;
                    }
                    else
                    {
                    }

                    var result = new InputBindingResult
                    {
                        DeviceType = "Keyboard",
                        ControlLabel = label
                    };

                    return result;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        // ---------------------- Internals ----------------------

        private sealed class RimHandle : IDisposable
        {
            private RawInputMonitor _rim;
            private Action<InputBindingResult> _cb;
            public RimHandle(RawInputMonitor rim, Action<InputBindingResult> cb) { _rim = rim; _cb = cb; }
            public void Dispose()
            {
                if(_rim != null)
                {
                    try { _rim.InputReceived -= _cb; } catch { }
                    try { _rim.Dispose(); } catch { }
                }
                _rim = null; _cb = null;
            }
        }

        private static ModifierKeys GetCurrentModifiers()
        {
            ModifierKeys mods = ModifierKeys.None;
            if((GetKeyState(VK_CONTROL) & 0x8000) != 0) mods |= ModifierKeys.Control;
            if((GetKeyState(VK_MENU) & 0x8000) != 0) mods |= ModifierKeys.Alt;
            if((GetKeyState(VK_SHIFT) & 0x8000) != 0) mods |= ModifierKeys.Shift;
            if((GetKeyState(VK_LWIN) & 0x8000) != 0) mods |= ModifierKeys.Windows;
            if((GetKeyState(VK_RWIN) & 0x8000) != 0) mods |= ModifierKeys.Windows;
            return mods;
        }

        private static string VkToFallbackLabel(int vk, ModifierKeys mods)
        {
            string core;
            if(vk == 0x20) core = "Space";
            else if(vk == 0x0D) core = "Enter";
            else if(vk == 0x1B) core = "Esc";
            else if(vk == 0x08) core = "Backspace";
            else if(vk >= 0x30 && vk <= 0x39) core = ((char)vk).ToString(); // 0-9
            else if(vk >= 0x41 && vk <= 0x5A) core = ((char)vk).ToString(); // A-Z
            else core = "VK_" + vk.ToString("X2");

            string prefix = "";
            if((mods & ModifierKeys.Control) != 0) prefix += "Ctrl + ";
            if((mods & ModifierKeys.Alt) != 0) prefix += "Alt + ";
            if((mods & ModifierKeys.Shift) != 0) prefix += "Shift + ";
            if((mods & ModifierKeys.Windows) != 0) prefix += "Win + ";
            return prefix + core;
        }

        // -------- Win32 --------

        private const uint RID_INPUT = 0x10000003;
        private const int RIM_TYPEKEYBOARD = 1;

        private const ushort RI_KEY_BREAK = 0x01;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")] private static extern short GetKeyState(int nVirtKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE { public ushort usFlags; public uint ulButtons; public ushort usButtonFlags; public ushort usButtonData; public uint ulRawButtons; public int lLastX; public int lLastY; public uint ulExtraInformation; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTUNION { [FieldOffset(0)] public RAWMOUSE mouse; [FieldOffset(0)] public RAWKEYBOARD keyboard; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT { public RAWINPUTHEADER header; public RAWINPUTUNION data; }

        // Add inside SimTools.Services.InputCapture (same class as before)
        public static void RouteFromHook(string device, string label)
        {
            try
            {
                // Marshal to UI thread so all your existing code paths & timers behave the same.
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var res = new InputBindingResult { DeviceType = device, ControlLabel = label };
                    RawInputMonitor.RouteSynthetic(res);
                }));
            }
            catch(Exception ex)
            {
            }
        }
    }
}

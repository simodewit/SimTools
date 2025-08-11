using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SimTools.Services
{
    public class InputBindingResult
    {
        public string DeviceType { get; set; }    // Keyboard / Mouse / HID
        public string DeviceName { get; set; }    // Friendly-ish name or VID:PID
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public string ControlLabel { get; set; }  // e.g., Enter / LeftButton / HID Input

        public override string ToString()
            => string.IsNullOrWhiteSpace(DeviceName) ? $"{DeviceType}: {ControlLabel}" : $"{DeviceName}: {ControlLabel}";
    }

    public static class InputCapture
    {
        public static InputBindingResult CaptureBinding(Window owner)
        {
            var dlg = new RawCaptureWindow { Owner = owner };
            bool? ok = dlg.ShowDialog();
            return ok == true ? dlg.Result : null;
        }

        private class RawCaptureWindow : Window
        {
            public InputBindingResult Result { get; private set; }

            private HwndSource _source;

            public RawCaptureWindow()
            {
                Title = "Listening…";
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                SizeToContent = SizeToContent.WidthAndHeight;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                ShowInTaskbar = false;

                var chrome = new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(38, 42, 48)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 76, 85)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(20),
                    MinWidth = 320
                };

                var title = new System.Windows.Controls.TextBlock
                {
                    Text = "Listening…",
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                var sub = new System.Windows.Controls.TextBlock
                {
                    Text = "Press any key / mouse button / device input",
                    Margin = new Thickness(0, 4, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 195))
                };

                var stack = new System.Windows.Controls.StackPanel();
                stack.Children.Add(title);
                stack.Children.Add(sub);
                chrome.Child = stack;

                Content = chrome;

                // Keyboard & mouse fallback (if Raw Input misses)
                PreviewKeyDown += (s, e) =>
                {
                    Result = new InputBindingResult
                    {
                        DeviceType = "Keyboard",
                        DeviceName = "Keyboard",
                        ControlLabel = FormatKey(e.Key)
                    };
                    DialogResult = true;
                };
                PreviewMouseDown += (s, e) =>
                {
                    Result = new InputBindingResult
                    {
                        DeviceType = "Mouse",
                        DeviceName = "Mouse",
                        ControlLabel = e.ChangedButton.ToString()
                    };
                    DialogResult = true;
                };

                // Drag the dialog by mouse
                chrome.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            }

            protected override void OnSourceInitialized(EventArgs e)
            {
                base.OnSourceInitialized(e);

                _source = (HwndSource)PresentationSource.FromVisual(this);
                _source.AddHook(WndProc);

                // Register for RAWINPUT for keyboard, mouse, and generic HID
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[]
                {
                    new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = 0, hwndTarget = _source.Handle }, // Keyboard
                    new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = 0, hwndTarget = _source.Handle }, // Mouse
                    new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x00, dwFlags = 0, hwndTarget = _source.Handle }, // Generic Desktop (catch-all)
                };

                RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            }

            private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                const int WM_INPUT = 0x00FF;
                if (msg == WM_INPUT)
                {
                    ProcessRawInput(lParam);
                    if (Result != null)
                    {
                        handled = true;
                        DialogResult = true;
                    }
                }
                return IntPtr.Zero;
            }

            private void ProcessRawInput(IntPtr hRawInput)
            {
                uint dwSize = 0;
                GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                if (dwSize == 0) return;

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                    if (GetRawInputData(hRawInput, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) != dwSize)
                        return;

                    RAWINPUT raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                    if (raw.header.dwType == RIM_TYPEKEYBOARD)
                    {
                        if (raw.keyboard.Message == WM_KEYDOWN || raw.keyboard.Message == WM_SYSKEYDOWN)
                        {
                            var key = KeyInterop.KeyFromVirtualKey(raw.keyboard.VKey);
                            Result = new InputBindingResult
                            {
                                DeviceType = "Keyboard",
                                DeviceName = "Keyboard",
                                ControlLabel = FormatKey(key)
                            };
                        }
                    }
                    else if (raw.header.dwType == RIM_TYPEMOUSE)
                    {
                        ushort flags = raw.mouse.usButtonFlags;
                        string btn = null;
                        if ((flags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0) btn = "LeftButton";
                        else if ((flags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0) btn = "RightButton";
                        else if ((flags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0) btn = "MiddleButton";
                        else if ((flags & RI_MOUSE_BUTTON_4_DOWN) != 0) btn = "XButton1";
                        else if ((flags & RI_MOUSE_BUTTON_5_DOWN) != 0) btn = "XButton2";

                        if (!string.IsNullOrEmpty(btn))
                        {
                            Result = new InputBindingResult
                            {
                                DeviceType = "Mouse",
                                DeviceName = "Mouse",
                                ControlLabel = btn
                            };
                        }
                    }
                    else if (raw.header.dwType == RIM_TYPEHID)
                    {
                        GetDeviceInfo(raw.header.hDevice,
                                      out ushort vid, out ushort pid,
                                      out string name, out ushort usagePage, out ushort usage);

                        Result = new InputBindingResult
                        {
                            DeviceType = "HID",
                            DeviceName = !string.IsNullOrWhiteSpace(name) ? name : $"HID {vid:X4}:{pid:X4}",
                            VendorId = vid,
                            ProductId = pid,
                            ControlLabel = $"UsagePage {usagePage}, Usage {usage}"
                        };
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            private static string FormatKey(Key key)
            {
                if (key >= Key.F1 && key <= Key.F24) return key.ToString();
                if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num" + (key - Key.NumPad0);
                if (key == Key.Space) return "Space";
                if (key == Key.Return) return "Enter";
                if (key == Key.Escape) return "Esc";
                if (key == Key.LeftCtrl || key == Key.RightCtrl) return "Ctrl";
                if (key == Key.LeftAlt || key == Key.RightAlt) return "Alt";
                if (key == Key.LeftShift || key == Key.RightShift) return "Shift";
                return key.ToString();
            }

            private static void GetDeviceInfo(IntPtr hDevice,
                                              out ushort vid, out ushort pid,
                                              out string name, out ushort usagePage, out ushort usage)
            {
                vid = pid = usagePage = usage = 0;
                name = null;

                // Device path (often contains VID/PID)
                uint pcbSize = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
                if (pcbSize > 0)
                {
                    var pData = Marshal.AllocHGlobal((int)pcbSize);
                    try
                    {
                        if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pData, ref pcbSize) > 0)
                        {
                            // RIDI_DEVICENAME is a Unicode string
                            string devicePath = Marshal.PtrToStringUni(pData);
                            name = devicePath;

                            if (!string.IsNullOrEmpty(devicePath))
                            {
                                int vidIdx = devicePath.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
                                int pidIdx = devicePath.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
                                if (vidIdx >= 0 && vidIdx + 8 <= devicePath.Length)
                                {
                                    string vs = devicePath.Substring(vidIdx + 4, 4);
                                    ushort.TryParse(vs, System.Globalization.NumberStyles.HexNumber, null, out vid);
                                }
                                if (pidIdx >= 0 && pidIdx + 8 <= devicePath.Length)
                                {
                                    string ps = devicePath.Substring(pidIdx + 4, 4);
                                    ushort.TryParse(ps, System.Globalization.NumberStyles.HexNumber, null, out pid);
                                }
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(pData); }
                }

                // Device info (usage page/usage)
                uint size = (uint)Marshal.SizeOf(typeof(RID_DEVICE_INFO));
                var info = new RID_DEVICE_INFO { cbSize = size };
                if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, ref info, ref size) > 0)
                {
                    usagePage = info.u.hid.usUsagePage;
                    usage = info.u.hid.usUsage;
                }
            }

            #region P/Invoke

            private const int RID_INPUT = 0x10000003;

            private const int RIM_TYPEMOUSE = 0;
            private const int RIM_TYPEKEYBOARD = 1;
            private const int RIM_TYPEHID = 2;

            private const int WM_KEYDOWN = 0x0100;
            private const int WM_SYSKEYDOWN = 0x0104;

            private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
            private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0002;
            private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
            private const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
            private const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;

            private const uint RIDI_DEVICENAME = 0x20000007;
            private const uint RIDI_DEVICEINFO = 0x2000000b;

            [StructLayout(LayoutKind.Sequential)]
            private struct RAWINPUTDEVICE
            {
                public ushort usUsagePage;
                public ushort usUsage;
                public uint dwFlags;
                public IntPtr hwndTarget;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RAWINPUTHEADER
            {
                public int dwType;
                public int dwSize;
                public IntPtr hDevice;
                public IntPtr wParam;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RAWMOUSE
            {
                public ushort usFlags;
                public uint ulButtons;
                public ushort usButtonFlags;
                public ushort usButtonData;
                public uint ulRawButtons;
                public int lLastX;
                public int lLastY;
                public uint ulExtraInformation;
            }

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
            private struct RAWINPUT
            {
                [FieldOffset(0)] public RAWINPUTHEADER header;
                [FieldOffset(16)] public RAWMOUSE mouse;
                [FieldOffset(16)] public RAWKEYBOARD keyboard;
                // (HID data is opaque — we query details via device handle)
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RID_DEVICE_INFO_MOUSE
            {
                public uint dwId;
                public uint dwNumberOfButtons;
                public uint dwSampleRate;
                [MarshalAs(UnmanagedType.Bool)] public bool fHasHorizontalWheel;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RID_DEVICE_INFO_KEYBOARD
            {
                public uint dwType;
                public uint dwSubType;
                public uint dwKeyboardMode;
                public uint dwNumberOfFunctionKeys;
                public uint dwNumberOfIndicators;
                public uint dwNumberOfKeysTotal;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RID_DEVICE_INFO_HID
            {
                public uint dwVendorId;
                public uint dwProductId;
                public uint dwVersionNumber;
                public ushort usUsagePage;
                public ushort usUsage;
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct RID_DEVICE_INFO_UNION
            {
                [FieldOffset(0)] public RID_DEVICE_INFO_MOUSE mouse;
                [FieldOffset(0)] public RID_DEVICE_INFO_KEYBOARD keyboard;
                [FieldOffset(0)] public RID_DEVICE_INFO_HID hid;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RID_DEVICE_INFO
            {
                public uint cbSize;
                public uint dwType;
                public RID_DEVICE_INFO_UNION u; // <-- named union field
            }

            [DllImport("User32.dll", SetLastError = true)]
            private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

            [DllImport("User32.dll", SetLastError = true)]
            private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

            [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

            [DllImport("User32.dll", SetLastError = true)]
            private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, ref RID_DEVICE_INFO pData, ref uint pcbSize);

            #endregion
        }
    }
}

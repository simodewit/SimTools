// services/InputCapture.cs
#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SimTools.Models;
using static SimTools.Interop.RawInputNative;

namespace SimTools.Services
{
    /// <summary>
    /// High-level input capture API for the UI:
    /// - CaptureBinding: show dialog and return one pressed control
    /// - StartMonitor: listen in background and report inputs as they happen
    /// Uses RawInputNative for interop and RawInputMonitor for background listening.
    /// </summary>
    public static class InputCapture
    {
        /// <summary>Modal capture dialog – shows "Listening…" and returns the first captured input.</summary>
        public static InputBindingResult? CaptureBinding(Window owner)
        {
            // Uses your XAML dialog instead of a code-built window
            var dlg = new SimTools.Views.InputCaptureDialog { Owner = owner };
            bool? ok = dlg.ShowDialog();
            return ok == true ? dlg.Result : null;
        }

        /// <summary>Start a background Raw Input monitor and invoke onInput whenever an input is received.</summary>
        public static IDisposable StartMonitor(Window owner, Action<InputBindingResult> onInput)
        {
            var monitor = new RawInputMonitor(owner);
            monitor.InputReceived += onInput;
            return monitor;
        }

        // -------------------- Shared RAWINPUT parsing (kept here) --------------------
        internal static InputBindingResult? ReadInput(IntPtr hRawInput)
        {
            uint dwSize = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            if (dwSize == 0) return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) != dwSize)
                    return null;

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                if (raw.header.dwType == RIM_TYPEKEYBOARD)
                {
                    if (raw.keyboard.Message == WM_KEYDOWN || raw.keyboard.Message == WM_SYSKEYDOWN)
                    {
                        var key = KeyInterop.KeyFromVirtualKey(raw.keyboard.VKey);
                        return new InputBindingResult
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
                    string? btn = null;
                    if ((flags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0) btn = "LeftButton";
                    else if ((flags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0) btn = "RightButton";
                    else if ((flags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0) btn = "MiddleButton";
                    else if ((flags & RI_MOUSE_BUTTON_4_DOWN) != 0) btn = "XButton1";
                    else if ((flags & RI_MOUSE_BUTTON_5_DOWN) != 0) btn = "XButton2";

                    if (!string.IsNullOrEmpty(btn))
                    {
                        return new InputBindingResult
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
                                  out string? name, out ushort usagePage, out ushort usage);

                    return new InputBindingResult
                    {
                        DeviceType = "HID",
                        DeviceName = !string.IsNullOrWhiteSpace(name) ? name : $"HID {vid:X4}:{pid:X4}",
                        VendorId = vid,
                        ProductId = pid,
                        ControlLabel = $"UsagePage {usagePage}, Usage {usage}"
                    };
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        internal static string FormatKey(Key key)
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

        internal static void GetDeviceInfo(IntPtr hDevice, out ushort vid, out ushort pid, out string? name, out ushort usagePage, out ushort usage)
        {
            vid = pid = usagePage = usage = 0;
            name = null;

            // Device path (Unicode; often contains VID/PID)
            uint pcbSize = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
            if (pcbSize > 0)
            {
                var pData = Marshal.AllocHGlobal((int)pcbSize);
                try
                {
                    if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pData, ref pcbSize) > 0)
                    {
                        string devicePath = Marshal.PtrToStringUni(pData)!;
                        name = devicePath;

                        if (!string.IsNullOrEmpty(devicePath))
                        {
                            int vidIdx = devicePath.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
                            int pidIdx = devicePath.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
                            if (vidIdx >= 0 && vidIdx + 8 <= devicePath.Length)
                            {
                                string vs = devicePath.Substring(vidIdx + 4, 4);
                                ushort.TryParse(vs, System.Globalization.NumberStyles.HexNumber, provider: null, out vid);
                            }
                            if (pidIdx >= 0 && pidIdx + 8 <= devicePath.Length)
                            {
                                string ps = devicePath.Substring(pidIdx + 4, 4);
                                ushort.TryParse(ps, System.Globalization.NumberStyles.HexNumber, provider: null, out pid);
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
    }
}

// services/RawInputMonitor.cs
#nullable enable
using System;
using System.Windows;
using System.Windows.Interop;
using SimTools.Models;
using static SimTools.Interop.RawInputNative;

namespace SimTools.Services
{
    /// <summary>
    /// Listens for Raw Input in the background and raises InputReceived for each event.
    /// Dispose to stop listening.
    /// </summary>
    public sealed class RawInputMonitor : IDisposable
    {
        private readonly Window _owner;
        private HwndSource? _source;
        private bool _disposed;

        public event Action<InputBindingResult>? InputReceived;

        public RawInputMonitor(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            var src = (HwndSource?)PresentationSource.FromVisual(owner);
            if (src == null)
            {
                owner.SourceInitialized += OwnerOnSourceInitialized;
            }
            else
            {
                Init(src);
            }
        }

        private void OwnerOnSourceInitialized(object? sender, EventArgs e)
        {
            var src = (HwndSource?)PresentationSource.FromVisual(_owner);
            if (src != null) Init(src);
        }

        private void Init(HwndSource src)
        {
            _source = src;
            _source.AddHook(WndProc);

            // Register keyboard, mouse, and common HID classes as INPUTSINK
            RAWINPUTDEVICE[] rid =
            {
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Keyboard
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Mouse
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x04, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Joystick
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x05, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Gamepad
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x08, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Multi-axis
                new RAWINPUTDEVICE { usUsagePage = 0x02, usUsage = 0x00, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Simulation Controls
                new RAWINPUTDEVICE { usUsagePage = 0x0C, usUsage = 0x01, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Consumer Control
            };

            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                var res = InputCapture.ReadInput(lParam);
                if (res != null) InputReceived?.Invoke(res);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
            _owner.SourceInitialized -= OwnerOnSourceInitialized;
        }
    }
}

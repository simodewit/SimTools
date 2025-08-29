// views/InputCaptureDialog.xaml.cs
#nullable enable
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SimTools.Models;
using SimTools.Services;
using static SimTools.Interop.RawInputNative;

namespace SimTools.Views
{
    /// <summary>
    /// Small dialog that listens for the next input (keyboard/mouse/HID) and exposes it via Result.
    /// Backwards compatible: still sets Device/DeviceKey/Modifiers/Key/Accepted for existing callers.
    /// </summary>
    public partial class InputCaptureDialog : Window
    {
        // NEW API (used by InputCapture.CaptureBinding)
        public InputBindingResult? Result { get; private set; }

        // BACK-COMPAT public properties (do not remove; keep old callers working)
        public string Device { get; private set; } = "";       // "Keyboard" / "Mouse" / "HID"
        public string DeviceKey { get; private set; } = "";    // "Ctrl+K" / "Left Button" / "UsagePage 1, Usage 4"
        public ModifierKeys Modifiers { get; private set; }
        public Key Key { get; private set; }
        public bool Accepted { get; private set; }

        private HwndSource? _source;

        public InputCaptureDialog()
        {
            InitializeComponent();

            // Keep your original handlers for quick routed-event capture and Esc
            KeyDown += OnKeyDownRouted;
            MouseDown += OnMouseDownRouted;

            // Bring to front when shown
            Loaded += (s, e) => Activate();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _source = (HwndSource)PresentationSource.FromVisual(this)!;
            _source.AddHook(WndProc);

            // Register RAWINPUT for keyboard, mouse, and common HID usages
            RAWINPUTDEVICE[] rid =
            {
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = 0, hwndTarget = _source.Handle }, // Keyboard
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = 0, hwndTarget = _source.Handle }, // Mouse
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x04, dwFlags = 0, hwndTarget = _source.Handle }, // Joystick
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x05, dwFlags = 0, hwndTarget = _source.Handle }, // Gamepad
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x08, dwFlags = 0, hwndTarget = _source.Handle }, // Multi-axis
                new RAWINPUTDEVICE { usUsagePage = 0x02, usUsage = 0x00, dwFlags = 0, hwndTarget = _source.Handle }, // Simulation Controls
                new RAWINPUTDEVICE { usUsagePage = 0x0C, usUsage = 0x01, dwFlags = 0, hwndTarget = _source.Handle }, // Consumer Control
            };

            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                var res = InputCapture.ReadInput(lParam);
                if (res != null)
                {
                    SetOutputsFromResult(res);
                    handled = true;
                    DialogResult = true;
                    Close();
                }
            }
            return IntPtr.Zero;
        }

        // ---------------- Routed event fallbacks (keep original behavior) ----------------

        private void OnKeyDownRouted(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore pure modifier keys as "key"
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return; // wait for a non-modifier
            }

            // Build display like "Ctrl+Alt+K"
            var mods = Keyboard.Modifiers;
            string modsText = "";
            if ((mods & ModifierKeys.Control) == ModifierKeys.Control) modsText += "Ctrl+";
            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift) modsText += "Shift+";
            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt) modsText += "Alt+";

            var display = string.IsNullOrEmpty(modsText) ? key.ToString() : (modsText + key);

            var res = new InputBindingResult
            {
                DeviceType = "Keyboard",
                DeviceName = "Keyboard",
                ControlLabel = display
            };

            // Keep old fields in sync
            SetOutputsFromResult(res, modifiers: mods, key: key);

            DialogResult = true;
            Close();
        }

        private void OnMouseDownRouted(object? sender, MouseButtonEventArgs e)
        {
            // Ignore clicks on a Cancel element (named "Cancel" in XAML), same as your original logic
            if (e.ChangedButton == MouseButton.Left && (e.Source is FrameworkElement fe) && fe.Name == "Cancel")
                return;

            var res = new InputBindingResult
            {
                DeviceType = "Mouse",
                DeviceName = "Mouse",
                ControlLabel = e.ChangedButton + " Button"
            };

            SetOutputsFromResult(res, modifiers: ModifierKeys.None, key: Key.None);

            DialogResult = true;
            Close();
        }

        // ---------------- Helpers ----------------

        private void SetOutputsFromResult(InputBindingResult res, ModifierKeys? modifiers = null, Key? key = null)
        {
            Result = res;

            // Back-compat properties:
            Device = res.DeviceType;
            DeviceKey = res.ControlLabel;
            Modifiers = modifiers ?? ModifierKeys.None;
            Key = key ?? Key.None;
            Accepted = true;

            // Optional UI status text (expects x:Name="Status" TextBlock in XAML)
            if (FindName("Status") is System.Windows.Controls.TextBlock status)
            {
                status.Text = "Captured: " + Device + " — " + DeviceKey;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }
    }
}

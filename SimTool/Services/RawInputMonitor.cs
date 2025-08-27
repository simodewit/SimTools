using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimTools.Debug;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Registers for Raw Input (keyboard) using RIDEV_INPUTSINK and raises InputReceived on WM_INPUT.
    /// Robust against invalid HWND and bad parameter combos; logs every step.
    /// </summary>
    public sealed class RawInputMonitor : IDisposable
    {
        private readonly Window _owner;
        private HwndSource _source;
        private bool _disposed;
        private bool _registered;

        private static event Action<InputBindingResult> SyntheticRouted;
        public event Action<InputBindingResult> InputReceived;

        public RawInputMonitor(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            // If the source isn't ready yet, defer until SourceInitialized.
            var src = (HwndSource)PresentationSource.FromVisual(owner);
            if(src == null)
            {
                _owner.SourceInitialized += OwnerOnSourceInitialized;
                _owner.Loaded += OwnerOnLoaded; // extra guard
                Diag.Log("RIM.Ctor: HwndSource null; will register on SourceInitialized/Loaded");
            }
            else
            {
                Init(src);
            }

            SyntheticRouted += OnSyntheticInput;
        }

        private void OwnerOnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                var src = (HwndSource)PresentationSource.FromVisual(_owner);
                if(src != null) Init(src);
            }
            catch(Exception ex) { Diag.LogEx("RIM.OwnerOnSourceInitialized", ex); }
            finally
            {
                _owner.SourceInitialized -= OwnerOnSourceInitialized;
            }
        }

        private void OwnerOnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if(_registered) return;
                var src = (HwndSource)PresentationSource.FromVisual(_owner);
                if(src != null) Init(src);
            }
            catch(Exception ex) { Diag.LogEx("RIM.OwnerOnLoaded", ex); }
            finally
            {
                _owner.Loaded -= OwnerOnLoaded;
            }
        }

        private void Init(HwndSource src)
        {
            _source = src;
            _source.AddHook(WndProc);

            var hwnd = _source.Handle;
            Diag.Log($"RIM.Init: hwnd=0x{hwnd.ToInt64():X}");

            if(hwnd == IntPtr.Zero)
            {
                Diag.Log("RIM.Init: hwnd is NULL, deferring registration");
                return; // Loaded/SourceInitialized handler will retry
            }

            // Register **only the keyboard** first. We'll add extra usages once this succeeds.
            // HID: UsagePage 0x01 (Generic Desktop), Usage 0x06 (Keyboard).
            var rid = new RAWINPUTDEVICE[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x06,
                    dwFlags = RIDEV_INPUTSINK,
                    hwndTarget = hwnd
                }
            };

            bool ok = RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            var err = Marshal.GetLastWin32Error();
            Diag.Log($"RIM.Register(Keyboard only) => {ok} (err={err})");

            if(!ok && err == 87)
            {
                // Retry once, some systems are picky about cbSize vs. packing or race to HWND
                Diag.Log("RIM.Register: err=87, retrying once after 50ms...");
                System.Threading.Thread.Sleep(50);
                ok = RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                err = Marshal.GetLastWin32Error();
                Diag.Log($"RIM.Register(retry) => {ok} (err={err})");
            }

            _registered = ok;
            if(!_registered)
                Diag.Log("RIM.Init: registration FAILED; WM_INPUT will not arrive.");
            else
                Diag.Log("RIM.Init: registration OK; WM_INPUT should arrive.");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(msg == WM_INPUT)
            {
                Diag.Log("RIM.WndProc: WM_INPUT received");
                InputBindingResult res = null;
                try { res = InputCapture.ReadInput(lParam); }
                catch(Exception ex) { Diag.LogEx("InputCapture.ReadInput", ex); }

                if(res == null)
                    Diag.Log("RIM.WndProc: ReadInput -> NULL");
                else
                    Diag.Log($"RIM.WndProc: ReadInput -> dev='{res.DeviceType}' key='{res.ControlLabel}'");

                if(res != null) InputReceived?.Invoke(res);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if(_disposed) return;
            _disposed = true;
            try
            {
                if(_source != null) _source.RemoveHook(WndProc);
            }
            catch { }

            SyntheticRouted -= OnSyntheticInput;
        }

        // P/Invoke + constants

        private const int WM_INPUT = 0x00FF;
        private const int RIDEV_INPUTSINK = 0x00000100;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;     // int instead of uint
            public IntPtr hwndTarget;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        // Add this NEW method:
        private void OnSyntheticInput(InputBindingResult res)
        {
            try
            {
                // Mirror whatever you do after parsing WM_INPUT.
                // If your code currently calls a single helper (e.g., HandleResult(res)),
                // call it here. If not, inline the same steps used in WndProc after ReadInput.
                SimTools.Debug.Diag.Log($"RIM.Synth: dev='{res.DeviceType}' key='{res.ControlLabel}'");
                HandleResult(res); // <-- this is the same internal method you already use after ReadInput
            }
            catch(Exception ex)
            {
                SimTools.Debug.Diag.LogEx("RIM.Synth", ex);
            }
        }

        // Add this NEW public static entry point the hook will call:
        public static void RouteSynthetic(InputBindingResult res)
        {
            try { SyntheticRouted?.Invoke(res); }
            catch(Exception ex) { SimTools.Debug.Diag.LogEx("RIM.RouteSynthetic", ex); }
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimTools.Debug;
using SimTools.Models;

namespace SimTools.Services
{
    public sealed class RawInputMonitor : IDisposable
    {
        private readonly Window _owner;
        private HwndSource _source;
        private bool _disposed;
        private bool _registered;

        // Synthetic pipeline: InputCapture.RouteFromHook -> RawInputMonitor.RouteSynthetic -> SyntheticRouted -> OnSyntheticInput
        private static event Action<InputBindingResult> SyntheticRouted;

        /// <summary>Entry point for the synthetic path. Called by InputCapture.RouteFromHook.</summary>
        public static void RouteSynthetic(InputBindingResult res)
        {
            try { SyntheticRouted?.Invoke(res); }
            catch (Exception ex) { Diag.LogEx("RIM.RouteSynthetic", ex); }
        }

        /// <summary>Raised for BOTH real WM_INPUT and synthetic inputs.</summary>
        public event Action<InputBindingResult> InputReceived;

        public RawInputMonitor(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            var src = (HwndSource)PresentationSource.FromVisual(owner);
            if (src == null)
            {
                _owner.SourceInitialized += OwnerOnSourceInitialized;
                _owner.Loaded += OwnerOnLoaded; // extra guard in case SourceInitialized already fired
                Diag.Log("RIM.Ctor: HwndSource null; will register on SourceInitialized/Loaded");
            }
            else
            {
                Init(src);
            }

            // Subscribe to the synthetic route
            try { SyntheticRouted += OnSyntheticInput; } catch { }
        }

        private void OwnerOnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                var src = (HwndSource)PresentationSource.FromVisual(_owner);
                if (src != null) Init(src);
            }
            catch (Exception ex) { Diag.LogEx("RIM.OwnerOnSourceInitialized", ex); }
            finally { _owner.SourceInitialized -= OwnerOnSourceInitialized; }
        }

        private void OwnerOnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_registered) return;
                var src = (HwndSource)PresentationSource.FromVisual(_owner);
                if (src != null) Init(src);
            }
            catch (Exception ex) { Diag.LogEx("RIM.OwnerOnLoaded", ex); }
            finally { _owner.Loaded -= OwnerOnLoaded; }
        }

        private void Init(HwndSource src)
        {
            if (_disposed || src == null) return;

            try
            {
                _source = src;
                _source.AddHook(WndProc);
                Diag.Log($"RIM.Init: hwnd={_source.Handle}");

                // Register Raw Input: keyboard only, INPUTSINK
                var rid = new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop Controls
                    usUsage = 0x06,     // Keyboard
                    dwFlags = 0x00000100, // RIDEV_INPUTSINK
                    hwndTarget = _source.Handle
                };

                bool ok = RegisterRawInputDevices(new[] { rid }, 1, Marshal.SizeOf<RAWINPUTDEVICE>());
                Diag.Log($"RIM.Register(Keyboard only) => {ok} (err={Marshal.GetLastWin32Error()})");
                _registered = ok;
                if (ok) Diag.Log("RIM.Init: registration OK; WM_INPUT should arrive.");
            }
            catch (Exception ex)
            {
                Diag.LogEx("RIM.Init", ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                Diag.Log("RIM.WndProc: WM_INPUT received");
                InputBindingResult res = null;
                try { res = InputCapture.ReadInput(lParam); }
                catch (Exception ex) { Diag.LogEx("InputCapture.ReadInput", ex); }

                if (res == null)
                {
                    Diag.Log("RIM.WndProc: ReadInput -> NULL");
                }
                else
                {
                    Diag.Log($"RIM.WndProc: ReadInput -> dev='{res.DeviceType}' key='{res.ControlLabel}'");
                    try { InputReceived?.Invoke(res); }
                    catch (Exception ex) { Diag.LogEx("RIM.InputReceived.Invoke", ex); }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Receives synthetic input (from the LL hook path) and forwards it into the same InputReceived event.
        /// This is the key fix so vJoy output fires even when HidHide + swallow prevents WM_INPUT from arriving.
        /// </summary>
        private void OnSyntheticInput(InputBindingResult res)
        {
            try
            {
                if (res == null)
                {
                    Diag.Log("RIM.Synth: NULL result");
                    return;
                }

                Diag.Log($"RIM.Synth: dev='{res.DeviceType}' key='{res.ControlLabel}'");
                InputReceived?.Invoke(res);
            }
            catch (Exception ex)
            {
                Diag.LogEx("RIM.Synth", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { SyntheticRouted -= OnSyntheticInput; } catch { }

            if (_source != null)
            {
                try { _source.RemoveHook(WndProc); } catch { }
                _source = null;
            }

            try
            {
                // Best-effort de-registration (optional)
                var rid = new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x06,
                    dwFlags = 0x00000001, // RIDEV_REMOVE
                    hwndTarget = IntPtr.Zero
                };
                RegisterRawInputDevices(new[] { rid }, 1, Marshal.SizeOf<RAWINPUTDEVICE>());
            }
            catch { }
        }

        // P/Invoke + constants
        private const int WM_INPUT = 0x00FF;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);
    }
}

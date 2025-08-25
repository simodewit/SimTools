// Services/RuntimeInputEngine.cs
#nullable enable
using System;
using System.Threading;
using System.Windows.Interop;
using static SimTools.Interop.RawInputNative;
using SimTools.Models;

namespace SimTools.Services
{
    public sealed class RuntimeInputEngine : IDisposable
    {
        private Thread _thread;
        private HwndSource _source;
        private volatile bool _running;
        public event Action<InputBindingResult> InputDown;
        public event Action<InputBindingResult> InputUp; // optional if you want UPs

        public void Start()
        {
            if(_running) return;
            _running = true;
            _thread = new Thread(ThreadMain) { IsBackground = true, Name = "SimTools.InputEngine" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _source?.Dispatcher?.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal); } catch { }
            try { _thread?.Join(500); } catch { }
        }

        public void Dispose() => Stop();

        private void ThreadMain()
        {
            var parms = new HwndSourceParameters("SimTools.InputEngine")
            {
                Width = 0,
                Height = 0,
                ParentWindow = IntPtr.Zero,
                WindowStyle = unchecked((int)0x80000000) /* WS_POPUP */
            };
            _source = new HwndSource(parms);
            _source.AddHook(WndProc);

            // Register for keyboard, mouse, gamepad, multi-axis, simulation, consumer control
            var rid = new RAWINPUTDEVICE[]
            {
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Keyboard
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Mouse
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x04, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Joystick
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x05, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Gamepad
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x08, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Multi-axis
                new RAWINPUTDEVICE { usUsagePage = 0x02, usUsage = 0x00, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Simulation Controls
                new RAWINPUTDEVICE { usUsagePage = 0x0C, usUsage = 0x01, dwFlags = (int)RIDEV_INPUTSINK, hwndTarget = _source.Handle }, // Consumer Control
            };
            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTDEVICE)));

            System.Windows.Threading.Dispatcher.Run();
            _source.RemoveHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(msg == (int)WM_INPUT)
            {
                var result = InputCapture.ReadInput(lParam); // reuse your parser
                if(result != null)
                {
                    // For simplicity we fire "down" for every message; extend to distinguish UP if needed.
                    InputDown?.Invoke(result);
                }
            }
            return IntPtr.Zero;
        }
    }
}

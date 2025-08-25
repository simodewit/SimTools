// Services/VirtualGamepadService.cs
using System;
using SimTools.Models;

namespace SimTools.Services
{
    public sealed class VirtualGamepadService : IDisposable
    {
        private static readonly VirtualJoystickService _vjoy = new VirtualJoystickService();
        private static readonly KeyboardOutputService _kb = new KeyboardOutputService();

        public bool IsRunning => _vjoy.IsRunning;

        public void TryStart() => _vjoy.TryStart();
        public void Connect() => _vjoy.TryStart();   // old alias
        public void Stop() => _vjoy.Stop();
        public void Disconnect() => _vjoy.Stop();      // old alias
        public void Dispose() => _vjoy.Dispose();

        /// <summary>Original signature some code-behind expects.</summary>
        public void SetButton(int index, bool down) => _vjoy.SetButton(index, down);

        /// <summary>
        /// Overload to support existing calls passing VirtualOutput directly.
        /// Maps aliases & Button1..128 to vJoy; routes Keyboard_* to SendInput.
        /// </summary>
        public void SetButton(VirtualOutput output, bool down)
        {
            if (output == VirtualOutput.None) return;

            if (output.TryAsVJoyButton(out var idx))
            {
                _vjoy.SetButton(idx, down);
                return;
            }

            if (output.TryAsKeyboard(out var sc, out var ex))
            {
                _kb.SendScan(sc, ex, down);
            }
        }

        /// <summary>
        /// Convenience used by some older call sites; safe to keep.
        /// </summary>
        public void Send(VirtualOutput output, bool isDown) => SetButton(output, isDown);
    }
}

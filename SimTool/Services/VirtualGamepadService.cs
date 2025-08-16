#if VIGEM
using System;
using SimTools.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace SimTools.Services
{
    /// <summary>Virtual Xbox 360 controller via ViGEm.</summary>
    public sealed class VirtualGamepadService : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _pad;

        public bool IsRunning => _pad != null;

        public void TryStart()
        {
            if (IsRunning) return;
            try
            {
                _client = new ViGEmClient();
                _pad = _client.CreateXbox360Controller();
                _pad.Connect();
            }
            catch { Stop(); }
        }

        public void Stop()
        {
            try { _pad?.Disconnect(); } catch { }
            try { _pad?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            _pad = null; _client = null;
        }

        public void Dispose() => Stop();

        public void SetButton(VirtualOutput output, bool pressed)
        {
            if (!IsRunning || output == VirtualOutput.None) return;
            var btn = Map(output);
            if (btn != 0) _pad.SetButtonState(btn, pressed);
        }

        private static Xbox360Button Map(VirtualOutput o)
        {
            switch (o)
            {
                case VirtualOutput.A:            return Xbox360Button.A;
                case VirtualOutput.B:            return Xbox360Button.B;
                case VirtualOutput.X:            return Xbox360Button.X;
                case VirtualOutput.Y:            return Xbox360Button.Y;
                case VirtualOutput.LeftBumper:   return Xbox360Button.LeftShoulder;
                case VirtualOutput.RightBumper:  return Xbox360Button.RightShoulder;
                case VirtualOutput.Back:         return Xbox360Button.Back;
                case VirtualOutput.Start:        return Xbox360Button.Start;
                case VirtualOutput.LeftThumb:    return Xbox360Button.LeftThumb;
                case VirtualOutput.RightThumb:   return Xbox360Button.RightThumb;
                case VirtualOutput.DpadUp:       return Xbox360Button.Up;
                case VirtualOutput.DpadDown:     return Xbox360Button.Down;
                case VirtualOutput.DpadLeft:     return Xbox360Button.Left;
                case VirtualOutput.DpadRight:    return Xbox360Button.Right;
                default:                         return 0;
            }
        }
    }
}
#else
using System;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>NO-OP stub so the project compiles without ViGEm.</summary>
    public sealed class VirtualGamepadService : IDisposable
    {
        public bool IsRunning => false;
        public void TryStart() { }
        public void Stop() { }
        public void Dispose() { }
        public void SetButton(VirtualOutput output, bool pressed) { }
    }
}
#endif

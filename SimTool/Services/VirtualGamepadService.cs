// Services/VirtualGamepadService.cs
#nullable enable
using System;
using SimTools.Models;
using vJoyInterfaceWrap;

namespace SimTools.Services
{
    /// <summary>Virtual output via vJoy (buttons 1..128). No UI changes required.</summary>
    public sealed class VirtualGamepadService : IDisposable
    {
        private readonly vJoy _vj = new vJoy();
        private readonly uint _id = 1;
        private bool _acquired;

        public bool IsRunning => _acquired;

        public void TryStart()
        {
            if(_acquired) return;

            // Ensure driver + device are ready (auto-configure Device 1 with 128 buttons if needed)
            VJoyBootstrap.EnsureConfigured(_id, requiredButtons: 128);

            var status = _vj.GetVJDStatus(_id);
            if(status == VjdStat.VJD_STAT_BUSY)
                throw new InvalidOperationException($"vJoy device {_id} is already in use by another process.");
            if(status == VjdStat.VJD_STAT_MISS)
                throw new InvalidOperationException($"vJoy device {_id} is missing. Please open vJoyConfig.");

            if(!_vj.AcquireVJD(_id))
                throw new InvalidOperationException($"Failed to acquire vJoy device {_id}.");

            _acquired = true;
            try { _vj.ResetButtons(_id); } catch { /* ignore */ }
        }

        public void SetButton(VirtualOutput output, bool pressed)
        {
            if(!_acquired || output == VirtualOutput.None) return;

            int btn = (int)output;
            if(btn < 1 || btn > 128) return;

            // vJoy uses 1-based button indices
            _vj.SetBtn(pressed, _id, (uint)btn);
        }

        public void Stop()
        {
            if(!_acquired) return;
            try { _vj.ResetButtons(_id); } catch { /* ignore */ }
            try { _vj.RelinquishVJD(_id); } catch { /* ignore */ }
            _acquired = false;
        }

        public void Dispose() => Stop();
    }
}

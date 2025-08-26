using System;
using vJoyInterfaceWrap;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Minimal vJoy wrapper that just acquires Device #1 and can press/release buttons.
    /// Assumes vJoy is installed and Device #1 is already configured (e.g., 128 buttons).
    /// No elevation, no configuration, no popups.
    /// </summary>
    public sealed class VirtualGamepadService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly vJoy _vjoy = new vJoy();
        private const uint DeviceId = 1;
        private bool _acquired;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; } = "";
        public int ButtonCount { get; private set; }

        /// <summary>
        /// Try to acquire vJoy Device #1. Returns true if acquired; false otherwise.
        /// </summary>
        public bool TryStart()
        {
            lock (_sync)
            {
                IsReady = false;
                LastError = "";
                ButtonCount = 0;

                // vJoy driver present?
                if (!_vjoy.vJoyEnabled())
                {
                    LastError = "vJoy driver not enabled/installed.";
                    return false;
                }

                // Device status must be FREE or already OWNed by us
                var status = _vjoy.GetVJDStatus(DeviceId);
                if (status != VjdStat.VJD_STAT_FREE && status != VjdStat.VJD_STAT_OWN)
                {
                    LastError = $"vJoy Device #{DeviceId} is not available (status: {status}).";
                    return false;
                }

                // Acquire if not already ours
                if (status != VjdStat.VJD_STAT_OWN)
                {
                    _acquired = _vjoy.AcquireVJD(DeviceId);
                    if (!_acquired)
                    {
                        LastError = $"Failed to acquire vJoy Device #{DeviceId}.";
                        return false;
                    }
                }
                else
                {
                    _acquired = true;
                }

                // Clear device & read capabilities (informational only)
                try { _vjoy.ResetVJD(DeviceId); } catch { }
                try { ButtonCount = (int)_vjoy.GetVJDButtonNumber(DeviceId); } catch { ButtonCount = 0; }

                IsReady = true;
                return true;
            }
        }

        /// <summary>
        /// Set a vJoy button state. Ignores requests if not ready.
        /// VirtualOutput enum must map 1..128 to buttons; 0 means None.
        /// </summary>
        public void SetButton(VirtualOutput output, bool down)
        {
            if (!IsReady) return;
            uint index = (uint)output;
            if (index == 0) return;

            lock (_sync)
            {
                try { _vjoy.SetBtn(down, DeviceId, index); } catch { /* ignore at runtime */ }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                try
                {
                    if (_acquired)
                    {
                        try { _vjoy.ResetVJD(DeviceId); } catch { }
                        _vjoy.RelinquishVJD(DeviceId);
                        _acquired = false;
                    }
                }
                catch { }
            }
        }

        /// <summary>Optional: simple status string if you want to log it.</summary>
        public string StatusSummary()
            => $"vJoy Enabled: {_vjoy.vJoyEnabled()}, Device: {DeviceId}, " +
               $"Acquired: {_acquired}, Buttons reported: {ButtonCount}";
    }
}

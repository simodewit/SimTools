using System;
using vJoyInterfaceWrap;
using SimTools.Models;
using SimTools.Debug;

namespace SimTools.Services
{
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
            lock(_sync)
            {
                IsReady = false;
                _acquired = false;
                LastError = "";
                ButtonCount = 0;

                try
                {
                    var enabled = _vjoy.vJoyEnabled();
                    var status = _vjoy.GetVJDStatus(DeviceId);
                    Diag.Log($"vJoy.TryStart: enabled={enabled}, deviceId={DeviceId}, status={status}");

                    if(!enabled)
                    {
                        LastError = "vJoy driver not enabled/installed.";
                        Diag.Log($"vJoy.TryStart: FAIL -> {LastError}");
                        return false;
                    }

                    if(status != VjdStat.VJD_STAT_FREE && status != VjdStat.VJD_STAT_OWN)
                    {
                        LastError = $"vJoy Device #{DeviceId} is not available (status: {status}).";
                        Diag.Log($"vJoy.TryStart: FAIL -> {LastError}");
                        return false;
                    }

                    if(status == VjdStat.VJD_STAT_FREE)
                    {
                        var ok = _vjoy.AcquireVJD(DeviceId);
                        Diag.Log($"vJoy.AcquireVJD({DeviceId}) => {ok}");
                        if(!ok)
                        {
                            LastError = "AcquireVJD returned false.";
                            Diag.Log($"vJoy.TryStart: FAIL -> {LastError}");
                            return false;
                        }
                        _acquired = true;
                    }
                    else
                    {
                        // already owned by us
                        _acquired = true;
                    }

                    try { _vjoy.ResetVJD(DeviceId); } catch(Exception ex) { Diag.LogEx("vJoy.ResetVJD", ex); }

                    try { ButtonCount = (int)_vjoy.GetVJDButtonNumber(DeviceId); }
                    catch(Exception ex) { ButtonCount = 0; Diag.LogEx("vJoy.GetVJDButtonNumber", ex); }

                    IsReady = true;
                    Diag.Log($"vJoy.TryStart: SUCCESS, buttons={ButtonCount}");
                    return true;
                }
                catch(Exception ex)
                {
                    LastError = ex.Message;
                    Diag.LogEx("vJoy.TryStart", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Press or release a vJoy button (1-based).
        /// </summary>
        public void SetButton(VirtualOutput output, bool down)
        {
            if(output == VirtualOutput.None) return;

            var index = (uint)((int)output - (int)VirtualOutput.VJoy1_Btn1 + 1);

            lock(_sync)
            {
                // Re-validate vJoy each time; cheap and robust against re-enumeration
                if(!_vjoy.vJoyEnabled())
                {
                    IsReady = false;
                    _acquired = false;
                    Diag.Log("vJoy.SetButton: vJoy driver not enabled");
                    return;
                }

                var status = _vjoy.GetVJDStatus(DeviceId);
                if(status != VjdStat.VJD_STAT_OWN)
                {
                    // Try to (re)acquire
                    if(status == VjdStat.VJD_STAT_FREE)
                    {
                        var okAcquire = _vjoy.AcquireVJD(DeviceId);
                        Diag.Log($"vJoy.SetButton: re-acquire -> {okAcquire}, status was {status}");
                        _acquired = okAcquire;
                        IsReady = okAcquire;
                        if(!okAcquire) return;
                    }
                    else
                    {
                        // Busy or missing – bail out cleanly
                        _acquired = false;
                        IsReady = false;
                        Diag.Log($"vJoy.SetButton: device not OWN (status={status})");
                        return;
                    }
                }

                var ok = _vjoy.SetBtn(down, DeviceId, index);
                Diag.Log($"vJoy.SetButton: out={output}({index}) down={down} -> {ok}");
                if(!ok)
                {
                    // Last-chance: device may have bounced mid-call; reset our state
                    _acquired = false;
                    IsReady = false;
                }
            }
        }

        public string StatusSummary()
            => $"vJoy Enabled: {_vjoy.vJoyEnabled()}, Device: {DeviceId}, " +
               $"Acquired: {_acquired}, Buttons reported: {ButtonCount}, LastError='{LastError}'";

        public void Dispose()
        {
            lock(_sync)
            {
                try
                {
                    if(_acquired)
                    {
                        try { _vjoy.ResetVJD(DeviceId); } catch { }
                        try { _vjoy.RelinquishVJD(DeviceId); } catch { }
                    }
                }
                catch { }
                finally
                {
                    _acquired = false;
                    IsReady = false;
                }
            }
        }

        public void MarkStale()
        {
            lock(_sync)
            {
                _acquired = false;
                IsReady = false;
            }
        }
    }
}

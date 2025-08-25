// Services/VirtualJoystickService.cs
using System;
using System.Runtime.InteropServices;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// vJoy adapter for a single virtual joystick (ID=1).
    /// Exposes SetButton(1..128, down) and handles acquire/release.
    /// </summary>
    public sealed class VirtualJoystickService : IDisposable
    {
        private const uint DeviceId = 1;

        public bool IsRunning { get; private set; }
        private bool[] _cache = new bool[129]; // 1..128

        public void TryStart()
        {
            if(IsRunning) return;

            if(!vJoyEnabled()) throw new InvalidOperationException("vJoy driver not enabled.");
            var status = GetVJDStatus(DeviceId);
            switch(status)
            {
                case VjdStat.VJD_STAT_OWN:
                    IsRunning = true;
                    break;
                case VjdStat.VJD_STAT_FREE:
                    if(!AcquireVJD(DeviceId)) throw new InvalidOperationException("Failed to acquire vJoy device.");
                    IsRunning = true;
                    break;
                case VjdStat.VJD_STAT_BUSY:
                case VjdStat.VJD_STAT_MISS:
                default:
                    throw new InvalidOperationException($"vJoy device #{DeviceId} not available: {status}");
            }

            ResetVJD(DeviceId);
            Array.Clear(_cache, 0, _cache.Length);
        }

        public void Stop()
        {
            if(!IsRunning) return;
            try { ResetVJD(DeviceId); } catch { }
            try { RelinquishVJD(DeviceId); } catch { }
            IsRunning = false;
        }

        public void Dispose() => Stop();

        public void SetButton(int index, bool down)
        {
            if(!IsRunning) return;
            if(index < 1 || index > 128) return;
            if(_cache[index] == down) return; // skip redundant
            _cache[index] = down;
            SetBtn(down, DeviceId, (uint)index);
        }

        // -------- vJoy interop (vJoyInterface.dll) --------
        private enum VjdStat
        {
            VJD_STAT_OWN = 0,   // Owned by this application
            VJD_STAT_FREE = 1,  // Available
            VJD_STAT_BUSY = 2,  // Owned by another app
            VJD_STAT_MISS = 3,  // Not installed or disabled
            VJD_STAT_UNKN = 4
        }

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool vJoyEnabled();

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern VjdStat GetVJDStatus(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool AcquireVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern void RelinquishVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool ResetVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetBtn(bool Value, uint rID, uint nBtn);
    }
}

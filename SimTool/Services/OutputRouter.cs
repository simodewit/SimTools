// Services/OutputRouter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Central translator: map current map's bindings to vJoy or keyboard output.
    /// Tracks active outputs and releases them on map/profile switches or suspend.
    /// </summary>
    public sealed class OutputRouter : IDisposable
    {
        private readonly AppState _state;
        private readonly VirtualJoystickService _vjoy;
        private readonly KeyboardOutputService _kb;
        private readonly GameModeGuard _guard;

        // Track "pressed" outputs so we can release on map switch/app exit
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);

        public OutputRouter(AppState state, VirtualJoystickService vjoy, KeyboardOutputService kb, GameModeGuard guard)
        {
            _state = state;
            _vjoy = vjoy;
            _kb = kb;
            _guard = guard ?? new GameModeGuard();

            // on map change, release everything
            // (hook: ensure caller invokes ReleaseAllActive when State.CurrentMap changes)
        }

        public void Dispose() => ReleaseAllActive();

        public void ReleaseAllActive()
        {
            // Release keyboard keys
            foreach(var key in _active.Where(a => a.StartsWith("K:")).ToList())
            {
                var parts = key.Split(':'); // K:scan:ex
                ushort sc = ushort.Parse(parts[1]);
                bool ex = parts[2] == "1";
                _kb.SendScan(sc, ex, false);
                _active.Remove(key);
            }

            // Release joystick buttons
            foreach(var joy in _active.Where(a => a.StartsWith("J:")).ToList())
            {
                var idx = int.Parse(joy.Substring(2));
                _vjoy.SetButton(idx, false);
                _active.Remove(joy);
            }
        }

        public void OnInput(InputBindingResult fired)
        {
            if(!_guard.ShouldOperateNow()) return;
            var map = _state.CurrentMap;
            if(map == null || map.Keybinds == null || fired == null) return;

            // Find all bindings that match this input (device label match like your UI uses)
            var matches = map.Keybinds.Where(kb => IsMatch(kb, fired)).ToList();
            if(matches.Count == 0) return;

            foreach(var kb in matches)
                Emit(kb.Output, true);
        }

        public void OnRelease(InputBindingResult fired) // optional if you track releases
        {
            var map = _state.CurrentMap;
            if(map == null || map.Keybinds == null || fired == null) return;

            var matches = map.Keybinds.Where(kb => IsMatch(kb, fired)).ToList();
            if(matches.Count == 0) return;

            foreach(var kb in matches)
                Emit(kb.Output, false);
        }

        private void Emit(VirtualOutput output, bool isDown)
        {
            if(output == VirtualOutput.None) return;

            if(output.TryAsVJoyButton(out var idx))
            {
                _vjoy.SetButton(idx, isDown);
                Track($"J:{idx}", isDown);
                return;
            }

            if(output.TryAsKeyboard(out var sc, out var ex))
            {
                _kb.SendScan(sc, ex, isDown);
                Track($"K:{sc}:{(ex ? 1 : 0)}", isDown);
                return;
            }
        }

        private static bool IsMatch(KeybindBinding kb, InputBindingResult fired)
        {
            if(kb == null || fired == null) return false;
            // Keep the same "string match" semantics you already use in the UI. :contentReference[oaicite:5]{index=5}
            if(string.IsNullOrWhiteSpace(kb.Device) || string.IsNullOrWhiteSpace(kb.DeviceKey))
                return false;

            if(!kb.Device.Equals(fired.DeviceType, StringComparison.OrdinalIgnoreCase))
                return false;

            var a = kb.DeviceKey ?? "";
            var b = fired.ControlLabel ?? "";
            if(a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;

            // HID UsagePage heuristic (same as your DeviceBindingDescriptor). :contentReference[oaicite:6]{index=6}
            if(a.Contains("UsagePage") && b.Contains("UsagePage")) return true;

            return false;
        }

        private void Track(string id, bool down)
        {
            if(down) _active.Add(id);
            else _active.Remove(id);
        }
    }
}

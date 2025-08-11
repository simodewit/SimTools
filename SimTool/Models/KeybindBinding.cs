using System;
using System.Windows.Input;

namespace SimTools.Models
{
    public class KeybindBinding
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Action";

        // Keyboard binding fields
        public Key Key { get; set; } = Key.None;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

        // Device metadata for display (e.g., "Keyboard: Ctrl+K", "Mouse: Left Button")
        public string Device { get; set; }  // "Keyboard" / "Mouse" / "Gamepad" (future)
        public string DeviceKey { get; set; }

        public override string ToString()
        {
            // Prefer Device/DeviceKey if present
            if (!string.IsNullOrEmpty(DeviceKey))
            {
                return string.IsNullOrEmpty(Device) ? DeviceKey : (Device + ": " + DeviceKey);
            }

            if (Key == Key.None && Modifiers == ModifierKeys.None) return "None";
            return Modifiers == ModifierKeys.None ? Key.ToString() : (Modifiers + " + " + Key);
        }
    }
}

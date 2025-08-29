using System.Collections.Generic;
using System.Windows.Input;

namespace SimTools.Models
{
    public class KeybindBinding
    {
        /// <summary>Display name shown in the UI (user-defined action name).</summary>
        public string Name { get; set; }

        /// <summary>Source device type label (e.g., "Keyboard", "Wheel", etc.).</summary>
        public string Device { get; set; }

        /// <summary>Source control label (e.g., "Ctrl + K", "Button 5").</summary>
        public string DeviceKey { get; set; }

        /// <summary>Keyboard key (if using keyboard capture).</summary>
        public Key Key { get; set; }

        /// <summary>Keyboard modifiers (if using keyboard capture).</summary>
        public ModifierKeys Modifiers { get; set; }

        /// <summary>
        /// Which virtual output to press when this binding fires
        /// (mapped to a ViGEm Xbox 360 button in the service layer).
        /// </summary>
        public VirtualOutput Output { get; set; } = VirtualOutput.None;

        /// <summary>
        /// When true, swallow the original physical key globally (via low-level hook).
        /// </summary>
        public bool BlockOriginal { get; set; } = true;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Device) && !string.IsNullOrWhiteSpace(DeviceKey))
                return $"{Device}: {DeviceKey}";

            if (Key == Key.None && Modifiers == ModifierKeys.None)
                return "None";

            // Friendly label for keyboard hotkeys
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            string keyName = Key switch
            {
                Key.Return => "Enter",
                Key.Escape => "Esc",
                Key.Prior => "PageUp",
                Key.Next => "PageDown",
                Key.OemPlus => "+",
                Key.OemMinus => "-",
                _ => Key.ToString()
            };

            parts.Add(keyName);
            return string.Join(" + ", parts);
        }
    }
}

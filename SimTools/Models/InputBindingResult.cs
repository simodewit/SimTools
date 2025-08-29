// models/InputBindingResult.cs
#nullable enable

namespace SimTools.Models
{
    /// <summary>
    /// Describes one captured input:
    /// - What device it came from (keyboard, mouse, or HID/game controller)
    /// - A friendly device name (if known) and its VID/PID
    /// - A short label for the control pressed (e.g., Enter, LeftButton, UsagePage 1/Usage 4)
    /// </summary>
    public class InputBindingResult
    {
        public string DeviceType { get; set; } = "";  // Keyboard / Mouse / HID
        public string? DeviceName { get; set; }       // Friendly-ish name or device path
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public string ControlLabel { get; set; } = ""; // e.g., Enter / LeftButton / UsagePage 1, Usage 4

        public override string ToString()
            => string.IsNullOrWhiteSpace(DeviceName) ? $"{DeviceType}: {ControlLabel}" : $"{DeviceName}: {ControlLabel}";
    }
}

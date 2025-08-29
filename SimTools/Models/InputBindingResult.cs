#nullable enable

namespace SimTools.Models
{
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

using System;

namespace SimTools.Models
{
    public sealed class DeviceBindingDescriptor
    {
        public string DeviceType { get; set; }
        public string ControlLabel { get; set; }

        public override string ToString()
            => string.IsNullOrWhiteSpace(DeviceType) && string.IsNullOrWhiteSpace(ControlLabel)
                ? "None"
                : string.IsNullOrWhiteSpace(DeviceType)
                    ? ControlLabel
                    : string.IsNullOrWhiteSpace(ControlLabel)
                        ? DeviceType
                        : $"{DeviceType}: {ControlLabel}";

        public static bool IsMatch(DeviceBindingDescriptor a, SimTools.Models.InputBindingResult b)
        {
            if (a == null || b == null) return false;
            if (string.IsNullOrWhiteSpace(a.DeviceType) || string.IsNullOrWhiteSpace(b.DeviceType)) return false;
            if (!a.DeviceType.Equals(b.DeviceType, StringComparison.OrdinalIgnoreCase)) return false;

            var al = a.ControlLabel ?? "";
            var bl = b.ControlLabel ?? "";
            if (al.Equals(bl, StringComparison.OrdinalIgnoreCase)) return true;

            // Same HID Usage category? Good enough for a “match” highlight.
            if (al.Contains("UsagePage") && bl.Contains("UsagePage")) return true;

            return false;
        }
    }
}

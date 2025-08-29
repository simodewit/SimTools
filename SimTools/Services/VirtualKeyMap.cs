using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Default mapping from vJoy buttons to rare desktop-friendly keys.
    /// Uses F13..F24 + Ctrl/Alt/Shift combos for capacity.
    /// </summary>
    public static class VirtualKeyMap
    {
        // Scan codes for F13..F24 (extended F-keys many keyboards don’t have)
        // On Win, F13..F24 scancodes are 0x64..0x6F.
        private static readonly ushort[] Fxx = {
            0x64,0x65,0x66,0x67,0x68,0x69,0x6A,0x6B,0x6C,0x6D,0x6E,0x6F
        };

        public struct Chord { public ushort Scan; public bool Ctrl, Alt, Shift; }

        /// <summary>
        /// Maps the given vJoy button (1..128) to a key chord.
        /// We cover 48 buttons by default: 12 F-keys * 4 modifier bundles.
        /// Extend as needed.
        /// </summary>
        public static bool TryMap(VirtualOutput btn, out Chord chord)
        {
            var i = (int)btn;
            chord = default;
            if (i <= 0) return false;

            // Bucket into groups of 12 (F13..F24)
            // 1..12 => plain F13..F24
            // 13..24 => Ctrl + F13..F24
            // 25..36 => Alt + F13..F24
            // 37..48 => Shift + F13..F24
            // You can add more (Ctrl+Alt, etc.) if you need >48.
            var idx = (i - 1) % 12;
            var group = (i - 1) / 12;

            if (idx < 0 || idx >= Fxx.Length) return false;
            if (group > 3) return false; // only first 48 mapped by default

            chord = new Chord
            {
                Scan = Fxx[idx],
                Ctrl = group == 1,
                Alt = group == 2,
                Shift = group == 3
            };
            return true;
        }
    }
}

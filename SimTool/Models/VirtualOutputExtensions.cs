// Models/VirtualOutputExtensions.cs
using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SimTools.Models
{
    public static class VirtualOutputExtensions
    {
        /// <summary>Returns true if this output is a vJoy button; outputs 1..128 index.</summary>
        public static bool TryAsVJoyButton(this VirtualOutput o, out int buttonIndex)
        {
            // Map back-compat aliases to Button1..Button14
            switch(o)
            {
                case VirtualOutput.A: buttonIndex = 1; return true;
                case VirtualOutput.B: buttonIndex = 2; return true;
                case VirtualOutput.X: buttonIndex = 3; return true;
                case VirtualOutput.Y: buttonIndex = 4; return true;
                case VirtualOutput.LeftBumper: buttonIndex = 5; return true;
                case VirtualOutput.RightBumper: buttonIndex = 6; return true;
                case VirtualOutput.Back: buttonIndex = 7; return true;
                case VirtualOutput.Start: buttonIndex = 8; return true;
                case VirtualOutput.LeftThumb: buttonIndex = 9; return true;
                case VirtualOutput.RightThumb: buttonIndex = 10; return true;
                case VirtualOutput.DpadUp: buttonIndex = 11; return true;
                case VirtualOutput.DpadDown: buttonIndex = 12; return true;
                case VirtualOutput.DpadLeft: buttonIndex = 13; return true;
                case VirtualOutput.DpadRight: buttonIndex = 14; return true;
            }

            if(o >= VirtualOutput.Button1 && o <= VirtualOutput.Button128)
            {
                buttonIndex = (int)o - (int)VirtualOutput.Button1 + 1; // Button1 -> 1, Button128 -> 128
                return true;
            }

            buttonIndex = 0;
            return false;
        }

        /// <summary>Returns true if this output is a keyboard key; outputs Win32 scan code.</summary>
        public static bool TryAsKeyboard(this VirtualOutput o, out ushort scanCode, out bool isExtended)
        {
            // Map to hardware scan codes (US layout-safe for these keys)
            // Reference: typical PC/AT set; we avoid VKs to sidestep layout issues.
            isExtended = false;
            switch(o)
            {
                case VirtualOutput.Keyboard_Enter: scanCode = 0x1C; return true;
                case VirtualOutput.Keyboard_Escape: scanCode = 0x01; return true;
                case VirtualOutput.Keyboard_Space: scanCode = 0x39; return true;
                case VirtualOutput.Keyboard_Tab: scanCode = 0x0F; return true;
                case VirtualOutput.Keyboard_Backspace: scanCode = 0x0E; return true;
                case VirtualOutput.Keyboard_Delete: scanCode = 0x53; isExtended = true; return true;
                case VirtualOutput.Keyboard_Home: scanCode = 0x47; isExtended = true; return true;
                case VirtualOutput.Keyboard_End: scanCode = 0x4F; isExtended = true; return true;
                case VirtualOutput.Keyboard_PageUp: scanCode = 0x49; isExtended = true; return true;
                case VirtualOutput.Keyboard_PageDown: scanCode = 0x51; isExtended = true; return true;
                case VirtualOutput.Keyboard_ArrowUp: scanCode = 0x48; isExtended = true; return true;
                case VirtualOutput.Keyboard_ArrowDown: scanCode = 0x50; isExtended = true; return true;
                case VirtualOutput.Keyboard_ArrowLeft: scanCode = 0x4B; isExtended = true; return true;
                case VirtualOutput.Keyboard_ArrowRight: scanCode = 0x4D; isExtended = true; return true;
                case VirtualOutput.Keyboard_F1: scanCode = 0x3B; return true;
                case VirtualOutput.Keyboard_F2: scanCode = 0x3C; return true;
                case VirtualOutput.Keyboard_F3: scanCode = 0x3D; return true;
                case VirtualOutput.Keyboard_F4: scanCode = 0x3E; return true;
                case VirtualOutput.Keyboard_F5: scanCode = 0x3F; return true;
                case VirtualOutput.Keyboard_F6: scanCode = 0x40; return true;
                case VirtualOutput.Keyboard_F7: scanCode = 0x41; return true;
                case VirtualOutput.Keyboard_F8: scanCode = 0x42; return true;
                case VirtualOutput.Keyboard_F9: scanCode = 0x43; return true;
                case VirtualOutput.Keyboard_F10: scanCode = 0x44; return true;
                case VirtualOutput.Keyboard_F11: scanCode = 0x57; return true;
                case VirtualOutput.Keyboard_F12: scanCode = 0x58; return true;
            }

            scanCode = 0;
            return false;
        }

        /// <summary>For ViewModels: classify to group/ordering in the ComboBox.</summary>
        public static OutputGroup GetGroup(this VirtualOutput o)
        {
            if(o == VirtualOutput.None) return OutputGroup.None;
            if(o.TryAsVJoyButton(out _)) return OutputGroup.Joystick;
            if(o.TryAsKeyboard(out _, out _)) return OutputGroup.Keyboard;
            return OutputGroup.Other;
        }
    }

    public enum OutputGroup { None, Joystick, Keyboard, Other }
}

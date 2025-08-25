// Models/VirtualOutput.cs
using System;

namespace SimTools.Models
{
    /// <summary>
    /// What the binding should emit.
    /// - Button1..Button128 map to the virtual HID joystick via vJoy.
    /// - Keyboard_* entries send Win32 keyboard keys via SendInput.
    /// - Aliases (A/B/X/Y...) remain for backward compatibility with existing saves.
    /// </summary>
    public enum VirtualOutput
    {
        None = 0,

        // ---- Back-compat aliases (mapped to Button1..14) ----
        A, B, X, Y,
        LeftBumper, RightBumper,
        Back, Start,
        LeftThumb, RightThumb,
        DpadUp, DpadDown, DpadLeft, DpadRight,

        // ---- Joystick buttons (vJoy) ----
        Button1 = 101,  // Keep distance from aliases to be explicit (XML stores names anyway)
        Button2, Button3, Button4, Button5, Button6, Button7, Button8, Button9, Button10,
        Button11, Button12, Button13, Button14, Button15, Button16, Button17, Button18, Button19, Button20,
        Button21, Button22, Button23, Button24, Button25, Button26, Button27, Button28, Button29, Button30,
        Button31, Button32, Button33, Button34, Button35, Button36, Button37, Button38, Button39, Button40,
        Button41, Button42, Button43, Button44, Button45, Button46, Button47, Button48, Button49, Button50,
        Button51, Button52, Button53, Button54, Button55, Button56, Button57, Button58, Button59, Button60,
        Button61, Button62, Button63, Button64, Button65, Button66, Button67, Button68, Button69, Button70,
        Button71, Button72, Button73, Button74, Button75, Button76, Button77, Button78, Button79, Button80,
        Button81, Button82, Button83, Button84, Button85, Button86, Button87, Button88, Button89, Button90,
        Button91, Button92, Button93, Button94, Button95, Button96, Button97, Button98, Button99, Button100,
        Button101, Button102, Button103, Button104, Button105, Button106, Button107, Button108, Button109, Button110,
        Button111, Button112, Button113, Button114, Button115, Button116, Button117, Button118, Button119, Button120,
        Button121, Button122, Button123, Button124, Button125, Button126, Button127, Button128,

        // ---- Keyboard outputs (minimal but useful set; expand later if needed) ----
        Keyboard_Enter = 1001,
        Keyboard_Escape,
        Keyboard_Space,
        Keyboard_Tab,
        Keyboard_Backspace,
        Keyboard_Delete,
        Keyboard_Home,
        Keyboard_End,
        Keyboard_PageUp,
        Keyboard_PageDown,
        Keyboard_ArrowUp,
        Keyboard_ArrowDown,
        Keyboard_ArrowLeft,
        Keyboard_ArrowRight,
        Keyboard_F1, Keyboard_F2, Keyboard_F3, Keyboard_F4, Keyboard_F5, Keyboard_F6,
        Keyboard_F7, Keyboard_F8, Keyboard_F9, Keyboard_F10, Keyboard_F11, Keyboard_F12
    }
}

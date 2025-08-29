using System.Windows.Input;

namespace SimTools.Services
{
    public struct HotkeyCaptureResult
    {
        public ModifierKeys Modifiers;
        public Key Key;
    }

    /// <summary>
    /// Helper for turning a WPF KeyEvent into a (Modifiers + Key) hotkey.
    /// Filters out pure modifier keys so you only get a real key when one is pressed.
    /// Use in your keybind UI to read what the user pressed.
    /// </summary>

    public static class HotkeyService
    {
        public static HotkeyCaptureResult Capture(KeyEventArgs e)
        {
            var result = new HotkeyCaptureResult();
            result.Modifiers = Keyboard.Modifiers;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt || key == Key.LWin || key == Key.RWin)
            {
                key = Key.None;
            }
            result.Key = key;
            return result;
        }
    }
}

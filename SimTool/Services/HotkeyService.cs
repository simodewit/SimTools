using System.Windows.Input;

namespace SimTools.Services
{
    public struct HotkeyCaptureResult
    {
        public ModifierKeys Modifiers;
        public Key Key;
    }

    // Basic key capture for binding UI; OS-level global hotkeys intentionally out-of-scope here.
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

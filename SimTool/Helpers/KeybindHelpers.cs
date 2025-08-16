using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace SimTools.Helpers
{
    public static class KeybindHelpers
    {
        public static string BuildKeyboardLabel(ModifierKeys mods, Key key)
        {
            var sb = new StringBuilder();
            if (mods.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
            if (mods.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
            if (mods.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
            if (mods.HasFlag(ModifierKeys.Windows)) sb.Append("Win+");

            string keyName;
            switch (key)
            {
                case Key.Return: keyName = "Enter"; break;
                case Key.Escape: keyName = "Esc"; break;
                case Key.Prior: keyName = "PageUp"; break;
                case Key.Next: keyName = "PageDown"; break;
                case Key.OemPlus: keyName = "+"; break;
                case Key.OemMinus: keyName = "-"; break;
                default: keyName = key.ToString(); break;
            }

            var s = sb.Append(keyName).ToString();
            if (s.EndsWith("+")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        /// <summary>Do not trigger map switches while any text input element has focus.</summary>
        public static bool IsTyping()
        {
            var fo = Keyboard.FocusedElement as DependencyObject;
            while (fo != null)
            {
                if (fo is TextBox || fo is RichTextBox || fo is PasswordBox)
                    return true;
                fo = System.Windows.Media.VisualTreeHelper.GetParent(fo);
            }
            return false;
        }
    }
}

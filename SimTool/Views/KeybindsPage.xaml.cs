using SimTools.Models;
using SimTools.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimTools.Views
{
    public partial class KeybindsPage : UserControl
    {
        public KeybindsPage()
        {
            InitializeComponent();
        }

        private KeybindsViewModel VM { get { return DataContext as KeybindsViewModel; } }

        // Capture a key for a specific keybind row (pressed while the focus is inside the binding field)
        private void AssignKey_OnKeyDown(object sender, KeyEventArgs e)
        {
            var binding = (sender as FrameworkElement)?.DataContext as KeybindBinding;
            if (binding == null) return;

            var cap = Services.HotkeyService.Capture(e);
            if (cap.Key == Key.None) return;

            binding.Modifiers = cap.Modifiers;
            binding.Key = cap.Key;
            e.Handled = true;

            // Optional: persist immediately
            if (VM != null && VM.Save != null && VM.Save.CanExecute(null))
                VM.Save.Execute(null);
        }

        private void RemoveKeybind_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.SelectedMap == null || VM.SelectedMap.Keybinds == null) return;

            var kb = (sender as FrameworkElement)?.DataContext as KeybindBinding;
            if (kb == null) return;

            VM.SelectedMap.Keybinds.Remove(kb);
            if (VM.Save != null && VM.Save.CanExecute(null))
                VM.Save.Execute(null);
        }

        // General settings hotkeys
        private void NextMapKey_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (VM == null) return;
            var cap = Services.HotkeyService.Capture(e);
            if (cap.Key == Key.None) return;
            VM.NextMapHotkey.Modifiers = cap.Modifiers;
            VM.NextMapHotkey.Key = cap.Key;
            e.Handled = true;
        }

        private void PrevMapKey_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (VM == null) return;
            var cap = Services.HotkeyService.Capture(e);
            if (cap.Key == Key.None) return;
            VM.PrevMapHotkey.Modifiers = cap.Modifiers;
            VM.PrevMapHotkey.Key = cap.Key;
            e.Handled = true;
        }
    }
}

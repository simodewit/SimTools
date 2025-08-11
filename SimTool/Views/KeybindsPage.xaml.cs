using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimTools.Views
{
    public partial class KeybindsPage : UserControl
    {
        public KeybindsPage()
        {
            InitializeComponent();
        }

        // ---------- Assign key to a keybind row ----------
        private void AssignKey_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var dlg = new ListeningDialog { Owner = owner };
            var result = dlg.ShowDialog();
            if (result != true) return;

            string label = dlg.ResultLabel ?? "Unassigned";

            // Data item for this row
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext;

            // Persist into the model (try several common property names)
            TrySetStringProperty(item, new[] { "BindingLabel", "Display", "AssignedKey", "KeyName", "Key", "Binding" }, label);

            // Update the button's visible text immediately (independent of binding)
            var btn = sender as Button;
            if (btn != null)
            {
                if (btn.Content is TextBlock tb)
                    tb.Text = label;
                else
                    btn.Content = label;
            }
        }

        // ---------- Remove a keybind row ----------
        private void RemoveKeybind_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext;
            if (item == null) return;

            // Find a collection named "Keybinds" on the DataContext
            var vm = DataContext;
            if (vm == null) return;

            var keybindsProp = vm.GetType().GetProperty("Keybinds", BindingFlags.Instance | BindingFlags.Public);
            var colObj = keybindsProp?.GetValue(vm);
            var list = colObj as IList;
            if (list == null) return;

            // Remove the specific row item if it exists in the list
            if (list.Contains(item))
            {
                list.Remove(item);
            }
        }

        // ---------- General Settings hotkeys ----------
        private void NextMapKey_OnKeyDown(object sender, KeyEventArgs e)
        {
            SetHotkeyOnViewModel("NextMapHotkey", e);
        }

        private void PrevMapKey_OnKeyDown(object sender, KeyEventArgs e)
        {
            SetHotkeyOnViewModel("PrevMapHotkey", e);
        }

        private void SetHotkeyOnViewModel(string propertyName, KeyEventArgs e)
        {
            var vm = DataContext;
            if (vm == null) return;

            string label = FormatKeyboardKey(e.Key);

            var prop = vm.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(vm, label, null); } catch { /* ignore */ }
            }

            e.Handled = true;
        }

        // ---------- Helpers ----------
        private static bool TrySetStringProperty(object target, string[] propNames, string value)
        {
            if (target == null) return false;

            foreach (var name in propNames)
            {
                var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string))
                {
                    try
                    {
                        p.SetValue(target, value, null);
                        return true;
                    }
                    catch { /* ignore and try next */ }
                }
            }
            return false;
        }

        private static string FormatKeyboardKey(Key key)
        {
            // Handle NumPad and function keys nicely
            if (key >= Key.F1 && key <= Key.F24) return key.ToString(); // F1..F24
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num" + (key - Key.NumPad0);
            if (key == Key.Space) return "Space";
            if (key == Key.Return) return "Enter";
            if (key == Key.Escape) return "Esc";
            if (key == Key.LeftCtrl || key == Key.RightCtrl) return "Ctrl";
            if (key == Key.LeftAlt || key == Key.RightAlt) return "Alt";
            if (key == Key.LeftShift || key == Key.RightShift) return "Shift";
            if (key == Key.OemPlus) return "+";
            if (key == Key.OemMinus) return "-";
            if (key == Key.OemComma) return ",";
            if (key == Key.OemPeriod) return ".";
            if (key == Key.OemQuestion) return "/";
            if (key == Key.Oem1) return ";";
            if (key == Key.Oem3) return "`";
            if (key == Key.Oem5) return "\\";
            if (key == Key.Oem6) return "]";
            if (key == Key.OemOpenBrackets) return "[";

            return key.ToString();
        }

        // Small modal dialog to capture the next key or mouse click
        private class ListeningDialog : Window
        {
            private TextBlock _status;

            public string ResultLabel { get; private set; }

            public ListeningDialog()
            {
                Title = "Listening…";
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                SizeToContent = SizeToContent.WidthAndHeight;
                WindowStyle = WindowStyle.ToolWindow;
                ShowInTaskbar = false;

                var border = new Border
                {
                    Background = TryFindBrush("Brush.Surface", Brushes.DimGray),
                    BorderBrush = TryFindBrush("Brush.Border", Brushes.Gray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(18),
                    MinWidth = 260
                };

                _status = new TextBlock
                {
                    Text = "Listening… Press any key or mouse button",
                    Foreground = TryFindBrush("Brush.Text", Brushes.White),
                    Margin = new Thickness(0, 2, 0, 0)
                };

                var title = new TextBlock
                {
                    Text = "Assign keybind",
                    FontWeight = FontWeights.Bold,
                    Foreground = TryFindBrush("Brush.Text", Brushes.White)
                };

                var stack = new StackPanel();
                stack.Children.Add(title);
                stack.Children.Add(_status);
                border.Child = stack;

                Content = border;

                PreviewKeyDown += OnPreviewKeyDown;
                PreviewMouseDown += OnPreviewMouseDown;
                Deactivated += (s, e) => { /* keep open until key or mouse */ };
            }

            private static Brush TryFindBrush(string key, Brush fallback)
            {
                var res = Application.Current?.Resources[key] as Brush;
                return res ?? fallback;
            }

            private void OnPreviewKeyDown(object sender, KeyEventArgs e)
            {
                ResultLabel = "Keyboard: " + FormatKeyboardKey(e.Key);
                DialogResult = true;
                Close();
            }

            private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                string btn = e.ChangedButton.ToString();
                ResultLabel = "Mouse: " + btn;
                DialogResult = true;
                Close();
            }
        }
    }
}

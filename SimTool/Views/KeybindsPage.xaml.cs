using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimTools.Services;

namespace SimTools.Views
{
    public partial class KeybindsPage : UserControl
    {
        private DispatcherTimer _saveTimer;
        private readonly List<INotifyPropertyChanged> _trackedItems = new List<INotifyPropertyChanged>();
        private INotifyCollectionChanged _profilesCol, _mapsCol, _keybindsCol;
        private INotifyPropertyChanged _vmINPC;

        private IDisposable _inputMonitor; // global input monitor for flash-on-press

        public KeybindsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        // ---------- Lifecycle / wiring ----------
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _saveTimer.Tick += (s, args) =>
            {
                _saveTimer.Stop();
                PerformSave();
            };

            WireViewModel(DataContext);

            // Start global input monitor (flash assign button on matching input)
            var owner = Window.GetWindow(this);
            if (owner != null && _inputMonitor == null)
            {
                _inputMonitor = InputCapture.StartMonitor(owner, OnGlobalInput);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _inputMonitor?.Dispose();
            _inputMonitor = null;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnwireViewModel();
            WireViewModel(e.NewValue);
        }

        private void WireViewModel(object vm)
        {
            if (vm == null) return;

            _vmINPC = vm as INotifyPropertyChanged;
            if (_vmINPC != null)
                _vmINPC.PropertyChanged += VmOnPropertyChanged;

            // Attach to collections
            _profilesCol = GetCollection(vm, "Profiles");
            _mapsCol = GetCollection(vm, "Maps");
            _keybindsCol = GetCollection(vm, "Keybinds");

            WireCollection(_profilesCol);
            WireCollection(_mapsCol);
            WireCollection(_keybindsCol);

            QueueSave(); // ensure state is persisted soon after load
        }

        private void UnwireViewModel()
        {
            if (_vmINPC != null)
                _vmINPC.PropertyChanged -= VmOnPropertyChanged;
            _vmINPC = null;

            UnwireCollection(_profilesCol);
            UnwireCollection(_mapsCol);
            UnwireCollection(_keybindsCol);
            _profilesCol = _mapsCol = _keybindsCol = null;

            foreach (var it in _trackedItems)
                it.PropertyChanged -= ItemOnPropertyChanged;
            _trackedItems.Clear();
        }

        private void VmOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Maps" || e.PropertyName == "Keybinds" || e.PropertyName == "Profiles")
            {
                var vm = DataContext;
                var newCol = GetCollection(vm, e.PropertyName);
                if (e.PropertyName == "Profiles") { UnwireCollection(_profilesCol); _profilesCol = newCol; WireCollection(_profilesCol); }
                if (e.PropertyName == "Maps") { UnwireCollection(_mapsCol); _mapsCol = newCol; WireCollection(_mapsCol); }
                if (e.PropertyName == "Keybinds") { UnwireCollection(_keybindsCol); _keybindsCol = newCol; WireCollection(_keybindsCol); }

                QueueSave();
            }
        }

        private INotifyCollectionChanged GetCollection(object vm, string propName)
        {
            if (vm == null) return null;
            var p = vm.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(vm) as INotifyCollectionChanged;
        }

        private void WireCollection(INotifyCollectionChanged col)
        {
            if (col == null) return;
            col.CollectionChanged += OnCollectionChanged;

            var enumerable = col as IEnumerable;
            if (enumerable != null)
            {
                foreach (var obj in enumerable)
                {
                    var inpc = obj as INotifyPropertyChanged;
                    if (inpc != null && !_trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged += ItemOnPropertyChanged;
                        _trackedItems.Add(inpc);
                    }
                }
            }
        }

        private void UnwireCollection(INotifyCollectionChanged col)
        {
            if (col == null) return;
            col.CollectionChanged -= OnCollectionChanged;

            var enumerable = col as IEnumerable;
            if (enumerable != null)
            {
                foreach (var obj in enumerable)
                {
                    var inpc = obj as INotifyPropertyChanged;
                    if (inpc != null && _trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged -= ItemOnPropertyChanged;
                        _trackedItems.Remove(inpc);
                    }
                }
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                {
                    var inpc = obj as INotifyPropertyChanged;
                    if (inpc != null && !_trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged += ItemOnPropertyChanged;
                        _trackedItems.Add(inpc);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (var obj in e.OldItems)
                {
                    var inpc = obj as INotifyPropertyChanged;
                    if (inpc != null && _trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged -= ItemOnPropertyChanged;
                        _trackedItems.Remove(inpc);
                    }
                }
            }

            QueueSave();
        }

        private void ItemOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            QueueSave();
        }

        private void QueueSave()
        {
            if (_saveTimer == null) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void PerformSave()
        {
            var vm = DataContext;
            if (vm == null) return;

            var saveCmdProp = vm.GetType().GetProperty("SaveCommand", BindingFlags.Instance | BindingFlags.Public);
            var cmd = saveCmdProp?.GetValue(vm) as ICommand;
            if (cmd != null && cmd.CanExecute(null))
            {
                try { cmd.Execute(null); return; } catch { }
            }

            var saveMethod = vm.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public);
            if (saveMethod != null)
            {
                try { saveMethod.Invoke(vm, null); return; } catch { }
            }
        }

        // ---------- Assign / Clear / Remove row ----------
        private void AssignKey_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            string label = result.ToString();

            var fe = sender as FrameworkElement;
            var item = fe?.DataContext;

            // Update model (try common property names)
            TrySetStringProperty(item, new[] { "BindingLabel", "Display", "AssignedKey", "KeyName", "Key", "Binding" }, label);

            // Update the button UI immediately
            var btn = sender as Button;
            if (btn != null)
            {
                if (btn.Content is TextBlock tb) tb.Text = label;
                else btn.Content = label;

                FlashAssignButton(btn);
            }

            QueueSave();
        }

        private void ClearKey_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext;

            // Clear model property
            if (item != null)
                TrySetStringProperty(item, new[] { "BindingLabel", "Display", "AssignedKey", "KeyName", "Key", "Binding" }, "None");

            // Also clear the visible Assign button text
            var rowGrid = FindAncestor<Grid>(fe);
            if (rowGrid != null)
            {
                foreach (var child in rowGrid.Children)
                {
                    var assignBtn = child as Button;
                    if (assignBtn != null && Grid.GetColumn(assignBtn) == 1)
                    {
                        if (assignBtn.Content is TextBlock tb) tb.Text = "None";
                        else assignBtn.Content = "None";
                        break;
                    }
                }
            }

            QueueSave();
        }

        private void RemoveKeybind_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext;
            if (item == null) return;

            var vm = DataContext;
            if (vm == null) return;

            var keybindsProp = vm.GetType().GetProperty("Keybinds", BindingFlags.Instance | BindingFlags.Public);
            var colObj = keybindsProp?.GetValue(vm) as IList;
            if (colObj == null) return;

            if (colObj.Contains(item))
                colObj.Remove(item);

            QueueSave();
        }

        // ---------- Global input -> flash matching binding ----------
        private void OnGlobalInput(InputBindingResult input)
        {
            // Build a comparable label the same way Assign does
            string candidate = input?.ToString();
            if (string.IsNullOrWhiteSpace(candidate)) return;

            // Find first row whose binding string matches (best-effort contains check)
            var items = (KeybindsItems.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                string bound = GetStringProperty(item, new[] { "BindingLabel", "Display", "AssignedKey", "KeyName", "Key", "Binding" });
                if (string.IsNullOrWhiteSpace(bound)) continue;

                if (StringEqualsLoose(bound, candidate))
                {
                    // Find the container and its Assign button
                    var cp = (ContentPresenter)KeybindsItems.ItemContainerGenerator.ContainerFromItem(item);
                    if (cp == null) continue;

                    var btn = FindVisualDescendants<Button>(cp).FirstOrDefault(b => Grid.GetColumn(b) == 1);
                    if (btn != null)
                    {
                        btn.Dispatcher.Invoke(() => FlashAssignButton(btn));
                        break; // flash first match
                    }
                }
            }
        }

        private void FlashAssignButton(Button btn)
        {
            var accent = TryFindBrush("Brush.Accent", new SolidColorBrush(Color.FromRgb(80, 200, 160)));
            var normalBg = btn.Background;

            btn.Background = accent;

            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                btn.Background = normalBg;
            };
            t.Start();
        }

        // ---------- General settings hotkeys ----------
        private void NextMapKey_OnKeyDown(object sender, KeyEventArgs e) => SetHotkeyOnViewModel("NextMapHotkey", e);
        private void PrevMapKey_OnKeyDown(object sender, KeyEventArgs e) => SetHotkeyOnViewModel("PrevMapHotkey", e);

        private void SetHotkeyOnViewModel(string propertyName, KeyEventArgs e)
        {
            var vm = DataContext;
            if (vm == null) return;

            string label = FormatKeyboardKey(e.Key);
            var prop = vm.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(vm, label, null); } catch { }
            }

            e.Handled = true;
            QueueSave();
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
                    try { p.SetValue(target, value, null); return true; }
                    catch { /* try next */ }
                }
            }
            return false;
        }

        private static string GetStringProperty(object target, string[] propNames)
        {
            if (target == null) return null;
            foreach (var name in propNames)
            {
                var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(string))
                {
                    try
                    {
                        var v = p.GetValue(target) as string;
                        if (!string.IsNullOrWhiteSpace(v)) return v;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static bool StringEqualsLoose(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            // Looser compare: ignore extra spaces/casing
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Norm(string s) => (s ?? "").Replace("  ", " ").Trim();

        private static string FormatKeyboardKey(Key key)
        {
            if (key >= Key.F1 && key <= Key.F24) return key.ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num" + (key - Key.NumPad0);
            if (key == Key.Space) return "Space";
            if (key == Key.Return) return "Enter";
            if (key == Key.Escape) return "Esc";
            if (key == Key.LeftCtrl || key == Key.RightCtrl) return "Ctrl";
            if (key == Key.LeftAlt || key == Key.RightAlt) return "Alt";
            if (key == Key.LeftShift || key == Key.RightShift) return "Shift";
            return key.ToString();
        }

        private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var d in FindVisualDescendants<T>(child)) yield return d;
            }
        }

        private static Brush TryFindBrush(string key, Brush fallback)
        {
            var res = Application.Current?.Resources[key] as Brush;
            return res ?? fallback;
        }
    }
}

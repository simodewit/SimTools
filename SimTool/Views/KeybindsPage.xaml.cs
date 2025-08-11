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
            // Make sure we persist on quick exits
            try { PerformSave(); } catch { }
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

            // Update the button UI immediately + store canonical descriptor in Tag (for future matching)
            var btn = sender as Button;
            if (btn != null)
            {
                btn.Tag = result; // store full device descriptor
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

            // Also clear the visible Assign button content and Tag
            var rowGrid = FindAncestor<Grid>(fe);
            if (rowGrid != null)
            {
                foreach (var child in rowGrid.Children)
                {
                    var assignBtn = child as Button;
                    if (assignBtn != null && Grid.GetColumn(assignBtn) == 1)
                    {
                        assignBtn.Tag = null;
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
            if (input == null) return;

            // Scan visible rows and general-setting buttons for a matching device
            // Match strategy:
            //  - if both have VendorId/ProductId and they match => match
            //  - else if both are Keyboard/Mouse and ControlLabel matches => match
            //  - else if both HID and UsagePage/Usage text appears in ControlLabel => fuzzy match
            Func<InputBindingResult, InputBindingResult, bool> isMatch = (a, b) =>
            {
                if (a == null || b == null) return false;
                if (a.VendorId != 0 && b.VendorId != 0 && a.VendorId == b.VendorId && a.ProductId == b.ProductId) return true;
                if (string.Equals(a.DeviceType, "Keyboard", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.DeviceType, "Keyboard", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.ControlLabel, b.ControlLabel, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(a.DeviceType, "Mouse", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.DeviceType, "Mouse", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.ControlLabel, b.ControlLabel, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(a.DeviceType, "HID", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.DeviceType, "HID", StringComparison.OrdinalIgnoreCase))
                {
                    // Try fuzzy usage match
                    return a.ControlLabel == b.ControlLabel ||
                           (a.ControlLabel ?? "").Contains("UsagePage") && (b.ControlLabel ?? "").Contains("UsagePage");
                }
                return false;
            };

            // Keybind rows
            var items = (KeybindsList.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                var cp = (ContentPresenter)KeybindsList.ItemContainerGenerator.ContainerFromItem(item);
                if (cp == null) continue;

                var btn = FindVisualDescendants<Button>(cp).FirstOrDefault(b => Grid.GetColumn(b) == 1);
                if (btn?.Tag is InputBindingResult bound && isMatch(bound, input))
                {
                    btn.Dispatcher.Invoke(() => FlashAssignButton(btn));
                    break;
                }
            }

            // General settings buttons
            if (NextMapBtn?.Tag is InputBindingResult next && isMatch(next, input))
                NextMapBtn.Dispatcher.Invoke(() => FlashAssignButton(NextMapBtn));
            if (PrevMapBtn?.Tag is InputBindingResult prev && isMatch(prev, input))
                PrevMapBtn.Dispatcher.Invoke(() => FlashAssignButton(PrevMapBtn));
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

        // ---------- General settings (click-to-assign, same flow as rows) ----------
        private void NextMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            // Set label on VM
            TrySetStringProperty(DataContext, new[] { "NextMapHotkey", "NextMapBinding", "NextMapKey" }, result.ToString());

            // Store descriptor for future flash matching
            NextMapBtn.Tag = result;
            QueueSave();
        }

        private void PrevMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            TrySetStringProperty(DataContext, new[] { "PrevMapHotkey", "PrevMapBinding", "PrevMapKey" }, result.ToString());
            PrevMapBtn.Tag = result;
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

        private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
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

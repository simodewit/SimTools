using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimTools.Models;
using SimTools.Services;

namespace SimTools.Views
{
    public partial class KeybindsPage : UserControl
    {
        private DispatcherTimer _saveTimer;
        private readonly List<INotifyPropertyChanged> _trackedItems = new List<INotifyPropertyChanged>();
        private INotifyCollectionChanged _profilesCol, _mapsCol, _keybindsCol;
        private INotifyPropertyChanged _vmINPC;

        // Monitor from your InputCapture (may or may not fire on your setup)
        private IDisposable _inputMonitor;

        // Live highlight state (button -> its original background)
        private readonly Dictionary<Button, Brush> _litButtons = new Dictionary<Button, Brush>();

        public KeybindsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private static string DescribeRehydrated(object o)
        {
            var rb = o as RehydratedBinding;
            if (rb != null) return $"RehydratedBinding(DeviceType='{rb.DeviceType}', ControlLabel='{rb.ControlLabel}')";
            return o == null ? "null" : o.ToString();
        }
        private static string DescribeInput(InputBindingResult r)
        {
            if (r == null) return "null";
            return $"InputBindingResult(DeviceType='{r.DeviceType}', ControlLabel='{r.ControlLabel}', VendorId={r.VendorId}, ProductId={r.ProductId})";
        }

        // ---------- Lifecycle ----------
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _saveTimer.Tick += (s, args) => { _saveTimer.Stop(); PerformSave(); };

            WireViewModel(DataContext);

            // Rehydrate UI after items render
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));

            // Try global monitor
            var owner = Window.GetWindow(this);
            if (owner != null && _inputMonitor == null)
            {
                try
                {
                    _inputMonitor = InputCapture.StartMonitor(owner, OnGlobalInput);
                }
                catch (Exception) { }
            }

            // Fallback keyboard monitor (works while app has focus) — handles KeyDown + KeyUp
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try { PerformSave(); } catch { }
            _inputMonitor?.Dispose();
            _inputMonitor = null;

            InputManager.Current.PreProcessInput -= OnPreProcessInput;

            // Safety: clear any remaining highlight
            ClearAllHighlights();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnwireViewModel();
            WireViewModel(e.NewValue);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
        }

        private void WireViewModel(object vm)
        {
            if (vm == null) return;

            _vmINPC = vm as INotifyPropertyChanged;
            if (_vmINPC != null) _vmINPC.PropertyChanged += VmOnPropertyChanged;

            _profilesCol = GetCollection(vm, "Profiles");
            _mapsCol = GetCollection(vm, "Maps");
            _keybindsCol = GetCollection(vm, "Keybinds");

            WireCollection(_profilesCol);
            WireCollection(_mapsCol);
            WireCollection(_keybindsCol);

            QueueSave();
        }

        private void UnwireViewModel()
        {
            if (_vmINPC != null) _vmINPC.PropertyChanged -= VmOnPropertyChanged;
            _vmINPC = null;

            UnwireCollection(_profilesCol);
            UnwireCollection(_mapsCol);
            UnwireCollection(_keybindsCol);
            _profilesCol = _mapsCol = _keybindsCol = null;

            foreach (var it in _trackedItems) it.PropertyChanged -= ItemOnPropertyChanged;
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

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
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

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
            QueueSave();
        }

        private void ItemOnPropertyChanged(object sender, PropertyChangedEventArgs e) => QueueSave();

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

        // ---------- Keyboard fallback (only while held) ----------
        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            var ke = e.StagingItem.Input as KeyEventArgs;
            if (ke == null) return;

            // Normalize to actual key
            var key = ke.Key == Key.System ? ke.SystemKey : ke.Key;

            // Ignore weird IME keys
            if (key == Key.ImeProcessed || key == Key.DeadCharProcessed) return;

            if (ke.RoutedEvent == Keyboard.PreviewKeyDownEvent || ke.RoutedEvent == Keyboard.KeyDownEvent)
            {
                // KeyDown -> highlight matches
                var label = BuildKeyboardLabel(Keyboard.Modifiers, key);
                var synthetic = new InputBindingResult { DeviceType = "Keyboard", ControlLabel = label };
                HighlightMatches(synthetic);
            }
            else if (ke.RoutedEvent == Keyboard.PreviewKeyUpEvent || ke.RoutedEvent == Keyboard.KeyUpEvent)
            {
                // KeyUp -> clear all highlights immediately
                ClearAllHighlights();
            }
        }

        private static string BuildKeyboardLabel(ModifierKeys mods, Key key)
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

            sb.Append(keyName);
            var s = sb.ToString();
            if (s.EndsWith("+")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        // ---------- Assign / Clear / Remove row ----------
        private void AssignKey_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            var fe = sender as FrameworkElement;
            var binding = fe?.DataContext as KeybindBinding;
            if (binding == null) return;

            binding.Device = result.DeviceType;
            binding.DeviceKey = result.ControlLabel;
            binding.Key = Key.None;
            binding.Modifiers = ModifierKeys.None;

            var btn = sender as Button;
            if (btn != null)
            {
                btn.Tag = new RehydratedBinding { DeviceType = binding.Device, ControlLabel = binding.DeviceKey };
                SetButtonText(btn, binding.ToString());
            }

            QueueSave();
        }

        private void ClearKey_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var binding = fe?.DataContext as KeybindBinding;
            if (binding != null)
            {
                binding.Device = null;
                binding.DeviceKey = null;
                binding.Key = Key.None;
                binding.Modifiers = ModifierKeys.None;
            }

            var rowGrid = FindAncestor<Grid>(fe);
            if (rowGrid != null)
            {
                foreach (var child in rowGrid.Children)
                {
                    var assignBtn = child as Button;
                    if (assignBtn != null && Grid.GetColumn(assignBtn) == 1)
                    {
                        // also unhighlight if currently lit
                        Unhighlight(assignBtn);
                        assignBtn.Tag = null;
                        SetButtonText(assignBtn, "None");
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

        // ---------- Global input (treat as a "press") ----------
        private void OnGlobalInput(InputBindingResult input)
        {
            if (input == null) return;
            // Highlight matches (we don't get an explicit release from this source)
            HighlightMatches(input);

            // Optional: if your global monitor fires *without* key-up events and you
            // want to auto-clear after a short pulse, uncomment this:
            // DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            // t.Tick += (s, e) => { t.Stop(); ClearAllHighlights(); };
            // t.Start();
        }

        // ---------- Highlighting logic ----------
        private void HighlightMatches(InputBindingResult input)
        {
            // Rows
            var items = (KeybindsList.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                var container = KeybindsList.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
                if (container == null) continue;

                var btn = FindVisualDescendants<Button>(container).FirstOrDefault(b =>
                {
                    try { return Grid.GetColumn(b) == 1; } catch { return true; }
                });

                if (btn?.Tag is RehydratedBinding tag && IsMatch(tag, input))
                {
                    Highlight(btn);
                    // do not break; multiple rows could match (rare but safe)
                }
            }

            // General buttons
            if (NextMapBtn?.Tag is RehydratedBinding next && IsMatch(next, input)) Highlight(NextMapBtn);
            if (PrevMapBtn?.Tag is RehydratedBinding prev && IsMatch(prev, input)) Highlight(PrevMapBtn);
        }

        private static bool IsMatch(RehydratedBinding tag, InputBindingResult fired)
        {
            if (tag == null || fired == null) return false;
            if (string.IsNullOrWhiteSpace(tag.DeviceType) || string.IsNullOrWhiteSpace(fired.DeviceType)) return false;
            if (!tag.DeviceType.Equals(fired.DeviceType, StringComparison.OrdinalIgnoreCase)) return false;

            var a = tag.ControlLabel ?? "";
            var b = fired.ControlLabel ?? "";
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;

            // Fuzzy HID usage text
            if (a.Contains("UsagePage") && b.Contains("UsagePage")) return true;

            return false;
        }

        private void Highlight(Button btn)
        {
            if (btn == null) return;
            if (_litButtons.ContainsKey(btn)) return; // already lit

            var accent = TryFindBrush("Brush.Accent", new SolidColorBrush(Color.FromRgb(80, 200, 160)));
            _litButtons[btn] = btn.Background; // remember original
            btn.Background = accent;
        }

        private void Unhighlight(Button btn)
        {
            if (btn == null) return;
            if (_litButtons.TryGetValue(btn, out var original))
            {
                btn.Background = original;
                _litButtons.Remove(btn);
            }
        }

        private void ClearAllHighlights()
        {
            // restore backgrounds for all lit buttons
            foreach (var kv in _litButtons.ToList())
            {
                kv.Key.Background = kv.Value;
            }
            _litButtons.Clear();
        }

        // ---------- General settings assign ----------
        private void NextMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            var profile = GetCurrentProfile();
            if (profile != null)
            {
                TrySetStringProperty(profile, new[] { "NextMapDevice" }, result.DeviceType);
                TrySetStringProperty(profile, new[] { "NextMapDeviceKey" }, result.ControlLabel);
            }
            else
            {
                TrySetStringProperty(DataContext, new[] { "NextMapDevice" }, result.DeviceType);
                TrySetStringProperty(DataContext, new[] { "NextMapDeviceKey" }, result.ControlLabel);
            }

            if (NextMapBtn != null)
            {
                Unhighlight(NextMapBtn);
                NextMapBtn.Tag = new RehydratedBinding { DeviceType = result.DeviceType, ControlLabel = result.ControlLabel };
                SetButtonText(NextMapBtn, result.ToString());
            }

            QueueSave();
        }

        private void PrevMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            var profile = GetCurrentProfile();
            if (profile != null)
            {
                TrySetStringProperty(profile, new[] { "PrevMapDevice" }, result.DeviceType);
                TrySetStringProperty(profile, new[] { "PrevMapDeviceKey" }, result.ControlLabel);
            }
            else
            {
                TrySetStringProperty(DataContext, new[] { "PrevMapDevice" }, result.DeviceType);
                TrySetStringProperty(DataContext, new[] { "PrevMapDeviceKey" }, result.ControlLabel);
            }

            if (PrevMapBtn != null)
            {
                Unhighlight(PrevMapBtn);
                PrevMapBtn.Tag = new RehydratedBinding { DeviceType = result.DeviceType, ControlLabel = result.ControlLabel };
                SetButtonText(PrevMapBtn, result.ToString());
            }

            QueueSave();
        }

        // ---------- Rehydrate on load ----------
        private void RehydrateVisibleButtons()
        {
            var items = (KeybindsList?.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                var container = KeybindsList.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
                if (container == null) continue;

                var btn = FindVisualDescendants<Button>(container).FirstOrDefault(b =>
                {
                    try { return Grid.GetColumn(b) == 1; } catch { return true; }
                });
                if (btn == null) continue;

                var kb = item as KeybindBinding;
                if (kb == null) continue;

                btn.Tag = BuildDescriptorFromModel(kb);
                SetButtonText(btn, kb.ToString());
            }

            var profile = GetCurrentProfile();
            string nextDev = profile != null ? GetStringProperty(profile, "NextMapDevice") : GetStringProperty(DataContext, "NextMapDevice");
            string nextKey = profile != null ? GetStringProperty(profile, "NextMapDeviceKey") : GetStringProperty(DataContext, "NextMapDeviceKey");
            string prevDev = profile != null ? GetStringProperty(profile, "PrevMapDevice") : GetStringProperty(DataContext, "PrevMapDevice");
            string prevKey = profile != null ? GetStringProperty(profile, "PrevMapDeviceKey") : GetStringProperty(DataContext, "PrevMapDeviceKey");

            if (NextMapBtn != null)
            {
                if (!string.IsNullOrWhiteSpace(nextDev) || !string.IsNullOrWhiteSpace(nextKey))
                {
                    NextMapBtn.Tag = new RehydratedBinding { DeviceType = nextDev, ControlLabel = nextKey };
                    SetButtonText(NextMapBtn, ComposeLabel(nextDev, nextKey));
                }
                else { NextMapBtn.Tag = null; SetButtonText(NextMapBtn, "None"); }
            }

            if (PrevMapBtn != null)
            {
                if (!string.IsNullOrWhiteSpace(prevDev) || !string.IsNullOrWhiteSpace(prevKey))
                {
                    PrevMapBtn.Tag = new RehydratedBinding { DeviceType = prevDev, ControlLabel = prevKey };
                    SetButtonText(PrevMapBtn, ComposeLabel(prevDev, prevKey));
                }
                else { PrevMapBtn.Tag = null; SetButtonText(PrevMapBtn, "None"); }
            }

            // Ensure no stale highlights after rehydrate
            ClearAllHighlights();
        }

        private static string ComposeLabel(string device, string key)
        {
            if (string.IsNullOrWhiteSpace(device) && string.IsNullOrWhiteSpace(key)) return "None";
            if (string.IsNullOrWhiteSpace(device)) return key ?? "None";
            if (string.IsNullOrWhiteSpace(key)) return device;
            return device + ": " + key;
        }

        private static void SetButtonText(Button btn, string label)
        {
            if (btn.Content is TextBlock tb) tb.Text = label;
            else btn.Content = label;
        }

        private object BuildDescriptorFromModel(KeybindBinding kb)
        {
            if (kb == null) return null;

            if (string.IsNullOrWhiteSpace(kb.Device) && string.IsNullOrWhiteSpace(kb.DeviceKey) &&
                kb.Key == Key.None && kb.Modifiers == ModifierKeys.None)
                return null;

            if (!string.IsNullOrWhiteSpace(kb.Device) || !string.IsNullOrWhiteSpace(kb.DeviceKey))
            {
                return new RehydratedBinding { DeviceType = kb.Device, ControlLabel = kb.DeviceKey };
            }
            return null;
        }

        private sealed class RehydratedBinding
        {
            public string DeviceType { get; set; }
            public string ControlLabel { get; set; }
        }

        // ---------- Helpers ----------
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

        private static bool TrySetStringProperty(object target, string[] propNames, string value)
        {
            if (target == null) return false;
            foreach (var name in propNames)
            {
                var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string))
                {
                    try { p.SetValue(target, value, null); return true; } catch { }
                }
            }
            return false;
        }

        private static string GetStringProperty(object target, string name)
        {
            var p = target?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(target) as string;
        }

        private object GetCurrentProfile()
        {
            var vm = DataContext;
            if (vm == null) return null;

            foreach (var name in new[] { "SelectedProfile", "CurrentProfile" })
            {
                var p = vm.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null) return p.GetValue(vm);
            }
            return null;
        }

        private void KeybindName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var bindingExpr = (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty);
                bindingExpr?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}

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

        // --- Hotkey debounce for map switching ---
        private DateTime _lastNextFiredUtc = DateTime.MinValue;
        private DateTime _lastPrevFiredUtc = DateTime.MinValue;
        private static readonly TimeSpan _hotkeyDebounce = TimeSpan.FromMilliseconds(250);

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

            // Fallback keyboard monitor (works while app has focus)
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try { PerformSave(); } catch { }
            _inputMonitor?.Dispose();
            _inputMonitor = null;

            InputManager.Current.PreProcessInput -= OnPreProcessInput;

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

            var key = ke.Key == Key.System ? ke.SystemKey : ke.Key;
            if (key == Key.ImeProcessed || key == Key.DeadCharProcessed) return;

            if (ke.RoutedEvent == Keyboard.PreviewKeyDownEvent || ke.RoutedEvent == Keyboard.KeyDownEvent)
            {
                var label = BuildKeyboardLabel(Keyboard.Modifiers, key);
                var synthetic = new InputBindingResult { DeviceType = "Keyboard", ControlLabel = label };
                HighlightMatches(synthetic);

                if (!ke.IsRepeat) MaybeSwitchMapFrom(synthetic);
            }
            else if (ke.RoutedEvent == Keyboard.PreviewKeyUpEvent || ke.RoutedEvent == Keyboard.KeyUpEvent)
            {
                ClearRowHighlights(); // keep global Next/Prev highlight intact
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
            HighlightMatches(input);
            MaybeSwitchMapFrom(input);
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

            if (a.Contains("UsagePage") && b.Contains("UsagePage")) return true;

            return false;
        }

        private void Highlight(Button btn)
        {
            if (btn == null) return;
            if (_litButtons.ContainsKey(btn)) return;

            var accent = TryFindBrush("Brush.Accent", new SolidColorBrush(Color.FromRgb(80, 200, 160)));
            _litButtons[btn] = btn.Background;
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
            foreach (var kv in _litButtons.ToList())
            {
                kv.Key.Background = kv.Value;
                _litButtons.Remove(kv.Key);
            }
        }

        // NEW: clear only row highlights (keep Next/Prev highlight persistent through map switch)
        private void ClearRowHighlights()
        {
            foreach (var kv in _litButtons.ToList())
            {
                if (kv.Key == NextMapBtn || kv.Key == PrevMapBtn)
                    continue;

                kv.Key.Background = kv.Value;
                _litButtons.Remove(kv.Key);
            }
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

            // Only drop row highlights on rehydrate; keep global Next/Prev if active
            ClearRowHighlights();
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

        // ---------- Hotkey → VM switching ----------
        private void MaybeSwitchMapFrom(InputBindingResult input)
        {
            bool matchNext = NextMapBtn?.Tag is RehydratedBinding next && IsMatch(next, input);
            bool matchPrev = PrevMapBtn?.Tag is RehydratedBinding prev && IsMatch(prev, input);

            if (matchNext && ShouldFire(ref _lastNextFiredUtc)) TriggerNextMap();
            if (matchPrev && ShouldFire(ref _lastPrevFiredUtc)) TriggerPrevMap();
        }

        private static bool ShouldFire(ref DateTime lastFiredUtc)
        {
            var now = DateTime.UtcNow;
            if ((now - lastFiredUtc) < _hotkeyDebounce) return false;
            lastFiredUtc = now;
            return true;
        }

        private void TriggerNextMap()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(TriggerNextMap)); return; }
            Highlight(NextMapBtn);
            ExecuteMapSwitchCommand(+1);
        }

        private void TriggerPrevMap()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(TriggerPrevMap)); return; }
            Highlight(PrevMapBtn);
            ExecuteMapSwitchCommand(-1);
        }

        private void ExecuteMapSwitchCommand(int delta)
        {
            var vm = DataContext;
            if (vm == null) return;

            if (delta > 0)
            {
                if (TryExecuteCommand(vm, "NextMapCommand")) return;
                if (TryInvoke(vm, "SwitchToNextMap")) return;
                if (TryInvoke(vm, "SelectNextMap")) return;
                if (TryInvoke(vm, "NextMap")) return;
            }
            else
            {
                if (TryExecuteCommand(vm, "PrevMapCommand")) return;
                if (TryExecuteCommand(vm, "PreviousMapCommand")) return;
                if (TryInvoke(vm, "SwitchToPreviousMap")) return;
                if (TryInvoke(vm, "SelectPreviousMap")) return;
                if (TryInvoke(vm, "PrevMap")) return;
            }
        }

        private static bool TryExecuteCommand(object target, string propertyName)
        {
            var p = target?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            var cmd = p?.GetValue(target) as ICommand;
            if (cmd != null && cmd.CanExecute(null)) { cmd.Execute(null); return true; }
            return false;
        }

        private static bool TryInvoke(object target, string methodName)
        {
            var m = target?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (m != null) { try { m.Invoke(target, null); return true; } catch { } }
            return false;
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

        // ---------- NEW: Rename (profiles / maps) ----------
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            BeginInlineRename(ProfilesList, "ProfileNameEditor", "ProfileNameText");
        }

        private void RenameMap_Click(object sender, RoutedEventArgs e)
        {
            BeginInlineRename(MapsList, "MapNameEditor", "MapNameText");
        }

        private void BeginInlineRename(ListBox listBox, string editorName, string textName)
        {
            if (listBox == null || listBox.SelectedItem == null) return;

            var container = listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) as DependencyObject;
            if (container == null) return;

            var editor = FindVisualDescendants<TextBox>(container).FirstOrDefault(tb => tb.Name == editorName);
            var label = FindVisualDescendants<TextBlock>(container).FirstOrDefault(tb => tb.Name == textName);
            if (editor == null || label == null) return;

            label.Visibility = Visibility.Collapsed;
            editor.Visibility = Visibility.Visible;

            editor.Focus();
            editor.SelectAll();
        }

        private void ProfileNameEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitAndCloseEditor(sender as TextBox);
                e.Handled = true;
            }
        }

        private void MapNameEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitAndCloseEditor(sender as TextBox);
                e.Handled = true;
            }
        }

        private void ProfileNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitAndCloseEditor(sender as TextBox);
        }

        private void MapNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitAndCloseEditor(sender as TextBox);
        }

        private void CommitAndCloseEditor(TextBox editor)
        {
            if (editor == null) return;

            // Update the bound Name property
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            // Hide editor, show label
            var grid = editor.Parent as Grid;
            var label = grid?.Children.OfType<TextBlock>().FirstOrDefault();
            if (label != null)
            {
                editor.Visibility = Visibility.Collapsed;
                label.Visibility = Visibility.Visible;
            }

            Keyboard.ClearFocus();
            QueueSave();
        }
    }
}

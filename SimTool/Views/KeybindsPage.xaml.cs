using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;

namespace SimTools.Views
{
    public partial class KeybindsPage : UserControl
    {
        // Services
        private DebouncedSaver _saver;
        private VmCollectionBinder _binder;
        private ButtonHighlightService _lights;
        private MapHotkeyController _hotkeys;

        // NEW: safe game-mode pieces
        private VirtualGamepadService _gamepad;
        private GameModeGuard _guard;

        // Monitor from your InputCapture (may or may not fire on your setup)
        private IDisposable _inputMonitor;

        // Tap duration for virtual button (ms) when we don't get release notifications.
        private const int TapMs = 50;

        public KeybindsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _lights = new ButtonHighlightService();
            _hotkeys = new MapHotkeyController(Dispatcher, _lights);

            _saver = new DebouncedSaver(PerformSave);
            _binder = new VmCollectionBinder("Profiles", "Maps", "Keybinds");
            _binder.Changed += (_, __) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
                _saver.Queue();
            };
            _binder.Wire(DataContext);

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));

            // Global input from your InputCapture (safe for games; no keyboard injection)
            var owner = Window.GetWindow(this);
            if (owner != null && _inputMonitor == null)
            {
                try { _inputMonitor = InputCapture.StartMonitor(owner, OnGlobalInput); }
                catch { /* monitor not available on some setups */ }
            }

            // Keyboard fallback (only while app has focus) – for UI hints, not game routing
            InputManager.Current.PreProcessInput += OnPreProcessInput;

            // NEW: start ViGEm controller + guard
            _gamepad = new VirtualGamepadService();
            _gamepad.TryStart();

            _guard = new GameModeGuard { GameModeEnabled = true }; // Game Mode ON by default
            _guard.StatusChanged += (suspended, exe) =>
            {
                // Optional: show a small banner/toast if suspended by a process.
            };
            _guard.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try { PerformSave(); } catch { }
            _inputMonitor?.Dispose();
            _inputMonitor = null;

            InputManager.Current.PreProcessInput -= OnPreProcessInput;

            _binder?.Unwire();
            _binder = null;

            _saver?.Dispose();
            _saver = null;

            _lights?.ClearAll();

            try { _guard?.Dispose(); } catch { }
            try { _gamepad?.Stop(); } catch { }
            _guard = null; _gamepad = null;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _binder?.Wire(e.NewValue);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
        }

        private void QueueSave() => _saver?.Queue();

        private void PerformSave()
        {
            var vm = DataContext;
            if (vm == null) return;

            var saveCmd = vm.GetType().GetProperty("SaveCommand", BindingFlags.Instance | BindingFlags.Public)?.GetValue(vm) as ICommand;
            if (saveCmd != null && saveCmd.CanExecute(null))
            { try { saveCmd.Execute(null); return; } catch { } }

            var saveMethod = vm.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public);
            if (saveMethod != null) { try { saveMethod.Invoke(vm, null); } catch { } }
        }

        // ---------- Keyboard fallback (only while held; UI highlighting) ----------
        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!(e.StagingItem.Input is KeyEventArgs ke)) return;

            var key = ke.Key == Key.System ? ke.SystemKey : ke.Key;
            if (key == Key.ImeProcessed || key == Key.DeadCharProcessed) return;

            if (ke.RoutedEvent == Keyboard.PreviewKeyDownEvent || ke.RoutedEvent == Keyboard.KeyDownEvent)
            {
                var label = KeybindHelpers.BuildKeyboardLabel(Keyboard.Modifiers, key);
                var synthetic = new InputBindingResult { DeviceType = "Keyboard", ControlLabel = label };
                HighlightMatches(synthetic);

                if (!ke.IsRepeat && !KeybindHelpers.IsTyping())
                    MaybeSwitchMapFrom(synthetic);
            }
            else if (ke.RoutedEvent == Keyboard.PreviewKeyUpEvent || ke.RoutedEvent == Keyboard.KeyUpEvent)
            {
                _lights.ClearAll();
                _lights.Unhighlight(NextMapBtn);
                _lights.Unhighlight(PrevMapBtn);
            }
        }

        // ---------- Assign / Clear / Remove row ----------
        private void AssignKey_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            if (!((sender as FrameworkElement)?.DataContext is KeybindBinding binding)) return;

            binding.Device = result.DeviceType;
            binding.DeviceKey = result.ControlLabel;
            binding.Key = Key.None;
            binding.Modifiers = ModifierKeys.None;

            if (sender is Button btn)
            {
                btn.Tag = new DeviceBindingDescriptor { DeviceType = binding.Device, ControlLabel = binding.DeviceKey };
                KeybindUiHelpers.SetButtonText(btn, binding.ToString());
            }

            QueueSave();
        }

        private void ClearKey_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is KeybindBinding binding)) return;

            binding.Device = null;
            binding.DeviceKey = null;
            binding.Key = Key.None;
            binding.Modifiers = ModifierKeys.None;

            var rowGrid = KeybindUiHelpers.FindAncestor<Grid>(sender as FrameworkElement);
            if (rowGrid != null)
            {
                foreach (var child in rowGrid.Children)
                {
                    if (child is Button assignBtn && Grid.GetColumn(assignBtn) == 1)
                    {
                        _lights.Unhighlight(assignBtn);
                        assignBtn.Tag = null;
                        KeybindUiHelpers.SetButtonText(assignBtn, "None");
                        break;
                    }
                }
            }

            QueueSave();
        }

        private void RemoveKeybind_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null) return;

            var col = ReflectionUtils.GetListProperty(DataContext, "Keybinds");
            if (col != null && col.Contains(item))
                col.Remove(item);

            QueueSave();
        }

        // ---------- Global input callback ----------
        private void OnGlobalInput(InputBindingResult input)
        {
            if (input == null) return;

            // UI highlighting (visual hint)
            HighlightMatches(input);

            // Map switching via Next/Prev on any device
            if (!KeybindHelpers.IsTyping())
                MaybeSwitchMapFrom(input);

            // Game Mode: route 1:1 to ViGEm (no hooks, no SendInput)
            TryRouteToVirtualTap(input);
        }

        // Route this input to a single virtual button (tap), if bound in the current map.
        private void TryRouteToVirtualTap(InputBindingResult input)
        {
            if (_gamepad == null || !_guard?.ShouldOperateNow() == true) return;

            var items = (KeybindsList?.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var kb in items.OfType<KeybindBinding>())
            {
                if (kb == null || kb.Output == VirtualOutput.None) continue;

                // Match on the binding's device + control label (case-insensitive)
                if (string.Equals(kb.Device, input.DeviceType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(kb.DeviceKey, input.ControlLabel, StringComparison.OrdinalIgnoreCase))
                {
                    TapVirtual(kb.Output, TapMs);
                    break; // one input -> one output
                }
            }
        }

        private void TapVirtual(VirtualOutput output, int durationMs)
        {
            try
            {
                _gamepad?.SetButton(output, true);

                var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(durationMs) };
                t.Tick += (s, e) =>
                {
                    try { _gamepad?.SetButton(output, false); } catch { }
                    (s as DispatcherTimer)?.Stop();
                };
                t.Start();
            }
            catch { /* never throw on input path */ }
        }

        // ---------- General settings assign (unchanged) ----------
        private void NextMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            var profile = ReflectionUtils.GetCurrentProfile(DataContext) ?? DataContext;
            ReflectionUtils.TrySetStringProperty(profile, new[] { "NextMapDevice" }, result.DeviceType);
            ReflectionUtils.TrySetStringProperty(profile, new[] { "NextMapDeviceKey" }, result.ControlLabel);

            if (NextMapBtn != null)
            {
                _lights.Unhighlight(NextMapBtn);
                NextMapBtn.Tag = new DeviceBindingDescriptor { DeviceType = result.DeviceType, ControlLabel = result.ControlLabel };
                KeybindUiHelpers.SetButtonText(NextMapBtn, result.ToString());
            }

            QueueSave();
        }

        private void PrevMapAssign_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = InputCapture.CaptureBinding(owner);
            if (result == null) return;

            var profile = ReflectionUtils.GetCurrentProfile(DataContext) ?? DataContext;
            ReflectionUtils.TrySetStringProperty(profile, new[] { "PrevMapDevice" }, result.DeviceType);
            ReflectionUtils.TrySetStringProperty(profile, new[] { "PrevMapDeviceKey" }, result.ControlLabel);

            if (PrevMapBtn != null)
            {
                _lights.Unhighlight(PrevMapBtn);
                PrevMapBtn.Tag = new DeviceBindingDescriptor { DeviceType = result.DeviceType, ControlLabel = result.ControlLabel };
                KeybindUiHelpers.SetButtonText(PrevMapBtn, result.ToString());
            }

            QueueSave();
        }

        private void NextMapClear_Click(object sender, RoutedEventArgs e)
        {
            var profile = ReflectionUtils.GetCurrentProfile(DataContext) ?? DataContext;
            ReflectionUtils.TrySetStringProperty(profile, new[] { "NextMapDevice" }, null);
            ReflectionUtils.TrySetStringProperty(profile, new[] { "NextMapDeviceKey" }, null);

            if (NextMapBtn != null)
            {
                _lights.Unhighlight(NextMapBtn);
                NextMapBtn.Tag = null;
                KeybindUiHelpers.SetButtonText(NextMapBtn, "None");
            }
            QueueSave();
        }

        private void PrevMapClear_Click(object sender, RoutedEventArgs e)
        {
            var profile = ReflectionUtils.GetCurrentProfile(DataContext) ?? DataContext;
            ReflectionUtils.TrySetStringProperty(profile, new[] { "PrevMapDevice" }, null);
            ReflectionUtils.TrySetStringProperty(profile, new[] { "PrevMapDeviceKey" }, null);

            if (PrevMapBtn != null)
            {
                _lights.Unhighlight(PrevMapBtn);
                PrevMapBtn.Tag = null;
                KeybindUiHelpers.SetButtonText(PrevMapBtn, "None");
            }
            QueueSave();
        }

        private void RehydrateVisibleButtons()
        {
            KeybindRehydrator.RehydrateRows(KeybindsList);

            var profile = ReflectionUtils.GetCurrentProfile(DataContext);
            string nextDev = profile != null ? ReflectionUtils.GetStringProperty(profile, "NextMapDevice") : ReflectionUtils.GetStringProperty(DataContext, "NextMapDevice");
            string nextKey = profile != null ? ReflectionUtils.GetStringProperty(profile, "NextMapDeviceKey") : ReflectionUtils.GetStringProperty(DataContext, "NextMapDeviceKey");
            string prevDev = profile != null ? ReflectionUtils.GetStringProperty(profile, "PrevMapDevice") : ReflectionUtils.GetStringProperty(DataContext, "PrevMapDevice");
            string prevKey = profile != null ? ReflectionUtils.GetStringProperty(profile, "PrevMapDeviceKey") : ReflectionUtils.GetStringProperty(DataContext, "PrevMapDeviceKey");

            if (NextMapBtn != null)
            {
                if (!string.IsNullOrWhiteSpace(nextDev) || !string.IsNullOrWhiteSpace(nextKey))
                {
                    NextMapBtn.Tag = new DeviceBindingDescriptor { DeviceType = nextDev, ControlLabel = nextKey };
                    KeybindUiHelpers.SetButtonText(NextMapBtn, new DeviceBindingDescriptor { DeviceType = nextDev, ControlLabel = nextKey }.ToString());
                }
                else { NextMapBtn.Tag = null; KeybindUiHelpers.SetButtonText(NextMapBtn, "None"); }
            }

            if (PrevMapBtn != null)
            {
                if (!string.IsNullOrWhiteSpace(prevDev) || !string.IsNullOrWhiteSpace(prevKey))
                {
                    PrevMapBtn.Tag = new DeviceBindingDescriptor { DeviceType = prevDev, ControlLabel = prevKey };
                    KeybindUiHelpers.SetButtonText(PrevMapBtn, new DeviceBindingDescriptor { DeviceType = prevDev, ControlLabel = prevKey }.ToString());
                }
                else { PrevMapBtn.Tag = null; KeybindUiHelpers.SetButtonText(PrevMapBtn, "None"); }
            }

            _lights.ClearRowsExcept(NextMapBtn, PrevMapBtn);
        }

        private void MaybeSwitchMapFrom(InputBindingResult input)
        {
            var nextTag = NextMapBtn?.Tag as DeviceBindingDescriptor;
            var prevTag = PrevMapBtn?.Tag as DeviceBindingDescriptor;

            _hotkeys.OnInput(
                input,
                nextTag,
                prevTag,
                nextCmd: () => ExecuteMapSwitchCommand(+1),
                prevCmd: () => ExecuteMapSwitchCommand(-1),
                nextBtn: NextMapBtn,
                prevBtn: PrevMapBtn
            );
        }

        private void ExecuteMapSwitchCommand(int delta)
        {
            var vm = DataContext;
            if (vm == null) return;

            if (delta > 0)
            {
                if (ReflectionUtils.TryExecuteCommand(vm, "NextMapCommand")) return;
                if (ReflectionUtils.TryInvoke(vm, "SwitchToNextMap")) return;
                if (ReflectionUtils.TryInvoke(vm, "SelectNextMap")) return;
                if (ReflectionUtils.TryInvoke(vm, "NextMap")) return;
            }
            else
            {
                if (ReflectionUtils.TryExecuteCommand(vm, "PrevMapCommand")) return;
                if (ReflectionUtils.TryExecuteCommand(vm, "PreviousMapCommand")) return;
                if (ReflectionUtils.TryInvoke(vm, "SwitchToPreviousMap")) return;
                if (ReflectionUtils.TryInvoke(vm, "SelectPreviousMap")) return;
                if (ReflectionUtils.TryInvoke(vm, "PrevMap")) return;
            }
        }

        // Inline rename + enter-to-commit (unchanged from your version)
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
            => BeginInlineRename(ProfilesList, "ProfileNameEditor", "ProfileNameText");
        private void RenameMap_Click(object sender, RoutedEventArgs e)
            => BeginInlineRename(MapsList, "MapNameEditor", "MapNameText");
        private void BeginInlineRename(ListBox listBox, string editorName, string textName)
        {
            if (listBox == null || listBox.SelectedItem == null) return;
            var container = listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) as DependencyObject;
            if (container == null) return;

            var editor = KeybindUiHelpers.FindVisualDescendants<TextBox>(container).FirstOrDefault(tb => tb.Name == editorName);
            var label = KeybindUiHelpers.FindVisualDescendants<TextBlock>(container).FirstOrDefault(tb => tb.Name == textName);
            if (editor == null || label == null) return;

            label.Visibility = Visibility.Collapsed;
            editor.Visibility = Visibility.Visible;

            editor.Focus();
            editor.SelectAll();
        }
        private void ProfileNameEditor_KeyDown(object sender, KeyEventArgs e)
        { if (e.Key == Key.Enter) { CommitAndCloseEditor(sender as TextBox); e.Handled = true; } }
        private void MapNameEditor_KeyDown(object sender, KeyEventArgs e)
        { if (e.Key == Key.Enter) { CommitAndCloseEditor(sender as TextBox); e.Handled = true; } }
        private void ProfileNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
            => CommitAndCloseEditor(sender as TextBox);
        private void MapNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
            => CommitAndCloseEditor(sender as TextBox);
        private void CommitAndCloseEditor(TextBox editor)
        {
            if (editor == null) return;
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

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
        private void KeybindName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        // Highlights the "Assign" button in any row whose binding matches the incoming input.
        // Used for visual feedback when an input is detected.
        private void HighlightMatches(InputBindingResult input)
        {
            var items = (KeybindsList?.ItemsSource as System.Collections.IEnumerable)
                        ?? System.Linq.Enumerable.Empty<object>();

            foreach (var item in items)
            {
                var container = KeybindsList.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.DependencyObject;
                if (container == null) continue;

                // Find the Assign button in column 1 for this row
                var btn = SimTools.Helpers.KeybindUiHelpers
                    .FindVisualDescendants<System.Windows.Controls.Button>(container)
                    .FirstOrDefault(b =>
                    {
                        try { return System.Windows.Controls.Grid.GetColumn(b) == 1; }
                        catch { return true; }
                    });

                // During assignment/rehydration we store a DeviceBindingDescriptor in the Assign button's Tag
                if (btn?.Tag is SimTools.Models.DeviceBindingDescriptor tag &&
                    SimTools.Models.DeviceBindingDescriptor.IsMatch(tag, input))
                {
                    _lights?.Highlight(btn);
                }
            }
        }
    }
}

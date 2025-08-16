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

        // Monitor from your InputCapture (may or may not fire on your setup)
        private IDisposable _inputMonitor;

        public KeybindsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        // ---------- Lifecycle ----------
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

            // Initial rehydrate after UI renders
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));

            // Try global monitor
            var owner = Window.GetWindow(this);
            if (owner != null && _inputMonitor == null)
            {
                try { _inputMonitor = InputCapture.StartMonitor(owner, OnGlobalInput); }
                catch { /* monitor not available on some setups */ }
            }

            // Fallback keyboard monitor (while app has focus)
            InputManager.Current.PreProcessInput += OnPreProcessInput;
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
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _binder?.Wire(e.NewValue);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RehydrateVisibleButtons));
        }

        // ---------- Save ----------
        private void QueueSave() => _saver?.Queue();

        private void PerformSave()
        {
            var vm = DataContext;
            if (vm == null) return;

            // Prefer SaveCommand; fall back to Save()
            var saveCmd = vm.GetType().GetProperty("SaveCommand", BindingFlags.Instance | BindingFlags.Public)?.GetValue(vm) as ICommand;
            if (saveCmd != null && saveCmd.CanExecute(null))
            { try { saveCmd.Execute(null); return; } catch { } }

            var saveMethod = vm.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public);
            if (saveMethod != null) { try { saveMethod.Invoke(vm, null); } catch { } }
        }

        // ---------- Keyboard fallback (only while held) ----------
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
                // Turn off ALL highlights, including the general Next/Prev buttons.
                _lights.ClearAll();

                // (extra safety; harmless if already cleared)
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

        // ---------- Global input (treat as a "press") ----------
        private void OnGlobalInput(InputBindingResult input)
        {
            if (input == null) return;
            HighlightMatches(input);

            if (!KeybindHelpers.IsTyping())
                MaybeSwitchMapFrom(input);
        }

        // ---------- Highlighting logic ----------
        private void HighlightMatches(InputBindingResult input)
        {
            var items = (KeybindsList?.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                var container = KeybindsList.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
                if (container == null) continue;

                var btn = KeybindUiHelpers.FindVisualDescendants<Button>(container).FirstOrDefault(b =>
                {
                    try { return Grid.GetColumn(b) == 1; } catch { return true; }
                });

                if (btn?.Tag is DeviceBindingDescriptor tag && DeviceBindingDescriptor.IsMatch(tag, input))
                    _lights.Highlight(btn);
            }
        }

        // ---------- General settings assign ----------
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

        // ---------- Rehydrate on load ----------
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

            // Clear only row highlights (leave Next/Prev if active)
            _lights.ClearRowsExcept(NextMapBtn, PrevMapBtn);
        }

        // ---------- Hotkey → VM switching ----------
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

        // ---------- Inline rename ----------
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
        {
            if (e.Key == Key.Enter) { CommitAndCloseEditor(sender as TextBox); e.Handled = true; }
        }

        private void MapNameEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitAndCloseEditor(sender as TextBox); e.Handled = true; }
        }

        private void ProfileNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
            => CommitAndCloseEditor(sender as TextBox);

        private void MapNameEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
            => CommitAndCloseEditor(sender as TextBox);

        private void CommitAndCloseEditor(TextBox editor)
        {
            if (editor == null) return;
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

        // Handles Enter-to-commit for the keybind name TextBox
        private void KeybindName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (sender as TextBox)?
                    .GetBindingExpression(TextBox.TextProperty)?
                    .UpdateSource();

                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}

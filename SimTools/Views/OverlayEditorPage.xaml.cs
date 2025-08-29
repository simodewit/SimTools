using Microsoft.Win32;
using SimTools.ViewModels;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SimTools.Views
{
    public partial class OverlayEditorPage : UserControl
    {
        private enum PreviewBackgroundKind { Asphalt, SolidGray, SolidDark, CustomImage }
        private PreviewBackgroundKind _bgKind = PreviewBackgroundKind.Asphalt;
        private ImageBrush _customImageBrush;

        private FrameworkElement _currentToolRoot;

        private bool _isDraggingInspector;
        private Point _dragStartMouseInPopup;
        private Point _dragStartPopupOffset;

        private bool _suppressSelection;

        public OverlayEditorPage()
        {
            InitializeComponent();
            Loaded += OverlayEditorPage_Loaded;
        }

        private OverlayEditorViewModel VM => DataContext as OverlayEditorViewModel;

        private void OverlayEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Asphalt (or fallback) background
            ApplyAsphaltOrFallback();

            // Build initial preview for selected tool
            RefreshToolPreview();

            // React to SelectedTool change
            if (VM is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(OverlayEditorViewModel.SelectedTool))
                        RefreshToolPreview();
                };
            }
        }

        private void ApplyAsphaltOrFallback()
        {
            // Try a couple of common resource pack URIs
            if (TrySetImageBackground("pack://application:,,,/SimTool;component/Images/Asphalt.png")) return;
            if (TrySetImageBackground("pack://application:,,,/Images/Asphalt.png")) return;

            // Fallback: flat surface from theme
            PreviewBackdrop.Background = (Brush)FindResource("Brush.SurfaceAlt");
            _bgKind = PreviewBackgroundKind.SolidGray;
        }

        private bool TrySetImageBackground(string packUri)
        {
            try
            {
                var uri = new Uri(packUri, UriKind.Absolute);
                var img = new BitmapImage(uri);
                var brush = new ImageBrush(img)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
                PreviewBackdrop.Background = brush;
                _bgKind = PreviewBackgroundKind.Asphalt;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshToolPreview()
        {
            _currentToolRoot = null;

            if (VM?.SelectedTool == null) { ToolHost.Content = null; return; }

            var view = VM.SelectedTool.BuildPreview?.Invoke();
            if (view != null)
            {
                // we want to: (1) allow selecting any element as before,
                // and (2) allow clicking the tool's BACKGROUND (container) even if content fills width.
                _currentToolRoot = view;

                // 1) As before: clicking any element opens inspector for that element
                view.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(PreviewElement_MouseLeftButtonUp), handledEventsToo: true);

                // 2) Special: if click lands on the container background itself, select the container
                view.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(ToolRoot_PreviewMouseLeftButtonUp), handledEventsToo: true);
            }

            ToolHost.Content = view;
        }

        private void PreviewElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                e.Handled = true;
                ShowInspectorFor(d, e.GetPosition(PreviewSurface));
            }
        }

        private void ToolRoot_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentToolRoot == null) return;

            var pos = e.GetPosition(_currentToolRoot);

            FrameworkElement top = null;
            VisualTreeHelper.HitTest(_currentToolRoot, null,
                result =>
                {
                    if (result.VisualHit is FrameworkElement fe)
                    {
                        top = fe;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pos));

            e.Handled = true;

            if (ReferenceEquals(top, _currentToolRoot))
            {
                ShowInspectorFor(_currentToolRoot, e.GetPosition(PreviewSurface));
                return;
            }

            if (top != null)
                ShowInspectorFor(top, e.GetPosition(PreviewSurface));
        }

        private void ToolTab_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (sender is ToggleButton t && t.Tag is OverlayToolDescriptor desc && !ReferenceEquals(VM.SelectedTool, desc))
            {
                VM.SelectedTool = desc;
                RefreshToolPreview();
            }
        }

        private void PreviewElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_suppressSelection || _currentToolRoot == null) { e.Handled = true; return; }

            // Find the element that was actually clicked
            var d = e.OriginalSource as DependencyObject;
            FrameworkElement fe = null;

            // Walk up until we find a FrameworkElement (and ensure it belongs to the tool)
            while (d != null)
            {
                if (ReferenceEquals(d, _currentToolRoot)) { fe = _currentToolRoot; break; }
                if (d is FrameworkElement f) { fe = f; break; }
                d = VisualTreeHelper.GetParent(d);
            }

            // Only allow selections inside the current tool root
            if (fe != null)
            {
                // Make sure the element lives under the tool root
                DependencyObject p = fe;
                bool withinTool = false;
                while (p != null)
                {
                    if (ReferenceEquals(p, _currentToolRoot)) { withinTool = true; break; }
                    p = VisualTreeHelper.GetParent(p);
                }
                if (!withinTool) { e.Handled = true; return; }

                e.Handled = true;
                ShowInspectorFor(fe, e.GetPosition(PreviewSurface));
            }
        }

        private void ToolRoot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_suppressSelection || _currentToolRoot == null) { e.Handled = true; return; }

            // Where was the click relative to the tool root?
            var pos = e.GetPosition(_currentToolRoot);

            // Hit test to get the topmost element at that point within the tool root
            FrameworkElement top = null;
            VisualTreeHelper.HitTest(_currentToolRoot, null,
                result =>
                {
                    if (result.VisualHit is FrameworkElement fe)
                    {
                        top = fe;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pos));

            // If the topmost element is the root container itself, treat it as a "background click" (still the tool)
            if (ReferenceEquals(top, _currentToolRoot))
            {
                e.Handled = true;
                ShowInspectorFor(_currentToolRoot, e.GetPosition(PreviewSurface));
                return;
            }

            if (top != null)
            {
                e.Handled = true;
                ShowInspectorFor(top, e.GetPosition(PreviewSurface));
            }
        }

        private void PreviewSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(PreviewSurface);
            DependencyObject found = null;

            VisualTreeHelper.HitTest(PreviewSurface, null,
                r =>
                {
                    if (r.VisualHit is FrameworkElement fe) { found = fe; return HitTestResultBehavior.Stop; }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pos));

            if (found != null)
            {
                e.Handled = true;
                ShowInspectorFor(found, pos);
            }
        }

        private void PreviewSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_suppressSelection || _currentToolRoot == null) { e.Handled = true; return; }

            // Try hit-testing INSIDE the tool root only
            var pos = e.GetPosition(_currentToolRoot);
            FrameworkElement found = null;

            VisualTreeHelper.HitTest(_currentToolRoot, null,
                r =>
                {
                    if (r.VisualHit is FrameworkElement fe) { found = fe; return HitTestResultBehavior.Stop; }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pos));

            if (found != null)
            {
                e.Handled = true;
                ShowInspectorFor(found, e.GetPosition(PreviewSurface));
            }
            else
            {
                // No selection if click is outside the tool — do NOT select backdrop/panels
                e.Handled = true;
            }
        }

        private void ShowInspectorFor(DependencyObject obj, Point? openAt = null)
        {
            if (VM == null) return;

            VM.SelectedVisual = obj;
            Inspector.SelectedObject = obj;

            var p = openAt ?? Mouse.GetPosition(PreviewSurface);

            // Defer until after the current input event completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InspectorPopup.HorizontalOffset = p.X + 8;  // small nudge
                InspectorPopup.VerticalOffset = p.Y + 8;
                InspectorPopup.IsOpen = true;
            }), DispatcherPriority.Input);
        }

        private void CloseInspector_Click(object sender, RoutedEventArgs e)
        {
            InspectorPopup.IsOpen = false;
        }

        private void BackgroundSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(BackgroundSelector.SelectedItem is ComboBoxItem item)) return;
            var label = (item.Content ?? "").ToString().ToLowerInvariant();

            if (label.StartsWith("asphalt"))
            {
                ApplyAsphaltOrFallback();
                return;
            }

            if (label.StartsWith("solid gray"))
            {
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
                _bgKind = PreviewBackgroundKind.SolidGray;
                return;
            }

            if (label.StartsWith("solid dark"))
            {
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                _bgKind = PreviewBackgroundKind.SolidDark;
                return;
            }

            if (label.StartsWith("choose image"))
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Choose background image",
                    Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        var img = new BitmapImage();
                        using (var fs = File.OpenRead(dlg.FileName))
                        {
                            img.BeginInit();
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.StreamSource = fs;
                            img.EndInit();
                            img.Freeze();
                        }
                        _customImageBrush = new ImageBrush(img)
                        {
                            Stretch = Stretch.UniformToFill,
                            AlignmentX = AlignmentX.Center,
                            AlignmentY = AlignmentY.Center
                        };
                        PreviewBackdrop.Background = _customImageBrush;
                        _bgKind = PreviewBackgroundKind.CustomImage;
                    }
                    catch
                    {
                        MessageBox.Show("Could not load that image. Please pick a different file.", "Background", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void OpenOverlay_Click(object sender, RoutedEventArgs e)
        {
            // Relay to existing VM command
            VM?.OpenOverlay?.Execute(null);
        }

        #region Inspector dragging
        private void InspectorHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!InspectorPopup.IsOpen) return;
            _isDraggingInspector = true;
            _dragStartMouseInPopup = e.GetPosition(PreviewSurface);
            _dragStartPopupOffset = new Point(InspectorPopup.HorizontalOffset, InspectorPopup.VerticalOffset);

            // Capture the entire popup subtree so we reliably get MouseUp even if pointer leaves header
            Mouse.Capture(InspectorPopup.Child, CaptureMode.SubTree);
            e.Handled = true;
        }

        private void InspectorHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingInspector || !InspectorPopup.IsOpen) return;
            var cur = e.GetPosition(PreviewSurface);
            var dx = cur.X - _dragStartMouseInPopup.X;
            var dy = cur.Y - _dragStartMouseInPopup.Y;
            InspectorPopup.HorizontalOffset = _dragStartPopupOffset.X + dx;
            InspectorPopup.VerticalOffset = _dragStartPopupOffset.Y + dy;
            e.Handled = true;
        }

        private void InspectorHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingInspector) return;
            _isDraggingInspector = false;
            Mouse.Capture(null);
            e.Handled = true;
        }

        private void InspectorHeader_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isDraggingInspector = false;
        }

        private void InspectorPopup_Closed(object sender, EventArgs e)
        {
            _isDraggingInspector = false;
            Mouse.Capture(null);
        }

        #endregion
    }
}

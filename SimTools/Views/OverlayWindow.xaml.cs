using SimTools.Helpers;
using SimTools.ViewModels;
using SimTools.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls; // Dock, Canvas
using System.Windows.Data;     // BindingOperations
using System.Windows.Markup;   // XamlWriter/XamlReader
using SD = System.Drawing;

namespace SimTools.Views
{
    public partial class OverlayWindow : Window, INotifyPropertyChanged
    {
        public sealed class ToolState : INotifyPropertyChanged
        {
            public object Descriptor { get; }

            private bool _isEnabled = false; // OFF by default
            public bool IsEnabled
            {
                get => _isEnabled;
                set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); UpdateVisibilityRequested?.Invoke(this, EventArgs.Empty); } }
            }

            private double _x = 80, _y = 80, _w = 360, _h = 240;
            public double X { get => _x; set { if (_x != value) { _x = value; OnPropertyChanged(); } } }
            public double Y { get => _y; set { if (_y != value) { _y = value; OnPropertyChanged(); } } }
            public double W { get => _w; set { if (_w != value) { _w = value; OnPropertyChanged(); } } }
            public double H { get => _h; set { if (_h != value) { _h = value; OnPropertyChanged(); } } }

            private bool _pinned = false;
            public bool Pinned { get => _pinned; set { if (_pinned != value) { _pinned = value; OnPropertyChanged(); } } }

            public event PropertyChangedEventHandler PropertyChanged;
            public event EventHandler UpdateVisibilityRequested;
            public ToolState(object d) => Descriptor = d;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private Dock _overlayMenuDock = Dock.Right;
        public Dock OverlayMenuDock
        {
            get => _overlayMenuDock;
            set { if (_overlayMenuDock != value) { _overlayMenuDock = value; OnPropertyChanged(); PositionToolbar(); } }
        }

        public ObservableCollection<ToolState> ToolStates { get; } = new();
        public ICommand SetMenuDockCommand { get; }
        public ICommand CloseOverlayCommand { get; }

        public SD.Rectangle? TargetScreenBoundsPx { get; set; }

        private bool _toolbarCollapsed = false;

        // Map descriptor -> panel for quick lookup/removal on drop
        private readonly Dictionary<object, ToolFloatingPanel> _panelByDescriptor = new();

        public OverlayWindow()
        {
            InitializeComponent();

            SetMenuDockCommand = new RelayCommand(param =>
            {
                if (param is string s && Enum.TryParse<Dock>(s, true, out var dock))
                    OverlayMenuDock = dock;
            });

            CloseOverlayCommand = new RelayCommand(() => Close());

            DataContextChanged += OverlayWindow_DataContextChanged;
            SourceInitialized += OverlayWindow_SourceInitialized;
            SizeChanged += (_, __) => PositionToolbar();
            Loaded += (_, __) => RebuildPanels();

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void OverlayWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ToolStates.Clear();
            _panelByDescriptor.Clear();

            if (DataContext is OverlayEditorViewModel vm && vm.Tools != null)
            {
                int idx = 0;
                foreach (var d in vm.Tools)
                {
                    ToolStates.Add(new ToolState(d)
                    {
                        X = 80 + (idx * 28),
                        Y = 80 + (idx * 20),
                        IsEnabled = false // OFF by default
                    });
                    idx++;
                }
            }
            RebuildPanels();
        }

        private void OverlayWindow_SourceInitialized(object sender, EventArgs e)
        {
            if (TargetScreenBoundsPx is not SD.Rectangle rect) return;

            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src?.CompositionTarget is not null)
            {
                Matrix m = src.CompositionTarget.TransformFromDevice;
                var tl = m.Transform(new System.Windows.Point(rect.Left, rect.Top));
                var br = m.Transform(new System.Windows.Point(rect.Right, rect.Bottom));

                Left = tl.X; Top = tl.Y;
                Width = Math.Max(1, br.X - tl.X);
                Height = Math.Max(1, br.Y - tl.Y);
            }
            else
            {
                Left = rect.Left; Top = rect.Top;
                Width = rect.Width; Height = rect.Height;
            }

            PositionToolbar();
        }

        // ===== Floating panels =====

        private void RebuildPanels()
        {
            if (OverlayCanvas == null) return;
            OverlayCanvas.Children.Clear();
            _panelByDescriptor.Clear();

            foreach (var ts in ToolStates)
            {
                var panel = CreatePanelFor(ts);
                OverlayCanvas.Children.Add(panel);
                _panelByDescriptor[ts.Descriptor] = panel;
            }
        }

        private ToolFloatingPanel CreatePanelFor(ToolState ts)
        {
            var title = TryGet(ts.Descriptor, "Name") as string ?? "Tool";
            var sharedVM = TryGet(ts.Descriptor, "ViewModel") ?? ts.Descriptor;

            // Try to clone the preview control to preserve appearance tweaks; fall back to BuildPreview
            UIElement content = TryClonePreviewElement(ts.Descriptor, sharedVM)
                                ?? (TryInvoke(ts.Descriptor, "BuildPreview") as UIElement)
                                ?? new TextBlock { Text = "No preview", Foreground = System.Windows.Media.Brushes.White };

            if (sharedVM != null && content is FrameworkElement fe && fe.DataContext == null)
                fe.DataContext = sharedVM;

            var panel = new ToolFloatingPanel
            {
                PanelTitle = title,
                IsPinned = ts.Pinned
            };

            panel.Content = content;
            panel.EnableDragExport(ts.Descriptor); // allow dragging into boxes

            // Bind geometry / pin
            BindingOperations.SetBinding(panel, ToolFloatingPanel.XProperty, new Binding(nameof(ToolState.X)) { Source = ts, Mode = BindingMode.TwoWay });
            BindingOperations.SetBinding(panel, ToolFloatingPanel.YProperty, new Binding(nameof(ToolState.Y)) { Source = ts, Mode = BindingMode.TwoWay });
            BindingOperations.SetBinding(panel, ToolFloatingPanel.PanelWidthProperty, new Binding(nameof(ToolState.W)) { Source = ts, Mode = BindingMode.TwoWay });
            BindingOperations.SetBinding(panel, ToolFloatingPanel.PanelHeightProperty, new Binding(nameof(ToolState.H)) { Source = ts, Mode = BindingMode.TwoWay });
            BindingOperations.SetBinding(panel, ToolFloatingPanel.IsPinnedProperty, new Binding(nameof(ToolState.Pinned)) { Source = ts, Mode = BindingMode.TwoWay });

            Canvas.SetLeft(panel, ts.X);
            Canvas.SetTop(panel, ts.Y);

            panel.Visibility = ts.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            ts.UpdateVisibilityRequested += (_, __) =>
            {
                panel.Visibility = ts.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            };

            panel.Closed += (_, __) => ts.IsEnabled = false;
            panel.Minimized += (_, __) =>
            {
                var puck = new Button
                {
                    Width = 28,
                    Height = 28,
                    Content = "▣",
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF0, 0x13, 0x13, 0x15)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Foreground = System.Windows.Media.Brushes.White
                };
                Canvas.SetLeft(puck, ts.X);
                Canvas.SetTop(puck, ts.Y);
                puck.Click += (_, __2) =>
                {
                    OverlayCanvas.Children.Remove(puck);
                    panel.Visibility = Visibility.Visible;
                };
                panel.Visibility = Visibility.Collapsed;
                OverlayCanvas.Children.Add(puck);
            };

            panel.Loaded += (_, __) => Clamp(panel);
            panel.SizeChanged += (_, __) => Clamp(panel);

            return panel;
        }

        private static object TryGet(object obj, string propName)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(propName);
            return p?.GetValue(obj);
        }

        private static object TryInvoke(object obj, string methodName)
        {
            if (obj == null) return null;
            var propDelegate = obj.GetType().GetProperty(methodName)?.GetValue(obj) as Delegate;
            if (propDelegate != null) return propDelegate.DynamicInvoke();
            var mi = obj.GetType().GetMethod(methodName);
            return mi?.Invoke(obj, null);
        }

        // Attempt to deep-clone the live preview element (if the descriptor or its VM exposes one)
        private UIElement TryClonePreviewElement(object descriptor, object sharedVM)
        {
            FrameworkElement preview = TryGet(descriptor, "PreviewElement") as FrameworkElement
                                       ?? TryGet(sharedVM, "PreviewElement") as FrameworkElement;

            if (preview == null) return null;

            try
            {
                string xaml = XamlWriter.Save(preview);
                var clone = (UIElement)XamlReader.Parse(xaml);
                if (clone is FrameworkElement fe && fe.DataContext == null) fe.DataContext = sharedVM;
                return clone;
            }
            catch
            {
                // If serialization fails, fall back to factory
                return null;
            }
        }

        private void Clamp(FrameworkElement fe)
        {
            if (OverlayCanvas == null || fe == null) return;

            double x = Canvas.GetLeft(fe);
            double y = Canvas.GetTop(fe);
            double w = fe.ActualWidth;
            double h = fe.ActualHeight;

            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;

            double maxX = Math.Max(0, OverlayCanvas.ActualWidth - w);
            double maxY = Math.Max(0, OverlayCanvas.ActualHeight - h);

            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x > maxX) x = maxX;
            if (y > maxY) y = maxY;

            Canvas.SetLeft(fe, x);
            Canvas.SetTop(fe, y);
        }

        // ===== Toolbar positioning & collapse (same as before) =====

        private void PositionToolbar()
        {
            if (SideToolbar == null || CollapsedPuck == null) return;

            const double margin = 18;
            SideToolbar.Visibility = _toolbarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            CollapsedPuck.Visibility = _toolbarCollapsed ? Visibility.Visible : Visibility.Collapsed;

            switch (OverlayMenuDock)
            {
                case Dock.Left:
                    SideToolbar.HorizontalAlignment = HorizontalAlignment.Left;
                    SideToolbar.VerticalAlignment = VerticalAlignment.Center;
                    SideToolbar.Margin = new Thickness(margin, 0, 0, 0);
                    CollapsedPuck.HorizontalAlignment = HorizontalAlignment.Left;
                    CollapsedPuck.VerticalAlignment = VerticalAlignment.Center;
                    CollapsedPuck.Margin = new Thickness(6, 0, 0, 0);
                    CollapsedPuck.Content = "▶";
                    break;
                case Dock.Right:
                    SideToolbar.HorizontalAlignment = HorizontalAlignment.Right;
                    SideToolbar.VerticalAlignment = VerticalAlignment.Center;
                    SideToolbar.Margin = new Thickness(0, 0, margin, 0);
                    CollapsedPuck.HorizontalAlignment = HorizontalAlignment.Right;
                    CollapsedPuck.VerticalAlignment = VerticalAlignment.Center;
                    CollapsedPuck.Margin = new Thickness(0, 0, 6, 0);
                    CollapsedPuck.Content = "◀";
                    break;
                case Dock.Top:
                    SideToolbar.HorizontalAlignment = HorizontalAlignment.Center;
                    SideToolbar.VerticalAlignment = VerticalAlignment.Top;
                    SideToolbar.Margin = new Thickness(0, margin, 0, 0);
                    CollapsedPuck.HorizontalAlignment = HorizontalAlignment.Center;
                    CollapsedPuck.VerticalAlignment = VerticalAlignment.Top;
                    CollapsedPuck.Margin = new Thickness(0, 6, 0, 0);
                    CollapsedPuck.Content = "▼";
                    break;
                case Dock.Bottom:
                    SideToolbar.HorizontalAlignment = HorizontalAlignment.Center;
                    SideToolbar.VerticalAlignment = VerticalAlignment.Bottom;
                    SideToolbar.Margin = new Thickness(0, 0, 0, margin);
                    CollapsedPuck.HorizontalAlignment = HorizontalAlignment.Center;
                    CollapsedPuck.VerticalAlignment = VerticalAlignment.Bottom;
                    CollapsedPuck.Margin = new Thickness(0, 0, 0, 6);
                    CollapsedPuck.Content = "▲";
                    break;
            }
        }

        private void CollapseToolbar_Click(object sender, RoutedEventArgs e)
        {
            _toolbarCollapsed = true;
            PositionToolbar();
        }

        private void ExpandToolbar_Click(object sender, RoutedEventArgs e)
        {
            _toolbarCollapsed = false;
            PositionToolbar();
        }

        // === New Box and drag-drop wiring ===
        private void NewBox_Click(object sender, RoutedEventArgs e)
        {
            var box = CreateAndAddBox(140, 140);

            // Seed with currently enabled tools
            foreach (var ts in ToolStates.Where(t => t.IsEnabled))
            {
                AddToolToBox(box, ts.Descriptor);
                // optionally hide panel once boxed:
                if (_panelByDescriptor.TryGetValue(ts.Descriptor, out var p))
                {
                    p.Visibility = Visibility.Collapsed;
                }
            }
        }

        private MultiToolBoxPanel CreateAndAddBox(double x, double y)
        {
            var box = new MultiToolBoxPanel { X = x, Y = y };
            box.ToolDroppedIn += (_, desc) =>
            {
                // when a tool is dropped in, add and hide its panel
                AddToolToBox(box, desc);
                if (_panelByDescriptor.TryGetValue(desc, out var panel))
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            };
            box.Closed += (_, __2) => OverlayCanvas.Children.Remove(box);

            OverlayCanvas.Children.Add(box);
            Canvas.SetLeft(box, box.X);
            Canvas.SetTop(box, box.Y);
            return box;
        }

        private void AddToolToBox(MultiToolBoxPanel box, object descriptor)
        {
            var title = TryGet(descriptor, "Name") as string ?? "Tool";
            var sharedVM = TryGet(descriptor, "ViewModel") ?? descriptor;

            Func<UIElement> factory = () =>
            {
                // Prefer clone of preview (preserves style tweaks)
                var el = TryClonePreviewElement(descriptor, sharedVM)
                         ?? (TryInvoke(descriptor, "BuildPreview") as UIElement)
                         ?? new TextBlock { Text = "No preview", Foreground = System.Windows.Media.Brushes.White };
                if (sharedVM != null && el is FrameworkElement fe && fe.DataContext == null)
                    fe.DataContext = sharedVM;
                return el;
            };

            box.AddTool(title, factory, sharedVM);
        }

        // === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

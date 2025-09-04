using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SimTools.Controls
{
    public partial class ToolFloatingPanel : UserControl
    {
        public event EventHandler Closed;
        public event EventHandler Minimized;

        // Allows OverlayWindow to enable drag-drop with a descriptor
        private object _dragPayload;
        private bool _manuallyResized = false;

        public ToolFloatingPanel()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                // Drag/Pin chrome
                DragHandle.MouseLeftButtonDown += DragHandle_MouseLeftButtonDown;
                DragHandle.MouseMove += DragHandle_MouseMoveStartDrag;

                // Resize (disabled when pinned)
                ResizeBR.DragDelta += (s, e) => { if (!IsPinned) { _manuallyResized = true; ResizeBy(e.HorizontalChange, e.VerticalChange); } };
                ResizeR.DragDelta += (s, e) => { if (!IsPinned) { _manuallyResized = true; ResizeBy(e.HorizontalChange, 0); } };
                ResizeB.DragDelta += (s, e) => { if (!IsPinned) { _manuallyResized = true; ResizeBy(0, e.VerticalChange); } };

                MinBtn.Click += (s, e) => Minimized?.Invoke(this, EventArgs.Empty);
                CloseBtn.Click += (s, e) => Closed?.Invoke(this, EventArgs.Empty);

                PinBtn.Checked += (_, __2) => UpdatePinState();
                PinBtn.Unchecked += (_, __2) => UpdatePinState();

                // Auto-size to content (like your preview)
                ContentHost.SizeChanged += (_, __2) => { if (AutoSizeToContent && !_manuallyResized) SyncSizeToContent(); };

                Canvas.SetLeft(this, X);
                Canvas.SetTop(this, Y);
                Width = PanelWidth;
                Height = PanelHeight;

                UpdatePinState();
            };
        }

        public void EnableDragExport(object payload) => _dragPayload = payload;

        #region Dependency Properties

        public static readonly DependencyProperty PanelTitleProperty =
            DependencyProperty.Register(nameof(PanelTitle), typeof(string), typeof(ToolFloatingPanel), new PropertyMetadata("Tool"));
        public string PanelTitle { get => (string)GetValue(PanelTitleProperty); set => SetValue(PanelTitleProperty, value); }

        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register(nameof(X), typeof(double), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));
        public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }

        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register(nameof(Y), typeof(double), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));
        public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }

        public static readonly DependencyProperty PanelWidthProperty =
            DependencyProperty.Register(nameof(PanelWidth), typeof(double), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(320d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSizeChanged));
        public double PanelWidth { get => (double)GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

        public static readonly DependencyProperty PanelHeightProperty =
            DependencyProperty.Register(nameof(PanelHeight), typeof(double), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(220d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSizeChanged));
        public double PanelHeight { get => (double)GetValue(PanelHeightProperty); set => SetValue(PanelHeightProperty, value); }

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPinnedChanged));
        public bool IsPinned { get => (bool)GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }

        public static readonly DependencyProperty AutoSizeToContentProperty =
            DependencyProperty.Register(nameof(AutoSizeToContent), typeof(bool), typeof(ToolFloatingPanel),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public bool AutoSizeToContent { get => (bool)GetValue(AutoSizeToContentProperty); set => SetValue(AutoSizeToContentProperty, value); }

        private static void OnPosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (ToolFloatingPanel)d;
            Canvas.SetLeft(p, p.X);
            Canvas.SetTop(p, p.Y);
        }
        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (ToolFloatingPanel)d;
            p.Width = p.PanelWidth;
            p.Height = p.PanelHeight;
        }
        private static void OnPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (ToolFloatingPanel)d;
            p.PinBtn.IsChecked = p.IsPinned;
            p.UpdatePinState();
        }
        #endregion

        private Point? _dragStart;
        private double _startX, _startY;

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsPinned) return;

            e.Handled = true;
            _dragStart = e.GetPosition(null);
            _startX = X;
            _startY = Y;
            Mouse.Capture(DragHandle);
            DragHandle.MouseMove += DragHandle_MouseMove;
            DragHandle.MouseLeftButtonUp += DragHandle_MouseLeftButtonUp;
        }

        // start a DoDragDrop when moving enough; payload is the tool descriptor
        private void DragHandle_MouseMoveStartDrag(object sender, MouseEventArgs e)
        {
            if (IsPinned || _dragPayload == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - 10) + Math.Abs(p.Y - 10) > 8) // small threshold
            {
                DragDrop.DoDragDrop(this, new DataObject("SimTools.ToolDescriptor", _dragPayload), DragDropEffects.Move);
            }
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsPinned) return;
            if (_dragStart is not Point s) return;

            var p = e.GetPosition(null);
            X = _startX + (p.X - s.X);
            Y = _startY + (p.Y - s.Y);
        }

        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(null);
            DragHandle.MouseMove -= DragHandle_MouseMove;
            DragHandle.MouseLeftButtonUp -= DragHandle_MouseLeftButtonUp;
            SnapAndClamp();
        }

        private void ResizeBy(double dx, double dy)
        {
            PanelWidth = Math.Max(180, PanelWidth + dx);
            PanelHeight = Math.Max(120, PanelHeight + dy);
            SnapAndClamp();
        }

        private void SnapAndClamp()
        {
            if (Parent is Canvas canvas)
            {
                double maxX = Math.Max(0, canvas.ActualWidth - PanelWidth);
                double maxY = Math.Max(0, canvas.ActualHeight - PanelHeight);

                const double t = 12;
                double newX = X, newY = Y;

                if (Math.Abs(newX) <= t) newX = 0;
                if (Math.Abs(newY) <= t) newY = 0;
                if (Math.Abs((canvas.ActualWidth - (newX + PanelWidth))) <= t) newX = Math.Max(0, canvas.ActualWidth - PanelWidth);
                if (Math.Abs((canvas.ActualHeight - (newY + PanelHeight))) <= t) newY = Math.Max(0, canvas.ActualHeight - PanelHeight);

                X = Math.Min(maxX, Math.Max(0, newX));
                Y = Math.Min(maxY, Math.Max(0, newY));
            }
        }

        private void UpdatePinState()
        {
            var pinned = IsPinned || (PinBtn.IsChecked == true);
            PinBtn.IsChecked = pinned;

            DragHandle.Cursor = pinned ? Cursors.Arrow : Cursors.SizeAll;

            ResizeB.IsHitTestVisible = !pinned;
            ResizeR.IsHitTestVisible = !pinned;
            ResizeBR.IsHitTestVisible = !pinned;
            ResizeB.Opacity = pinned ? 0.0 : 1.0;
            ResizeR.Opacity = pinned ? 0.0 : 1.0;
            ResizeBR.Opacity = pinned ? 0.0 : 1.0;
        }

        private void SyncSizeToContent()
        {
            if (ContentHost.Content is FrameworkElement fe)
            {
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = fe.DesiredSize;
                PanelWidth = Math.Max(180, desired.Width + 20);  // + margins
                PanelHeight = Math.Max(120, desired.Height + 44); // + content + title bar
            }
        }
    }
}

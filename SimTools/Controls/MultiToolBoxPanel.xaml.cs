using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimTools.Controls
{
    public partial class MultiToolBoxPanel : UserControl
    {
        public event EventHandler Closed;
        public event EventHandler<object> ToolDroppedIn; // bubbles the dropped descriptor up

        public MultiToolBoxPanel()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                // Drag (move) behavior from the top bar
                DragHandle.MouseLeftButtonDown += DragStart;
                DragHandle.MouseMove += DragMoveStartDrag; // keeps threshold behavior consistent

                // Removed: ResizeBR hookup (the tiny white thumb no longer exists)

                PrevBtn.Click += (_, __2) => Prev();
                NextBtn.Click += (_, __2) => Next();
                CloseBtn.Click += (_, __2) => Closed?.Invoke(this, EventArgs.Empty);

                Bind1Btn.Click += (_, __2) => CaptureKey(k => Key1 = k);
                Bind2Btn.Click += (_, __2) => CaptureKey(k => Key2 = k);

                PinBtn.Checked += (_, __2) => UpdatePinState();
                PinBtn.Unchecked += (_, __2) => UpdatePinState();

                Width = PanelWidth;
                Height = PanelHeight;
                UpdateContent();
                UpdatePinState();
            };

            // Key toggle
            PreviewKeyDown += (s, e) =>
            {
                if (Key1.HasValue && e.Key == Key1.Value) { Next(); e.Handled = true; }
                else if (Key2.HasValue && e.Key == Key2.Value) { Prev(); e.Handled = true; }
            };

            // Accept drops from tool panels
            AllowDrop = true;
            DragOver += (_, e) =>
            {
                if (e.Data.GetDataPresent("SimTools.ToolDescriptor"))
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                }
            };
            Drop += (_, e) =>
            {
                if (e.Data.GetDataPresent("SimTools.ToolDescriptor"))
                {
                    var desc = e.Data.GetData("SimTools.ToolDescriptor");
                    ToolDroppedIn?.Invoke(this, desc);
                    e.Handled = true;
                }
            };
        }

        #region DP: Position/Size/Keys/Title/Pin
        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register(nameof(X), typeof(double), typeof(MultiToolBoxPanel),
                new FrameworkPropertyMetadata(120d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PosChanged));
        public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }

        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register(nameof(Y), typeof(double), typeof(MultiToolBoxPanel),
                new FrameworkPropertyMetadata(120d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PosChanged));
        public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }

        public static readonly DependencyProperty PanelWidthProperty =
            DependencyProperty.Register(nameof(PanelWidth), typeof(double), typeof(MultiToolBoxPanel),
                new FrameworkPropertyMetadata(360d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SizeChanged));
        public double PanelWidth { get => (double)GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

        public static readonly DependencyProperty PanelHeightProperty =
            DependencyProperty.Register(nameof(PanelHeight), typeof(double), typeof(MultiToolBoxPanel),
                new FrameworkPropertyMetadata(260d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SizeChanged));
        public double PanelHeight { get => (double)GetValue(PanelHeightProperty); set => SetValue(PanelHeightProperty, value); }

        public static readonly DependencyProperty CurrentToolTitleProperty =
            DependencyProperty.Register(nameof(CurrentToolTitle), typeof(string), typeof(MultiToolBoxPanel), new PropertyMetadata(""));
        public string CurrentToolTitle { get => (string)GetValue(CurrentToolTitleProperty); set => SetValue(CurrentToolTitleProperty, value); }

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(MultiToolBoxPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public bool IsPinned { get => (bool)GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }

        public Key? Key1 { get; set; }
        public Key? Key2 { get; set; }
        #endregion

        private static void PosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (MultiToolBoxPanel)d;
            Canvas.SetLeft(p, p.X);
            Canvas.SetTop(p, p.Y);
        }
        private static void SizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (MultiToolBoxPanel)d;
            p.Width = p.PanelWidth;
            p.Height = p.PanelHeight;
        }

        private Point? _dragStart;
        private double _startX, _startY;
        private void DragStart(object sender, MouseButtonEventArgs e)
        {
            if (IsPinned) return;
            e.Handled = true;
            _dragStart = e.GetPosition(null);
            _startX = X; _startY = Y;
            Mouse.Capture(DragHandle);
            DragHandle.MouseMove += DragMove;
            DragHandle.MouseLeftButtonUp += DragEnd;
        }
        private void DragMove(object sender, MouseEventArgs e)
        {
            if (IsPinned) return;
            if (_dragStart is not Point s) return;
            var p = e.GetPosition(null);
            X = _startX + (p.X - s.X);
            Y = _startY + (p.Y - s.Y);
        }
        private void DragMoveStartDrag(object sender, MouseEventArgs e) { /* keep threshold behavior consistent */ }
        private void DragEnd(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(null);
            DragHandle.MouseMove -= DragMove;
            DragHandle.MouseLeftButtonUp -= DragEnd;
            Clamp();
        }

        private void Clamp()
        {
            if (Parent is Canvas c)
            {
                var maxX = Math.Max(0, c.ActualWidth - Width);
                var maxY = Math.Max(0, c.ActualHeight - Height);
                X = Math.Max(0, Math.Min(maxX, X));
                Y = Math.Max(0, Math.Min(maxY, Y));
            }
        }

        // --- Tool management ---
        private readonly List<(string Title, Func<UIElement> Factory, UIElement Instance, Size Measured)> _tools = new();
        private int _index = -1;

        public void AddTool(string title, Func<UIElement> factory, object sharedVM = null)
        {
            UIElement Create()
            {
                var el = factory?.Invoke() ?? new TextBlock { Text = "Empty", Foreground = System.Windows.Media.Brushes.White };
                if (sharedVM != null && el is FrameworkElement fe && fe.DataContext == null) fe.DataContext = sharedVM;
                // measure to compute "largest" sizing
                el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return el;
            }

            var inst = Create();
            var size = inst.DesiredSize;
            _tools.Add((title, factory, inst, size));
            RecalcToLargest();
            if (_index == -1) { _index = 0; UpdateContent(); }
        }

        public void Next()
        {
            if (_tools.Count == 0) return;
            _index = (_index + 1) % _tools.Count;
            UpdateContent();
        }
        public void Prev()
        {
            if (_tools.Count == 0) return;
            _index = (_index - 1 + _tools.Count) % _tools.Count;
            UpdateContent();
        }

        private void UpdateContent()
        {
            if (_index < 0 || _index >= _tools.Count)
            {
                ContentHost.Content = null;
                CurrentToolTitle = "";
                return;
            }

            var entry = _tools[_index];
            var view = entry.Instance ?? entry.Factory();
            ContentHost.Content = view;
            CurrentToolTitle = entry.Title;
        }

        private void RecalcToLargest()
        {
            double maxW = 340, maxH = 240;
            foreach (var t in _tools)
            {
                maxW = Math.Max(maxW, t.Measured.Width + 20);
                maxH = Math.Max(maxH, t.Measured.Height + 46);
            }
            PanelWidth = maxW;
            PanelHeight = maxH;
        }

        private void CaptureKey(Action<Key> setter)
        {
            var win = Window.GetWindow(this);
            if (win == null) return;
            void Handler(object s, KeyEventArgs e)
            {
                setter(e.Key);
                win.PreviewKeyDown -= Handler;
            }
            win.PreviewKeyDown += Handler;
        }

        private void UpdatePinState()
        {
            var pinned = IsPinned || (PinBtn.IsChecked == true);
            PinBtn.IsChecked = pinned;
            DragHandle.Cursor = pinned ? Cursors.Arrow : Cursors.SizeAll;

            // Removed: ResizeBR interactivity/opacity toggles (no longer present)
        }
    }
}

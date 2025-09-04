using SimTools.ViewModels;
using SimTools.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;          // Dock lives here
using SD = System.Drawing;              // alias for System.Drawing
using Forms = System.Windows.Forms;     // for Screen if needed elsewhere

namespace SimTools.Views
{
    public partial class OverlayWindow : Window, INotifyPropertyChanged
    {
        // Tool wrapper — Descriptor is object so it can be either a nested or top-level type
        public sealed class ToolState : INotifyPropertyChanged
        {
            public object Descriptor { get; }
            private bool _isEnabled = true;
            public bool IsEnabled
            {
                get => _isEnabled;
                set { if(_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
            }
            public ToolState(object descriptor) => Descriptor = descriptor;
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private Dock _overlayMenuDock = Dock.Right;
        public Dock OverlayMenuDock
        {
            get => _overlayMenuDock;
            set { if(_overlayMenuDock != value) { _overlayMenuDock = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ToolState> ToolStates { get; } = new();

        public ICommand SetMenuDockCommand { get; }
        public ICommand CloseOverlayCommand { get; }

        // Selected monitor bounds (device pixels)
        public SD.Rectangle? TargetScreenBoundsPx { get; set; }

        public OverlayWindow()
        {
            InitializeComponent();

            SetMenuDockCommand = new RelayCommand(param =>
            {
                if(param is string s && Enum.TryParse<Dock>(s, true, out var dock))
                    OverlayMenuDock = dock;
            });

            CloseOverlayCommand = new RelayCommand(() => Close());

            DataContextChanged += OverlayWindow_DataContextChanged;
            SourceInitialized += OverlayWindow_SourceInitialized;
            PreviewKeyDown += (s, e) => { if(e.Key == Key.Escape) Close(); };
        }

        private void OverlayWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ToolStates.Clear();

            // Accepts: vm.Tools is IEnumerable of descriptors (any type with Name + BuildPreview)
            if(DataContext is OverlayEditorViewModel vm && vm.Tools != null)
            {
                foreach(var d in vm.Tools)
                    ToolStates.Add(new ToolState(d));
            }
        }

        private void OverlayWindow_SourceInitialized(object sender, EventArgs e)
        {
            if(TargetScreenBoundsPx is not SD.Rectangle rect) return;

            var src = (HwndSource)PresentationSource.FromVisual(this);
            if(src?.CompositionTarget is not null)
            {
                Matrix m = src.CompositionTarget.TransformFromDevice;

                var topLeft = m.Transform(new System.Windows.Point(rect.Left, rect.Top));
                var bottomRight = m.Transform(new System.Windows.Point(rect.Right, rect.Bottom));

                Left = topLeft.X;
                Top = topLeft.Y;
                Width = Math.Max(1, bottomRight.X - topLeft.X);
                Height = Math.Max(1, bottomRight.Y - topLeft.Y);
            }
            else
            {
                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Width;
                Height = rect.Height;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

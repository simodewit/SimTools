using Microsoft.Win32;
using SimTools.Controls;
using SimTools.ViewModels;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimTools.Views
{
    public partial class OverlayEditorPage : UserControl
    {
        private enum PreviewBackgroundKind { Asphalt, SolidGray, SolidDark, CustomImage }
        private PreviewBackgroundKind _bgKind = PreviewBackgroundKind.Asphalt;
        private ImageBrush _customImageBrush;

        public OverlayEditorPage()
        {
            InitializeComponent();
            Loaded += OverlayEditorPage_Loaded;
        }

        private OverlayEditorViewModel VM => DataContext as OverlayEditorViewModel;

        private void OverlayEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Try to use the asphalt image again as default background.
            ApplyAsphaltOrFallback();

            // Refresh the initial preview for the selected tool.
            RefreshToolPreview();

            // React to SelectedTool VM changes.
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
            try
            {
                // Prefer an assembly-local resource: /Images/Asphalt.png (Build Action: Resource)
                var uri = new Uri("pack://application:,,,/Images/Asphalt.png", UriKind.Absolute);
                var img = new BitmapImage(uri);
                var brush = new ImageBrush(img) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                PreviewBackdrop.Background = brush;
                _bgKind = PreviewBackgroundKind.Asphalt;
            }
            catch
            {
                // Fallback to the theme surface if the image isn't found.
                PreviewBackdrop.Background = (Brush)FindResource("Brush.SurfaceAlt");
                _bgKind = PreviewBackgroundKind.SolidGray;
            }
        }

        private void RefreshToolPreview()
        {
            if (VM?.SelectedTool == null) return;

            var view = VM.SelectedTool.BuildPreview?.Invoke();
            if (view != null)
            {
                view.IsHitTestVisible = true;
                view.MouseLeftButtonDown += PreviewElement_MouseLeftButtonDown;
            }
            ToolHost.Content = view;
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (sender is Button b && b.Tag is OverlayToolDescriptor desc && !ReferenceEquals(VM.SelectedTool, desc))
            {
                VM.SelectedTool = desc;
                RefreshToolPreview();
            }
        }

        private void PreviewElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DependencyObject d)
                ShowInspectorFor(d);
        }

        private void PreviewSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // General hit test: nearest FrameworkElement under the mouse
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
                ShowInspectorFor(found);
        }

        private void OverlayBackgroundHitTarget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Selecting the overlay background surface
            ShowInspectorFor(sender as DependencyObject);
        }

        private void ShowInspectorFor(DependencyObject obj)
        {
            if (VM == null) return;
            VM.SelectedVisual = obj;

            Inspector.SelectedObject = obj;

            // Open (stays open until user closes)
            InspectorPopup.IsOpen = true;
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
                _bgKind = PreviewBackgroundKind.Asphalt;
                ApplyAsphaltOrFallback();
                return;
            }

            if (label.StartsWith("solid gray"))
            {
                _bgKind = PreviewBackgroundKind.SolidGray;
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
                return;
            }

            if (label.StartsWith("solid dark"))
            {
                _bgKind = PreviewBackgroundKind.SolidDark;
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
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
    }
}

using SimTools.Models;
using SimTools.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimTools.Views
{
    public partial class OverlayEditorPage : UserControl
    {
        private bool _draggingExisting;
        private Point _dragStart;
        private OverlayElement _dragElement;

        public OverlayEditorPage()
        {
            InitializeComponent();
        }

        private OverlayEditorViewModel VM
        {
            get { return DataContext as OverlayEditorViewModel; }
        }

        // Drag a tool from the Tools list
        private void Tool_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var b = sender as Border;
                if (b != null && b.Child is TextBlock)
                {
                    var tb = (TextBlock)b.Child;
                    var type = tb.Tag as string;
                    if (!string.IsNullOrEmpty(type))
                    {
                        DragDrop.DoDragDrop(b, new DataObject("ToolKind", type), DragDropEffects.Copy);
                    }
                }
            }
        }

        private void Preview_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ToolKind"))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Preview_Drop(object sender, DragEventArgs e)
        {
            if (VM == null) return;
            if (e.Data.GetDataPresent("ToolKind"))
            {
                string type = e.Data.GetData("ToolKind") as string;
                var p = e.GetPosition(PreviewCanvas);
                VM.AddElement(type, p.X - 60, p.Y - 20); // drop with a slight offset
            }
        }

        // Dragging existing elements inside the preview
        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var el = (sender as FrameworkElement)?.DataContext as OverlayElement;
            if (el == null) return;
            _draggingExisting = true;
            _dragElement = el;
            _dragStart = e.GetPosition(PreviewCanvas);
            (sender as FrameworkElement).CaptureMouse();
        }

        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingExisting || _dragElement == null) return;
            var pos = e.GetPosition(PreviewCanvas);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;
            _dragElement.X += dx;
            _dragElement.Y += dy;
            _dragStart = pos;
        }

        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggingExisting = false;
            _dragElement = null;
            (sender as FrameworkElement)?.ReleaseMouseCapture();
        }
    }
}

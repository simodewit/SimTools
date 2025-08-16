using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimTools.Controls
{
    public partial class TitleBar : UserControl
    {
        public TitleBar()
        {
            InitializeComponent();
        }

        private bool IsInsideButtons(DependencyObject source)
        {
            if (ButtonsPanel == null || source == null) return false;
            var current = source;
            while (current != null)
            {
                if (current == ButtonsPanel) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Optional: only if you want to support dragging from a specific child element
            // Not needed if you rely entirely on CaptionHeight.
            if (e.ButtonState == MouseButtonState.Pressed)
                Window.GetWindow(this)?.DragMove();
        }

        // Buttons can keep toggling WindowState — with WindowChrome they’ll respect the taskbar.
        private void Min_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)!.WindowState = WindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this)!;
            w.WindowState = (w.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}

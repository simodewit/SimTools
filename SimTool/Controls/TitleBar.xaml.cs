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
            // Ignore drag if clicking on buttons
            if (IsInsideButtons(e.OriginalSource as DependencyObject)) return;

            var window = Window.GetWindow(this);
            if (window == null) return;

            // Double-click to toggle maximize
            if (e.ClickCount == 2)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            try { window.DragMove(); } catch { }
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w != null) w.WindowState = WindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null) return;
            w.WindowState = (w.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w != null) w.Close();
        }
    }
}

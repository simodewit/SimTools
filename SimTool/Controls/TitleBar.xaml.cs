using SimTools.Views;
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
            if (IsInsideButtons(e.OriginalSource as DependencyObject)) return;
            var w = Window.GetWindow(this);
            if (w == null) return;

            if (e.ClickCount == 2)
            {
                // Toggle fake maximize
                if (IsWorkAreaMaximized(w))
                    w.WindowState = WindowState.Normal;
                else
                    WindowWorkAreaHelper.MaximizeToWorkArea(w);
                return;
            }

            try { w.DragMove(); } catch { }
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

            if (IsWorkAreaMaximized(w))
                w.WindowState = WindowState.Normal;
            else
                WindowWorkAreaHelper.MaximizeToWorkArea(w);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w != null) w.Close();
        }

        private static bool IsWorkAreaMaximized(Window w) =>
        w.WindowState == WindowState.Normal && (w.Left <= 1) && (w.Top <= 1);
    }
}

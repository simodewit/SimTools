using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimTools.Controls
{
    public partial class TitleBar : UserControl
    {
        public TitleBar()
        {
            InitializeComponent();
        }

        private Window GetWindowSafe()
        {
            return Window.GetWindow(this);
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            var w = GetWindowSafe();
            if (w != null) w.DragMove();
        }

        private void Minimize(object sender, RoutedEventArgs e)
        {
            var w = GetWindowSafe();
            if (w != null) w.WindowState = WindowState.Minimized;
        }

        private void MaxRestore(object sender, RoutedEventArgs e)
        {
            var w = GetWindowSafe();
            if (w == null) return;
            w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            var w = GetWindowSafe();
            if (w != null) w.Close();
        }
    }
}

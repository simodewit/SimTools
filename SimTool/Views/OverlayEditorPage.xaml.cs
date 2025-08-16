using SimTools.Models;
using SimTools.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimTools.Views
{
    public partial class OverlayEditorPage : UserControl
    {
        public OverlayEditorPage()
        {
            InitializeComponent();
        }

        private OverlayEditorViewModel VM { get { return DataContext as OverlayEditorViewModel; } }

        private void Preview_DragOver(object sender, DragEventArgs e) { }
        private void Preview_Drop(object sender, DragEventArgs e) { }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var el = (sender as FrameworkElement)?.DataContext as OverlayElement;
            if (el == null) return;
            if (VM != null) VM.SelectElement(el);
        }

        private void Element_MouseMove(object sender, MouseEventArgs e) { }
        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }

        private void BottomTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tab = (sender as TabControl)?.SelectedItem as TabItem;
            if (VM == null || tab == null) return;
            var header = (tab.Header ?? "").ToString();
            VM.IsKeybindsActive = string.Equals(header, "Keybinds", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

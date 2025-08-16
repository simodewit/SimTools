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

        // Canvas: DragOver (no-op)
        private void Preview_DragOver(object sender, DragEventArgs e)
        {
            // Drag & drop disabled (legacy). Intentionally left blank.
        }

        // Canvas: Drop (no-op)
        private void Preview_Drop(object sender, DragEventArgs e)
        {
            // Drag & drop disabled (legacy). Intentionally left blank.
        }

        // Item: MouseLeftButtonDown -> only selects the element now
        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var el = (sender as FrameworkElement)?.DataContext as OverlayElement;
            if (el == null) return;

            // Select the element so the centered settings panel can bind to it
            if (VM != null) VM.SelectElement(el);
        }

        // Item: MouseMove (no-op)
        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            // Element dragging disabled (legacy). Intentionally left blank.
        }

        // Item: MouseLeftButtonUp (no-op)
        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Element dragging disabled (legacy). Intentionally left blank.
        }

        // Bottom tabs: flip flag to control which centered panel is visible
        private void BottomTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tab = (sender as TabControl)?.SelectedItem as TabItem;
            if (VM == null || tab == null) return;

            var header = (tab.Header ?? "").ToString();
            VM.IsKeybindsActive = string.Equals(header, "Keybinds", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

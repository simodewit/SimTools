using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimTools.Helpers
{
    public static class KeybindUiHelpers
    {
        public static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        public static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var d in FindVisualDescendants<T>(child)) yield return d;
            }
        }

        public static Brush TryFindBrush(string key, Brush fallback)
        {
            var res = Application.Current?.Resources[key] as Brush;
            return res ?? fallback;
        }

        public static void SetButtonText(Button btn, string label)
        {
            if (btn == null) return;
            if (btn.Content is TextBlock tb) tb.Text = label;
            else btn.Content = label;
        }
    }
}

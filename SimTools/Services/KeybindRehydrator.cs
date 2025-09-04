using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimTools.Helpers;
using SimTools.Models;

namespace SimTools.Services
{
    public static class KeybindRehydrator
    {
        /// <summary>
        /// Walk visible rows and set up each row’s Assign button:
        /// - Content text (friendly binding label)
        /// - Tag = DeviceBindingDescriptor (used by highlight matching)
        /// </summary>
        public static void RehydrateRows(ItemsControl list)
        {
            var items = (list?.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var kb in items.OfType<KeybindBinding>())
            {
                var container = list.ItemContainerGenerator.ContainerFromItem(kb) as DependencyObject;
                if (container == null) continue;

                // Find the “Assign” button in the row (named AssignBtn in XAML, or column 1)
                var assignBtn = KeybindUiHelpers
                    .FindVisualDescendants<Button>(container)
                    .FirstOrDefault(b => b.Name == "AssignBtn" || Grid.GetColumn(b) == 1);

                if (assignBtn == null) continue;

                var descriptor = BuildDescriptorFromModel(kb);
                assignBtn.Tag = descriptor; // null is fine = no binding
                KeybindUiHelpers.SetButtonText(assignBtn, kb?.ToString() ?? "None");
            }
        }

        public static DeviceBindingDescriptor BuildDescriptorFromModel(KeybindBinding kb)
        {
            if (kb == null) return null;

            var hasDevice = !string.IsNullOrWhiteSpace(kb.Device) || !string.IsNullOrWhiteSpace(kb.DeviceKey);
            var hasKeyboard = kb.Key != System.Windows.Input.Key.None || kb.Modifiers != System.Windows.Input.ModifierKeys.None;

            if (!hasDevice && !hasKeyboard) return null;

            // We only need device-based match info for highlighting. Keyboard hotkeys are handled by
            // synthesizing InputBindingResult in KeyPreview and comparing in HighlightMatches.
            if (hasDevice)
                return new DeviceBindingDescriptor { DeviceType = kb.Device, ControlLabel = kb.DeviceKey };

            return null;
        }
    }
}

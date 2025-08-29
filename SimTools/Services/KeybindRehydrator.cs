using System.Collections;
using System.Linq;
using System.Windows.Controls;
using SimTools.Helpers;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Rehydrates row and general buttons from model state.
    /// </summary>
    public static class KeybindRehydrator
    {
        public static void RehydrateRows(ItemsControl list)
        {
            var items = (list?.ItemsSource as IEnumerable) ?? Enumerable.Empty<object>();
            foreach (var item in items)
            {
                var container = list.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.DependencyObject;
                if (container == null) continue;

                var btn = KeybindUiHelpers.FindVisualDescendants<Button>(container).FirstOrDefault(b =>
                {
                    try { return Grid.GetColumn(b) == 1; } catch { return true; }
                });
                if (btn == null) continue;

                if (!(item is SimTools.Models.KeybindBinding kb)) continue;

                btn.Tag = BuildDescriptorFromModel(kb);
                KeybindUiHelpers.SetButtonText(btn, kb.ToString());
            }
        }

        public static DeviceBindingDescriptor BuildDescriptorFromModel(SimTools.Models.KeybindBinding kb)
        {
            if (kb == null) return null;
            if (string.IsNullOrWhiteSpace(kb.Device) && string.IsNullOrWhiteSpace(kb.DeviceKey) &&
                kb.Key == System.Windows.Input.Key.None && kb.Modifiers == System.Windows.Input.ModifierKeys.None)
                return null;

            if (!string.IsNullOrWhiteSpace(kb.Device) || !string.IsNullOrWhiteSpace(kb.DeviceKey))
                return new DeviceBindingDescriptor { DeviceType = kb.Device, ControlLabel = kb.DeviceKey };

            return null;
        }
    }
}

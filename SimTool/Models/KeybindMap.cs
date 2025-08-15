using System;
using System.Collections.ObjectModel;

namespace SimTools.Models
{
    // Groups multiple keybindings under a named map.
    // Has a stable Id, display Name, and a collection of KeybindBinding items.
    // Used by profiles to organize different sets (e.g., per game or mode).

    public class KeybindMap
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Map";
        public ObservableCollection<KeybindBinding> Keybinds { get; set; } = new ObservableCollection<KeybindBinding>();
    }
}

using System;
using System.Collections.ObjectModel;

namespace SimTools.Models
{
    public class KeybindMap
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Map";
        public ObservableCollection<KeybindBinding> Keybinds { get; set; } = new ObservableCollection<KeybindBinding>();
    }
}

using System;
using System.Collections.ObjectModel;

namespace SimTools.Models
{
    public class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Profile";
        public ObservableCollection<KeybindMap> Maps { get; set; } = new ObservableCollection<KeybindMap>();
    }
}

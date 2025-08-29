using System;
using System.Collections.ObjectModel;

namespace SimTools.Models
{
    public class Profile
    {
        public string NextMapDevice { get; set; }
        public string NextMapDeviceKey { get; set; }
        public string PrevMapDevice { get; set; }
        public string PrevMapDeviceKey { get; set; }


        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Profile";
        public ObservableCollection<KeybindMap> Maps { get; set; } = new ObservableCollection<KeybindMap>();
    }
}

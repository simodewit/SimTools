using System;
using System.Collections.ObjectModel;

namespace SimTools.Models
{
    // Represents a user profile in SimTools.
    // - Stores navigation hotkeys for switching between keybind maps (Next/Prev).
    // - Each profile has a unique Id, display Name, and a collection of KeybindMaps.
    // - Profiles are persisted via StorageService and shared across the app via AppState.

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

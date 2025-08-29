using System.Collections.ObjectModel;
using SimTools.Models;

namespace SimTools.Services
{
    public class AppState
    {
        public ObservableCollection<Profile> Profiles { get; set; } = new ObservableCollection<Profile>();
        public Profile CurrentProfile { get; set; }
        public KeybindMap CurrentMap { get; set; }

        public void EnsureSelections()
        {
            if (CurrentProfile == null && Profiles.Count > 0)
                CurrentProfile = Profiles[0];
            if (CurrentMap == null && CurrentProfile != null && CurrentProfile.Maps != null && CurrentProfile.Maps.Count > 0)
                CurrentMap = CurrentProfile.Maps[0];
        }
    }
}

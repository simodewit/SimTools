using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SimTools.ViewModels
{
    public class KeybindsViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        private readonly StorageService _storage;

        public ObservableCollection<Profile> Profiles { get { return State.Profiles; } }
        public Profile SelectedProfile
        {
            get { return State.CurrentProfile; }
            set
            {
                State.CurrentProfile = value;
                Raise();
                Raise("Maps");
                SelectedMap = value != null ? value.Maps.FirstOrDefault() : null;
            }
        }

        public ObservableCollection<KeybindMap> Maps
        {
            get { return SelectedProfile != null ? SelectedProfile.Maps : new ObservableCollection<KeybindMap>(); }
        }

        public KeybindMap SelectedMap
        {
            get { return State.CurrentMap; }
            set
            {
                State.CurrentMap = value;
                Raise();
                Raise("Keybinds");
            }
        }

        public ObservableCollection<KeybindBinding> Keybinds
        {
            get { return SelectedMap != null ? SelectedMap.Keybinds : new ObservableCollection<KeybindBinding>(); }
        }

        public RelayCommand AddProfile { get; private set; }
        public RelayCommand RemoveProfile { get; private set; }
        public RelayCommand AddMap { get; private set; }
        public RelayCommand RemoveMap { get; private set; }
        public RelayCommand AddKeybind { get; private set; }
        public RelayCommand RemoveKeybind { get; private set; }
        public RelayCommand Save { get; private set; }

        // General settings hotkeys
        public KeybindBinding NextMapHotkey { get; private set; } = new KeybindBinding { Name = "Next Map" };
        public KeybindBinding PrevMapHotkey { get; private set; } = new KeybindBinding { Name = "Previous Map" };

        public KeybindsViewModel(AppState state, StorageService storage)
        {
            State = state; _storage = storage;

            AddProfile = new RelayCommand(() =>
            {
                var p = new Profile { Name = "New Profile" };
                p.Maps.Add(new KeybindMap { Name = "Map 1" });
                Profiles.Add(p);
                SelectedProfile = p;
                _storage.Save(Profiles);
            });

            RemoveProfile = new RelayCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    Profiles.Remove(SelectedProfile);
                    SelectedProfile = Profiles.FirstOrDefault();
                    _storage.Save(Profiles);
                }
            });

            AddMap = new RelayCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    var m = new KeybindMap { Name = "Map " + (SelectedProfile.Maps.Count + 1) };
                    SelectedProfile.Maps.Add(m);
                    SelectedMap = m;
                    _storage.Save(Profiles);
                }
            });

            RemoveMap = new RelayCommand(() =>
            {
                if (SelectedProfile != null && SelectedMap != null)
                {
                    SelectedProfile.Maps.Remove(SelectedMap);
                    SelectedMap = SelectedProfile.Maps.FirstOrDefault();
                    _storage.Save(Profiles);
                }
            });

            AddKeybind = new RelayCommand(() =>
            {
                if (SelectedMap != null)
                {
                    SelectedMap.Keybinds.Add(new KeybindBinding { Name = "New Action" });
                    _storage.Save(Profiles);
                }
            });

            RemoveKeybind = new RelayCommand(() =>
            {
                if (SelectedMap != null && SelectedMap.Keybinds.Any())
                {
                    SelectedMap.Keybinds.Remove(SelectedMap.Keybinds.Last());
                    _storage.Save(Profiles);
                }
            });

            Save = new RelayCommand(() => _storage.Save(Profiles));
        }

        public void AssignKey(KeyEventArgs e, KeybindBinding binding)
        {
            var cap = HotkeyService.Capture(e);
            if (cap.Key == Key.None) return;
            binding.Modifiers = cap.Modifiers;
            binding.Key = cap.Key;
            Raise("Keybinds");
            _storage.Save(Profiles);
        }

        public void NextMap()
        {
            if (SelectedProfile == null || !SelectedProfile.Maps.Any()) return;
            var idx = SelectedProfile.Maps.IndexOf(SelectedMap);
            if (idx < 0) idx = 0;
            idx = (idx + 1) % SelectedProfile.Maps.Count;
            SelectedMap = SelectedProfile.Maps[idx];
        }

        public void PrevMap()
        {
            if (SelectedProfile == null || !SelectedProfile.Maps.Any()) return;
            var idx = SelectedProfile.Maps.IndexOf(SelectedMap);
            if (idx < 0) idx = 0;
            idx = (idx - 1 + SelectedProfile.Maps.Count) % SelectedProfile.Maps.Count;
            SelectedMap = SelectedProfile.Maps[idx];
        }
    }
}

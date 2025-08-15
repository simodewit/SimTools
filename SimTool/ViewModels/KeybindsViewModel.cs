using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;

namespace SimTools.ViewModels
{
    // ViewModel for the Keybinds page.
    // Binds profiles/maps/keybinds from AppState and saves via StorageService.
    // Exposes add/remove/save commands and selection properties.
    // AssignKey() captures a keyboard hotkey and updates the binding; supports NextMap/PrevMap.

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

        // What KeybindsPage looks for:
        public ICommand SaveCommand { get; private set; }   // exact name matters
        public void Save() => _storage.Save(Profiles);      // method fallback

        // NEW: Commands the view will call when hotkeys fire
        public ICommand NextMapCommand { get; private set; }
        public ICommand PrevMapCommand { get; private set; }

        // Optional: if no profile-level storage exists, the page can store/retrieve on the VM
        // (KeybindsPage uses reflection for NextMapDevice/NextMapDeviceKey/etc.)
        public string NextMapDevice { get; set; }
        public string NextMapDeviceKey { get; set; }
        public string PrevMapDevice { get; set; }
        public string PrevMapDeviceKey { get; set; }

        // General settings hotkeys (persist these in your models if you want them saved too)
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

            SaveCommand = new RelayCommand(_ => Save(), _ => true);

            // NEW: VM commands the view triggers when hotkeys fire
            NextMapCommand = new RelayCommand(_ => NextMap(), _ => true);
            PrevMapCommand = new RelayCommand(_ => PrevMap(), _ => true);
        }

        /// <summary>
        /// Assigns a keyboard hotkey to a KeybindBinding using your existing model fields only.
        /// Also sets Device/DeviceKey so it round-trips and matches global input after restart.
        /// </summary>
        public void AssignKey(System.Windows.Input.KeyEventArgs e, KeybindBinding binding)
        {
            if (binding == null || e == null) return;

            var cap = HotkeyService.Capture(e); // your existing capture
            if (cap.Key == Key.None)
                return;

            binding.Modifiers = cap.Modifiers;
            binding.Key = cap.Key;

            // Populate device metadata used by the UI/global matching and saved to disk
            binding.Device = "Keyboard";
            binding.DeviceKey = FormatHotkey(binding.Modifiers, binding.Key); // e.g., "Ctrl + Shift + P"

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

        // ---- Helpers ----
        private static string FormatHotkey(ModifierKeys mods, Key key)
        {
            if (key == Key.None && mods == ModifierKeys.None) return "None";

            var parts = new System.Collections.Generic.List<string>(4);
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            // Friendly key names for a few common cases; fall back to enum name
            string keyName = key switch
            {
                Key.Return => "Enter",
                Key.Escape => "Esc",
                Key.Prior => "PageUp",
                Key.Next => "PageDown",
                Key.OemPlus => "+",
                Key.OemMinus => "-",
                _ => key.ToString()
            };

            parts.Add(keyName);
            return string.Join(" + ", parts);
        }
    }
}

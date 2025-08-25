// ViewModels/KeybindsViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;

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

        // ---- Only change: we control ordering/grouping in the ComboBox (no XAML edits) ----
        public IReadOnlyList<VirtualOutput> AvailableOutputs { get; } =
            Enum.GetValues(typeof(VirtualOutput)).Cast<VirtualOutput>()
                .Where(v => v != VirtualOutput.None)
                .OrderBy(v => v.GetGroup() == OutputGroup.Joystick ? 0 : v.GetGroup() == OutputGroup.Keyboard ? 1 : 2)
                .ThenBy(v =>
                {
                    if(v.TryAsVJoyButton(out var idx)) return idx;                 // Button number
                    if(v.TryAsKeyboard(out _, out _)) return 1000 + (int)v;        // Stable order
                    return 2000 + (int)v;
                })
                .ToList();

        public RelayCommand AddProfile { get; private set; }
        public RelayCommand RemoveProfile { get; private set; }
        public RelayCommand AddMap { get; private set; }
        public RelayCommand RemoveMap { get; private set; }
        public RelayCommand AddKeybind { get; private set; }
        public RelayCommand RemoveKeybind { get; private set; }

        public ICommand SaveCommand { get; private set; }
        public void Save() => _storage.Save(Profiles);

        public ICommand NextMapCommand { get; private set; }
        public ICommand PrevMapCommand { get; private set; }

        public string NextMapDevice { get; set; }
        public string NextMapDeviceKey { get; set; }
        public string PrevMapDevice { get; set; }
        public string PrevMapDeviceKey { get; set; }

        public KeybindBinding NextMapHotkey { get; private set; } = new KeybindBinding { Name = "Next Map" };
        public KeybindBinding PrevMapHotkey { get; private set; } = new KeybindBinding { Name = "Previous Map" };

        public RelayCommand DuplicateProfile { get; private set; }
        public RelayCommand DuplicateMap { get; private set; }

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
                if(SelectedProfile != null)
                {
                    Profiles.Remove(SelectedProfile);
                    SelectedProfile = Profiles.FirstOrDefault();
                    _storage.Save(Profiles);
                }
            });

            AddMap = new RelayCommand(() =>
            {
                if(SelectedProfile != null)
                {
                    var m = new KeybindMap { Name = "Map " + (SelectedProfile.Maps.Count + 1) };
                    SelectedProfile.Maps.Add(m);
                    SelectedMap = m;
                    _storage.Save(Profiles);
                }
            });

            RemoveMap = new RelayCommand(() =>
            {
                if(SelectedProfile != null && SelectedMap != null)
                {
                    SelectedProfile.Maps.Remove(SelectedMap);
                    SelectedMap = SelectedProfile.Maps.FirstOrDefault();
                    _storage.Save(Profiles);
                }
            });

            AddKeybind = new RelayCommand(() =>
            {
                if(SelectedMap != null)
                {
                    SelectedMap.Keybinds.Add(new KeybindBinding { Name = "New Action", BlockOriginal = true });
                    _storage.Save(Profiles);
                }
            });

            RemoveKeybind = new RelayCommand(() =>
            {
                if(SelectedMap != null && SelectedMap.Keybinds.Any())
                {
                    SelectedMap.Keybinds.Remove(SelectedMap.Keybinds.Last());
                    _storage.Save(Profiles);
                }
            });

            SaveCommand = new RelayCommand(_ => Save(), _ => true);

            NextMapCommand = new RelayCommand(_ => NextMap(), _ => true);
            PrevMapCommand = new RelayCommand(_ => PrevMap(), _ => true);

            DuplicateProfile = new RelayCommand(() =>
            {
                if(SelectedProfile == null) return;
                var copy = CloneProfile(SelectedProfile);
                copy.Name = MakeCopyName(SelectedProfile.Name, Profiles.Select(p => p.Name));

                Profiles.Add(copy);
                SelectedProfile = copy;
                _storage.Save(Profiles);
            });

            DuplicateMap = new RelayCommand(() =>
            {
                if(SelectedProfile == null || SelectedMap == null) return;

                var copy = CloneMap(SelectedMap);
                copy.Name = MakeCopyName(SelectedMap.Name, SelectedProfile.Maps.Select(m => m.Name));

                SelectedProfile.Maps.Add(copy);
                SelectedMap = copy;
                _storage.Save(Profiles);
            });
        }

        public void AssignKey(System.Windows.Input.KeyEventArgs e)
        {
            // your existing helper logic can stay
        }

        public void AssignDeviceBinding(SimTools.Models.InputBindingResult res)
        {
            // your existing helper logic can stay
        }

        private void NextMap() { /* existing */ }
        private void PrevMap() { /* existing */ }

        // ---- copy helpers (unchanged) ----
        private static Profile CloneProfile(Profile src)
        {
            var p = new Profile { Name = src?.Name };
            if(src?.Maps != null) foreach(var m in src.Maps) p.Maps.Add(CloneMap(m));
            CopyStringIfExists(src, p, "NextMapDevice");
            CopyStringIfExists(src, p, "NextMapDeviceKey");
            CopyStringIfExists(src, p, "PrevMapDevice");
            CopyStringIfExists(src, p, "PrevMapDeviceKey");
            return p;
        }

        private static KeybindMap CloneMap(KeybindMap src)
        {
            var m = new KeybindMap { Name = src?.Name };
            if(src?.Keybinds != null) foreach(var kb in src.Keybinds) m.Keybinds.Add(CloneBinding(kb));
            return m;
        }

        private static KeybindBinding CloneBinding(KeybindBinding src)
        {
            if(src == null) return new KeybindBinding();
            return new KeybindBinding
            {
                Name = src.Name,
                Device = src.Device,
                DeviceKey = src.DeviceKey,
                Key = src.Key,
                Modifiers = src.Modifiers,
                Output = src.Output,
                BlockOriginal = src.BlockOriginal
            };
        }

        private static string MakeCopyName(string baseName, IEnumerable<string> existing)
        {
            var n = baseName;
            int i = 2;
            var set = new HashSet<string>(existing ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            while(set.Contains(n)) n = $"{baseName} ({i++})";
            return n;
        }

        private static void CopyStringIfExists(object src, object dst, string propName)
        {
            if(src == null || dst == null) return;
            var sp = src.GetType().GetProperty(propName);
            var dp = dst.GetType().GetProperty(propName);
            if(sp != null && dp != null && sp.PropertyType == typeof(string) && dp.PropertyType == typeof(string) && dp.CanWrite)
            {
                dp.SetValue(dst, (string)sp.GetValue(src));
            }
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;
using SimTools.Models;

namespace SimTools.Services
{
    /// <summary>
    /// Saves and loads the app’s profiles to a single XML file in %AppData%\SimTools.
    /// - Save(profiles): writes the current profiles.
    /// - Load(): reads them; if the file is missing, creates a simple starter profile and returns it.
    /// </summary>

    public class StorageService
    {
        private readonly string _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimTools");
        private readonly string _file;

        public StorageService(string fileName = "simtools.settings.xml")
        {
            _file = Path.Combine(_folder, fileName);
            Directory.CreateDirectory(_folder);
        }

        public void Save(ObservableCollection<Profile> profiles)
        {
            var ser = new XmlSerializer(typeof(ObservableCollection<Profile>));
            using (var fs = new FileStream(_file, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ser.Serialize(fs, profiles);
            }
        }

        public ObservableCollection<Profile> Load()
        {
            if (!File.Exists(_file))
            {
                var seed = new ObservableCollection<Profile>
                {
                    new Profile
                    {
                        Name = "My Profile",
                        Maps = { new KeybindMap { Name = "Default Map" } }
                    }
                };
                Save(seed);
                return seed;
            }

            try
            {
                var ser = new XmlSerializer(typeof(ObservableCollection<Profile>));
                using (var fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var obj = ser.Deserialize(fs) as ObservableCollection<Profile>;
                    return obj ?? new ObservableCollection<Profile>();
                }
            }
            catch
            {
                return new ObservableCollection<Profile>();
            }
        }
    }
}

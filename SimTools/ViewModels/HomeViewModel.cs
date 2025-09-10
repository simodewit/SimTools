using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SimTools.Services;
using SimTools.Models;

namespace SimTools.ViewModels
{
    public class HomeViewModel
    {
        public ObservableCollection<MediaItem> Media { get; } = new ObservableCollection<MediaItem>();
        public ObservableCollection<RaceEvent> UpcomingRaces { get; } = new ObservableCollection<RaceEvent>();

        public HomeViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            // Media (existing)
            var mediaSvc = new MediaService();
            var mediaItems = await mediaSvc.FetchAsync(40);
            Media.Clear();
            foreach(var m in mediaItems) Media.Add(m);

            // Races — request many more
            var raceSvc = new RaceCalendarService();
            var races = await raceSvc.FetchUpcomingAsync(200); // increased
            UpcomingRaces.Clear();
            foreach(var r in races) UpcomingRaces.Add(r);
        }
    }
}

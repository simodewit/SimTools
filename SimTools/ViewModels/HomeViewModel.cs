using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SimTools.Services;

namespace SimTools.ViewModels
{
    public class HomeViewModel
    {
        // Removed: public ObservableCollection<NewsItem> News { get; } = ...
        public ObservableCollection<MediaItem> Media { get; } = new ObservableCollection<MediaItem>();

        public HomeViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            // Simply load Media; no split, no exclusions.
            var mediaSvc = new MediaService();
            var mediaItems = await mediaSvc.FetchAsync(40 /* or whatever count you prefer */);
            Media.Clear();
            foreach(var m in mediaItems) Media.Add(m);
        }
    }
}

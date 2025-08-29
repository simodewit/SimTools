using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SimTools.Services;

namespace SimTools.ViewModels
{
    public class HomeViewModel
    {
        public ObservableCollection<NewsItem> News { get; } = new ObservableCollection<NewsItem>();
        public ObservableCollection<MediaItem> Media { get; } = new ObservableCollection<MediaItem>();

        public HomeViewModel()
        {
            // Kick off async load (fire-and-forget)
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            // 1) Load News (balanced & capped per series)
            var newsSvc = new NewsService();
            var newsItems = await newsSvc.FetchAsync(40);
            News.Clear();
            foreach (var n in newsItems) News.Add(n);

            // 2) Load Media excluding News links (unique content), balanced & capped
            var excludeLinks = newsItems.Select(n => n.Link);
            var mediaSvc = new MediaService();
            var mediaItems = await mediaSvc.FetchAsync(30, excludeLinks);
            Media.Clear();
            foreach (var m in mediaItems) Media.Add(m);
        }
    }
}

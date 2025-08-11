using SimTools.Helpers;
using SimTools.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SimTools.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly NewsService _news = new NewsService();

        private ObservableCollection<NewsItem> _newsItems = new ObservableCollection<NewsItem>();
        public ObservableCollection<NewsItem> News
        {
            get { return _newsItems; }
            set { Set(ref _newsItems, value); }
        }

        private string _rssUrl = "https://www.motorsport.com/rss/all/news/";
        public string RssUrl
        {
            get { return _rssUrl; }
            set
            {
                if (Set(ref _rssUrl, value))
                {
                    var _ = LoadNews();
                }
            }
        }

        public HomeViewModel()
        {
            var _ = LoadNews();
        }

        private async Task LoadNews()
        {
            _news.RssUrl = RssUrl;
            News = await _news.FetchAsync();
        }
    }
}

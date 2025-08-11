using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;

namespace SimTools.Services
{
    public class NewsItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public DateTime PubDate { get; set; }
        public string Category { get; set; }  // Normalized series (F1, WEC, etc.)

        public NewsItem(string title, string link, DateTime pubDate, string category)
        {
            Title = title;
            Link = link;
            PubDate = pubDate;
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category;
        }
    }

    public class NewsService
    {
        private readonly HttpClient _http = new HttpClient();
        public string RssUrl { get; set; } = "https://www.motorsport.com/rss/all/news/";

        public async Task<ObservableCollection<NewsItem>> FetchAsync(int max = 40)
        {
            try
            {
                var xml = await _http.GetStringAsync(RssUrl);
                var doc = XDocument.Parse(xml);
                var items = doc.Descendants("item")
                    .Select(x =>
                    {
                        var title = (string)(x.Element("title") ?? new XElement("title", "(no title)"));
                        var link = (string)(x.Element("link") ?? new XElement("link", ""));
                        var pub = ParseDate((string)x.Element("pubDate"));
                        var rawCats = x.Elements("category").Select(c => (string)c).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                        var series = NormalizeSeries(title, rawCats);
                        return new NewsItem(title, link, pub, series);
                    })
                    .OrderByDescending(n => n.PubDate)
                    .ToList();

                var balanced = BalanceBySeries(items, max);
                return new ObservableCollection<NewsItem>(balanced);
            }
            catch
            {
                return new ObservableCollection<NewsItem>();
            }
        }

        private static DateTime ParseDate(string s)
        {
            DateTime dt;
            if (DateTime.TryParse(s, out dt)) return dt.ToLocalTime();
            return DateTime.Now;
        }

        // Map item to a racing series (avoid event/circuit labels)
        private static string NormalizeSeries(string title, IList<string> categories)
        {
            var hay = (title + " | " + string.Join(" | ", categories)).ToLowerInvariant();
            Func<string[], bool> hasAny = keys => keys.Any(k => hay.Contains(k.ToLowerInvariant()));

            // Open-wheel ladder
            if (hasAny(new[] { "formula 1", "formula one", "f1", "grand prix" })) return "F1";
            if (hasAny(new[] { "formula 2", "f2" })) return "F2";
            if (hasAny(new[] { "formula 3", "f3" })) return "F3";
            if (hasAny(new[] { "formula 4", "f4" })) return "F4";

            // Endurance/GT
            if (hasAny(new[] { "wec", "world endurance championship" })) return "WEC";
            if (hasAny(new[] { "imsa", "weathertech" })) return "IMSA";
            if (hasAny(new[] { "gt world challenge", "gtwc", "sro", "gt3" })) return "GT3";

            // US + rally
            if (hasAny(new[] { "nascar", "cup series", "xfinity", "truck series" })) return "NASCAR";
            if (hasAny(new[] { "indycar", "indy 500", "indy 200" })) return "IndyCar";
            if (hasAny(new[] { "wrc", "world rally championship", "rally1", "rallying" })) return "WRC";

            // Others
            if (hasAny(new[] { "dtm" })) return "DTM";
            if (hasAny(new[] { "motogp", "moto gp" })) return "MotoGP";
            if (hasAny(new[] { "super formula", "superformula", "sf-23", "sf23" })) return "SuperFormula";

            var known = new HashSet<string>(new[]
            {
                "F1","F2","F3","F4","WEC","IMSA","GT3","NASCAR","IndyCar","WRC","DTM","MotoGP","SuperFormula"
            }, StringComparer.OrdinalIgnoreCase);

            var direct = categories.FirstOrDefault(c => known.Contains(c));
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            return "Other";
        }

        // Balance categories so NASCAR doesn't dominate, and include more series when available.
        private static List<NewsItem> BalanceBySeries(List<NewsItem> items, int max)
        {
            var groups = items
                .GroupBy(n => n.Category)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PubDate).ToList());

            string[] order = {
                "F1","WEC","IMSA","GT3","IndyCar","NASCAR","WRC","DTM","MotoGP","SuperFormula","F2","F3","F4","Other"
            };

            var result = new List<NewsItem>(max);
            int perCategoryCap = 3; // at most 3 per series in the initial passes

            // Round-robin up to cap
            for (int pass = 0; pass < perCategoryCap && result.Count < max; pass++)
            {
                foreach (var cat in order)
                {
                    if (result.Count >= max) break;
                    List<NewsItem> list;
                    if (groups.TryGetValue(cat, out list) && list.Count > 0)
                    {
                        result.Add(list[0]);
                        list.RemoveAt(0);
                    }
                }
            }

            // Fill remaining slots by recency across all remaining categories (still soft-limiting NASCAR)
            if (result.Count < max)
            {
                var leftovers = groups.Values.SelectMany(v => v).OrderByDescending(x => x.PubDate);
                foreach (var n in leftovers)
                {
                    if (result.Count >= max) break;
                    // Soft-limit NASCAR overall
                    int nascarSoFar = result.Count(x => x.Category == "NASCAR");
                    if (n.Category == "NASCAR" && nascarSoFar >= 4) continue;
                    result.Add(n);
                }
            }

            return result;
        }
    }
}

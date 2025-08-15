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

    /// <summary>
    /// Fetches and processes racing news RSS feeds into <see cref="NewsItem"/> objects.
    /// - Combines multiple feeds for different series (F1, WEC, IMSA, NASCAR, etc.).
    /// - Removes duplicates and balances the number of results per series.
    /// - Falls back gracefully if a feed is unavailable.
    /// </summary>

    public class NewsService
    {
        private readonly HttpClient _http = new HttpClient();

        // Primary “all” feed plus per-series feeds to fill underrepresented categories
        private static readonly (string Category, string Url)[] Sources = new[]
        {
            ("F1",            "https://www.motorsport.com/rss/f1/news/"),
            ("F2",            "https://www.motorsport.com/rss/f2/news/"),
            ("F3",            "https://www.motorsport.com/rss/f3/news/"),
            ("F4",            "https://www.motorsport.com/rss/f4/news/"),
            ("WEC",           "https://www.motorsport.com/rss/wec/news/"),
            ("IMSA",          "https://www.motorsport.com/rss/imsa/news/"),
            ("GT3",           "https://www.motorsport.com/rss/gt/news/"),
            ("IndyCar",       "https://www.motorsport.com/rss/indycar/news/"),
            ("NASCAR",        "https://www.motorsport.com/rss/nascar/news/"),
            ("WRC",           "https://www.motorsport.com/rss/wrc/news/"),
            ("DTM",           "https://www.motorsport.com/rss/dtm/news/"),
            ("MotoGP",        "https://www.motorsport.com/rss/motogp/news/"),
            ("SuperFormula",  "https://www.motorsport.com/rss/super-formula/news/"),
            // broad feed as a final catch-all
            ("Other",         "https://www.motorsport.com/rss/all/news/")
        };

        public async Task<ObservableCollection<NewsItem>> FetchAsync(int max = 40)
        {
            try
            {
                var items = new List<NewsItem>();
                var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var src in Sources)
                {
                    List<NewsItem> fromFeed;
                    try
                    {
                        var xml = await _http.GetStringAsync(src.Url);
                        fromFeed = ParseFeed(xml, src.Category);
                    }
                    catch
                    {
                        continue; // skip failing feed
                    }

                    foreach (var it in fromFeed)
                    {
                        if (string.IsNullOrWhiteSpace(it.Link)) continue;
                        var norm = NormalizeLink(it.Link);
                        if (seenLinks.Contains(norm)) continue;
                        seenLinks.Add(norm);
                        items.Add(it);
                    }
                }

                // Sort all items by recency then balance per category
                items = items.OrderByDescending(n => n.PubDate).ToList();
                var balanced = BalanceBySeries(items, max);
                return new ObservableCollection<NewsItem>(balanced);
            }
            catch
            {
                return new ObservableCollection<NewsItem>();
            }
        }

        private List<NewsItem> ParseFeed(string xml, string forcedCategory)
        {
            var doc = XDocument.Parse(xml);
            var list = new List<NewsItem>();

            foreach (var x in doc.Descendants("item"))
            {
                var title = (string)(x.Element("title") ?? new XElement("title", "(no title)"));
                var link = (string)(x.Element("link") ?? new XElement("link", ""));
                var pub = ParseDate((string)x.Element("pubDate"));

                // If feed is series-specific, prefer forced label; otherwise try to normalize
                var cats = x.Elements("category").Select(c => (string)c).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                var series = string.IsNullOrWhiteSpace(forcedCategory) || forcedCategory == "Other"
                    ? NormalizeSeries(title, cats)
                    : forcedCategory;

                list.Add(new NewsItem(title, link, pub, series));
            }

            return list;
        }

        private static DateTime ParseDate(string s)
        {
            DateTime dt;
            if (DateTime.TryParse(s, out dt)) return dt.ToLocalTime();
            return DateTime.Now;
        }

        private static string NormalizeLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link)) return link;
            link = link.Trim();
            if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var idx = link.IndexOf("://");
                if (idx >= 0) link = link.Substring(idx + 3);
            }
            var q = link.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) link = link.Substring(0, q);
            if (link.EndsWith("/")) link = link.Substring(0, link.Length - 1);
            if (link.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) link = link.Substring(4);
            return link.ToLowerInvariant();
        }

        // Map item to a racing series (avoid event/circuit labels)
        private static string NormalizeSeries(string title, IList<string> categories)
        {
            var hay = (title + " | " + string.Join(" | ", categories)).ToLowerInvariant();
            Func<string[], bool> hasAny = keys => keys.Any(k => hay.Contains(k.ToLowerInvariant()));

            if (hasAny(new[] { "formula 1", "formula one", "f1", "grand prix" })) return "F1";
            if (hasAny(new[] { "formula 2", "f2" })) return "F2";
            if (hasAny(new[] { "formula 3", "f3" })) return "F3";
            if (hasAny(new[] { "formula 4", "f4" })) return "F4";

            if (hasAny(new[] { "wec", "world endurance championship" })) return "WEC";
            if (hasAny(new[] { "imsa", "weathertech" })) return "IMSA";
            if (hasAny(new[] { "gt world challenge", "gtwc", "sro", "gt3" })) return "GT3";

            if (hasAny(new[] { "nascar", "cup series", "xfinity", "truck series" })) return "NASCAR";
            if (hasAny(new[] { "indycar", "indy 500", "indy 200" })) return "IndyCar";
            if (hasAny(new[] { "wrc", "world rally championship", "rally1", "rallying" })) return "WRC";

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

        // Limit to 5 per category overall and avoid NASCAR dominance (<= 3)
        private static List<NewsItem> BalanceBySeries(List<NewsItem> items, int max)
        {
            const int perCategoryCap = 5;
            const int nascarOverallCap = 3;

            var groups = items
                .GroupBy(n => n.Category)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PubDate).ToList());

            string[] order = {
                "F1","WEC","IMSA","GT3","IndyCar","NASCAR","WRC","DTM","MotoGP","SuperFormula","F2","F3","F4","Other"
            };

            var result = new List<NewsItem>(max);

            // Round-robin up to perCategoryCap
            for (int pass = 0; pass < perCategoryCap && result.Count < max; pass++)
            {
                foreach (var cat in order)
                {
                    if (result.Count >= max) break;
                    List<NewsItem> list;
                    if (groups.TryGetValue(cat, out list) && list.Count > 0)
                    {
                        if (cat == "NASCAR" && result.Count(x => x.Category == "NASCAR") >= nascarOverallCap)
                            continue;
                        result.Add(list[0]);
                        list.RemoveAt(0);
                    }
                }
            }

            // Fill remaining strictly respecting caps
            if (result.Count < max)
            {
                var leftovers = groups.SelectMany(kv => kv.Value.Select(v => new { Cat = kv.Key, Item = v }))
                                      .OrderByDescending(x => x.Item.PubDate);

                var perCatCounts = result.GroupBy(x => x.Category)
                                         .ToDictionary(g => g.Key, g => g.Count());

                foreach (var x in leftovers)
                {
                    if (result.Count >= max) break;
                    int countForCat;
                    perCatCounts.TryGetValue(x.Cat, out countForCat);
                    if (countForCat >= perCategoryCap) continue;
                    if (x.Cat == "NASCAR" && result.Count(i => i.Category == "NASCAR") >= nascarOverallCap) continue;

                    result.Add(x.Item);
                    perCatCounts[x.Cat] = countForCat + 1;
                }
            }

            return result.Take(max).ToList();
        }
    }
}

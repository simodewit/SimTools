using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimTools.Controls
{
    public partial class InspectorPanel : UserControl
    {
        public InspectorPanel() { InitializeComponent(); }

        public object SelectedObject
        {
            get => DataContext;
            set => DataContext = value == null ? null : new InspectViewModel(value);
        }

        private sealed class InspectViewModel : INotifyPropertyChanged
        {
            private readonly DependencyObject _target;
            public string Title { get; }

            public InspectViewModel(object obj)
            {
                _target = obj as DependencyObject;
                Title = BuildTitle(obj);
                BuildFontOptions();    // <— add the fonts for the dropdown
                Hydrate();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private static string BuildTitle(object obj)
            {
                if(obj is FrameworkElement fe)
                {
                    var name = string.IsNullOrWhiteSpace(fe.Name) ? fe.GetType().Name : fe.Name;
                    return $"{name} ({fe.GetType().Name})";
                }
                return obj?.GetType().Name ?? "(null)";
            }

            // =========================
            // BACKGROUND
            // =========================
            public bool HasBackground => _target is Control || _target is Panel || _target is Border;
            public Color BackgroundColor { get => GetBackgroundColor(); set => SetBackgroundColor(value); }
            public double BackgroundHue { get => ColorToHSV(BackgroundColor).h; set => SetBackgroundHSV(h: value); }
            public double BackgroundSaturation { get => ColorToHSV(BackgroundColor).s; set => SetBackgroundHSV(s: value); }
            public double BackgroundValue { get => ColorToHSV(BackgroundColor).v; set => SetBackgroundHSV(v: value); }
            public double BackgroundAlpha { get => GetBackgroundBrush() is SolidColorBrush b ? b.Color.A / 255.0 : 1.0; set => SetBackgroundAlpha(value); }

            private Brush GetBackgroundBrush()
            {
                if(_target is Border bd) return bd.Background;
                if(_target is Control c) return c.Background;
                if(_target is Panel p) return p.Background;
                return null;
            }
            private void SetBackgroundBrush(Brush b)
            {
                if(_target is Border bd) bd.Background = b;
                else if(_target is Control c) c.Background = b;
                else if(_target is Panel p) p.Background = b;
            }
            private Color GetBackgroundColor()
            {
                var b = GetBackgroundBrush() as SolidColorBrush;
                if(b == null) return Colors.Transparent;
                return b.Color;
            }
            private void SetBackgroundColor(Color c)
            {
                var existing = GetBackgroundBrush() as SolidColorBrush;
                var opacity = existing?.Opacity ?? 1.0;

                var local = new SolidColorBrush(c) { Opacity = opacity };
                SetBackgroundBrush(local);
            }
            private void SetBackgroundAlpha(double a01)
            {
                var b = GetBackgroundBrush() as SolidColorBrush;
                var c = (b?.Color ?? Colors.Transparent);
                c.A = (byte)Math.Round(Math.Max(0, Math.Min(1, a01)) * 255.0);
                SetBackgroundColor(c);
            }
            private void SetBackgroundHSV(double? h = null, double? s = null, double? v = null)
            {
                var (ch, cs, cv) = ColorToHSV(BackgroundColor);
                var nc = FromHSV(h ?? ch, s ?? cs, v ?? cv, BackgroundColor.A);
                SetBackgroundColor(nc);
            }

            // =========================
            // FOREGROUND
            // =========================
            public bool HasForeground => _target is Control || _target is TextBlock;
            public Color ForegroundColor
            {
                get
                {
                    if(_target is Control c && c.Foreground is SolidColorBrush sb1) return sb1.Color;
                    if(_target is TextBlock t && t.Foreground is SolidColorBrush sb2) return sb2.Color;
                    return Colors.White;
                }
                set
                {
                    var b = new SolidColorBrush(value);
                    if(_target is Control c) c.Foreground = b;
                    if(_target is TextBlock t) t.Foreground = b;
                }
            }
            public double ForegroundHue { get => ColorToHSV(ForegroundColor).h; set => SetForegroundHSV(h: value); }
            public double ForegroundSaturation { get => ColorToHSV(ForegroundColor).s; set => SetForegroundHSV(s: value); }
            public double ForegroundValue { get => ColorToHSV(ForegroundColor).v; set => SetForegroundHSV(v: value); }
            private void SetForegroundHSV(double? h = null, double? s = null, double? v = null)
            {
                var (ch, cs, cv) = ColorToHSV(ForegroundColor);
                var nc = FromHSV(h ?? ch, s ?? cs, v ?? cv, 255);
                ForegroundColor = nc;
            }

            // =========================
            // OPACITY
            // =========================
            public bool HasOpacity => _target is UIElement;
            public double Opacity
            {
                get => _target is UIElement u ? u.Opacity : 1.0;
                set { if(_target is UIElement u) u.Opacity = Math.Max(0, Math.Min(1, value)); }
            }

            // =========================
            // FONT SIZE
            // =========================
            public bool HasFontSize => _target is Control || _target is TextBlock;
            public double FontSize
            {
                get
                {
                    if(_target is Control c) return c.FontSize;
                    if(_target is TextBlock t) return t.FontSize;
                    return 12;
                }
                set
                {
                    if(_target is Control c) c.FontSize = value;
                    if(_target is TextBlock t) t.FontSize = value;
                }
            }

            // =========================
            // FONT FAMILY (bundled + grouped)
            // =========================
            public bool HasFontFamily => _target is Control || _target is TextBlock;

            public FontFamily FontFamily
            {
                get
                {
                    if(_target is Control c) return c.FontFamily;
                    if(_target is TextBlock t) return t.FontFamily;
                    return new FontFamily("Segoe UI");
                }
                set
                {
                    if(_target is Control c) c.FontFamily = value;
                    if(_target is TextBlock t) t.FontFamily = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedFontOption)));
                }
            }

            public sealed class FontOption : INotifyPropertyChanged
            {
                public string Name { get; init; }
                public string Category { get; init; }   // "Motorsport", "Game", "Close", "System (installed)"
                public FontFamily Family { get; init; }
                public Visibility ShowCategoryHeader { get; set; } = Visibility.Collapsed;
                public event PropertyChangedEventHandler PropertyChanged;
            }

            public ObservableCollection<FontOption> FontOptions { get; } = new();

            public FontOption SelectedFontOption
            {
                get
                {
                    // Try to select the current element's family if present, else first option
                    return FontOptions.FirstOrDefault(o =>
                        string.Equals(o.Family?.Source, FontFamily?.Source, StringComparison.OrdinalIgnoreCase))
                           ?? FontOptions.FirstOrDefault();
                }
                set { if(value != null) FontFamily = value.Family; }
            }

            private void BuildFontOptions()
            {
                // 1) Bundled fonts (FREE). Place files under Assets/Fonts and set Build Action=Resource.
                // The fragment after # MUST match the font's internal family name.
                var bundled = new (string Category, string Display, string PackFamily)[]
                {
                    // Motorsport (driver names & numbers)
                    ("Motorsport", "Bebas Neue",        "./Assets/Fonts/#Bebas Neue"),
                    ("Motorsport", "Oswald",            "./Assets/Fonts/#Oswald"),
                    ("Motorsport", "Anton",             "./Assets/Fonts/#Anton"),
                    ("Motorsport", "League Gothic",     "./Assets/Fonts/#League Gothic"),
                    ("Motorsport", "Teko",              "./Assets/Fonts/#Teko"),
                    ("Motorsport", "Rajdhani",          "./Assets/Fonts/#Rajdhani"),
                    ("Motorsport", "Saira Condensed",   "./Assets/Fonts/#Saira Condensed"),

                    // Game / HUD / telemetry
                    ("Game", "Orbitron",                "./Assets/Fonts/#Orbitron"),
                    ("Game", "Oxanium",                 "./Assets/Fonts/#Oxanium"),
                    ("Game", "Exo 2",                   "./Assets/Fonts/#Exo 2"),
                    ("Game", "Audiowide",               "./Assets/Fonts/#Audiowide"),
                    ("Game", "Michroma",                "./Assets/Fonts/#Michroma"),
                    ("Game", "DSEG 7-Segment",          "./Assets/Fonts/#DSEG7 Classic"),
                    ("Game", "DSEG 14-Segment",         "./Assets/Fonts/#DSEG14 Classic"),

                    // Close (free substitutes)
                    ("Close", "Antonio",                "./Assets/Fonts/#Antonio"),
                    ("Close", "Roboto Condensed",       "./Assets/Fonts/#Roboto Condensed"),
                    ("Close", "Barlow Condensed",       "./Assets/Fonts/#Barlow Condensed"),
                };

                var list = new List<FontOption>();
                foreach(var (cat, display, packFamily) in bundled)
                {
                    try
                    {
                        var fam = new FontFamily(new Uri("pack://application:,,,/"), packFamily);
                        list.Add(new FontOption { Category = cat, Name = display, Family = fam });
                    }
                    catch
                    {
                        // If a file or family is missing, skip silently.
                    }
                }

                // 2) Add *installed* system lookalikes if present (still free if user installed them)
                string[] tokens = { "Formula1", "F1", "DIN", "Microgramma", "Eurostile", "Bank Gothic", "Compacta" };
                foreach(var f in Fonts.SystemFontFamilies)
                {
                    var src = f.Source ?? string.Empty;
                    if(tokens.Any(t => src.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        if(!list.Any(o => string.Equals(o.Family?.Source, f.Source, StringComparison.OrdinalIgnoreCase)))
                            list.Add(new FontOption { Category = "System (installed)", Name = src, Family = f });
                    }
                }

                // 3) Sort by category, then name; mark first item of each category with a header
                string[] order = { "Motorsport", "Game", "Close", "System (installed)" };
                var ordered = list.OrderBy(o => Array.IndexOf(order, o.Category))
                                  .ThenBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
                                  .ToList();

                string currentCat = null;
                FontOptions.Clear();
                foreach(var item in ordered)
                {
                    if(item.Category != currentCat)
                    {
                        item.ShowCategoryHeader = Visibility.Visible;
                        currentCat = item.Category;
                    }
                    FontOptions.Add(item);
                }

                // Nudge selection so the combo shows a valid entry at first load
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedFontOption)));
            }

            // =========================
            // MISC
            // =========================
            private void Hydrate() { }

            private static bool TryParseThickness(string s, out Thickness t)
            {
                t = new Thickness();
                try
                {
                    var parts = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                    if(parts.Length == 1) t = new Thickness(parts[0]);
                    else if(parts.Length == 2) t = new Thickness(parts[0], parts[1], parts[0], parts[1]);
                    else if(parts.Length == 4) t = new Thickness(parts[0], parts[1], parts[2], parts[3]);
                    else return false;
                    return true;
                }
                catch { return false; }
            }

            private static (double h, double s, double v) ColorToHSV(Color c)
            {
                double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                double h = 0, s, v = max;
                double d = max - min;
                s = max == 0 ? 0 : d / max;
                if(d != 0)
                {
                    if(max == r) h = (g - b) / d + (g < b ? 6 : 0);
                    else if(max == g) h = (b - r) / d + 2;
                    else h = (r - g) / d + 4;
                    h *= 60;
                }
                return (h, s, v);
            }

            private static Color FromHSV(double h, double s, double v, byte a)
            {
                h = (h % 360 + 360) % 360;
                int i = (int)Math.Floor(h / 60);
                double f = h / 60 - i;
                double p = v * (1 - s);
                double q = v * (1 - f * s);
                double t = v * (1 - (1 - f) * s);

                double r = 0, g = 0, b = 0;
                switch(i % 6)
                {
                    case 0: r = v; g = t; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = t; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = t; g = p; b = v; break;
                    case 5: r = v; g = p; b = q; break;
                }
                return Color.FromArgb(
                    a,
                    (byte)Math.Round(r * 255),
                    (byte)Math.Round(g * 255),
                    (byte)Math.Round(b * 255));
            }
        }
    }
}

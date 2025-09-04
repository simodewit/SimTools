using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimTools.ViewModels
{
    public interface IKeybindResolver { string GetDisplayForAction(string actionId); }
    internal sealed class NullKeybindResolver : IKeybindResolver
    {
        public static readonly NullKeybindResolver Instance = new NullKeybindResolver();
        private NullKeybindResolver() { }
        public string GetDisplayForAction(string actionId) => "(unbound)";
    }

    public interface IMapNeighborResolver
    {
        string GetPreviousMapName(AppState state);
        string GetNextMapName(AppState state);
    }
    internal sealed class NullMapNeighborResolver : IMapNeighborResolver
    {
        public static readonly NullMapNeighborResolver Instance = new NullMapNeighborResolver();
        private NullMapNeighborResolver() { }
        public string GetPreviousMapName(AppState state) => string.Empty;
        public string GetNextMapName(AppState state) => string.Empty;
    }

    public sealed class OverlayToolDescriptor
    {
        public string Id { get; }
        public string Name { get; }
        public Func<FrameworkElement> BuildPreview { get; }
        public OverlayToolDescriptor(string id, string name, Func<FrameworkElement> buildPreview)
        {
            Id = id; Name = name; BuildPreview = buildPreview;
        }
        public override string ToString() => Name;
    }

    public sealed class OverlayEditorViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public RelayCommand OpenOverlay { get; private set; }

        public ObservableCollection<OverlayElement> OverlayElements { get; } = new ObservableCollection<OverlayElement>();

        // Tool selection
        public ObservableCollection<OverlayToolDescriptor> Tools { get; } = new ObservableCollection<OverlayToolDescriptor>();
        private OverlayToolDescriptor _selectedTool;
        public OverlayToolDescriptor SelectedTool
        {
            get => _selectedTool;
            set { if (!ReferenceEquals(_selectedTool, value)) { _selectedTool = value; Raise(nameof(SelectedTool)); } }
        }

        // Selected visual (for inspector)
        private object _selectedVisual;
        public object SelectedVisual
        {
            get => _selectedVisual;
            set { if (!ReferenceEquals(_selectedVisual, value)) { _selectedVisual = value; Raise(nameof(SelectedVisual)); } }
        }

        // Keybinds active (for bottom tabs)
        private bool _isKeybindsActive;
        public bool IsKeybindsActive
        {
            get => _isKeybindsActive;
            set { if (_isKeybindsActive != value) { _isKeybindsActive = value; Raise(nameof(IsKeybindsActive)); } }
        }

        // Keybind resolver (injected)
        private IKeybindResolver _keybinds = NullKeybindResolver.Instance;
        public void SetKeybindResolver(IKeybindResolver resolver)
        {
            _keybinds = resolver ?? NullKeybindResolver.Instance;
            Raise(nameof(PrevMapKeyDisplay)); Raise(nameof(NextMapKeyDisplay));
        }
        public string PrevMapKeyDisplay => _keybinds.GetDisplayForAction("Maps.Previous");
        public string NextMapKeyDisplay => _keybinds.GetDisplayForAction("Maps.Next");

        // Map neighbors (injected)
        private IMapNeighborResolver _neighbors = NullMapNeighborResolver.Instance;
        public void SetMapNeighborResolver(IMapNeighborResolver resolver)
        {
            _neighbors = resolver ?? NullMapNeighborResolver.Instance;
            Raise(nameof(PrevMapName)); Raise(nameof(NextMapName));
        }
        public string CurrentMapName => (State != null && State.CurrentMap != null && !string.IsNullOrEmpty(State.CurrentMap.Name))
            ? State.CurrentMap.Name : "(no map)";
        public string PrevMapName { get { var n = _neighbors.GetPreviousMapName(State); return string.IsNullOrEmpty(n) ? "(top)" : n; } }
        public string NextMapName { get { var n = _neighbors.GetNextMapName(State); return string.IsNullOrEmpty(n) ? "(bottom)" : n; } }

        // OVERLAY PANEL BACKGROUND (HSV + Alpha) – drives OverlayTheme "Overlay.PanelBrush" at runtime
        private SolidColorBrush _overlayPanelBrush;
        public SolidColorBrush OverlayPanelBrush
        {
            get => _overlayPanelBrush;
            private set
            {
                if (!ReferenceEquals(_overlayPanelBrush, value))
                {
                    _overlayPanelBrush = value;
                    Application.Current.Resources["Overlay.PanelBrush"] = value;
                    Raise(nameof(OverlayPanelBrush));
                }
            }
        }

        private double _overlayHue;
        public double OverlayHue { get => _overlayHue; set { if (_overlayHue != value) { _overlayHue = value; Raise(nameof(OverlayHue)); UpdateOverlayFromHSV(); } } }
        private double _overlaySaturation;
        public double OverlaySaturation { get => _overlaySaturation; set { if (_overlaySaturation != value) { _overlaySaturation = value; Raise(nameof(OverlaySaturation)); UpdateOverlayFromHSV(); } } }
        private double _overlayValue;
        public double OverlayValue { get => _overlayValue; set { if (_overlayValue != value) { _overlayValue = value; Raise(nameof(OverlayValue)); UpdateOverlayFromHSV(); } } }
        private double _overlayAlpha = 0.95;
        public double OverlayAlpha { get => _overlayAlpha; set { if (_overlayAlpha != value) { _overlayAlpha = value; Raise(nameof(OverlayAlpha)); UpdateOverlayFromHSV(); } } }

        private void UpdateOverlayFromHSV()
        {
            var c = FromHSV(_overlayHue, _overlaySaturation, _overlayValue);
            c.A = (byte)Math.Round(Math.Max(0, Math.Min(1, _overlayAlpha)) * 255.0);
            if (OverlayPanelBrush == null) OverlayPanelBrush = new SolidColorBrush(c);
            else OverlayPanelBrush.Color = c;
            Application.Current.Resources["Overlay.PanelBrush"] = OverlayPanelBrush;
        }

        // SAMPLE FONT PREVIEW
        private string _fontSampleText = "The quick brown fox jumps over the lazy dog";
        public string FontSampleText { get => _fontSampleText; set { if (_fontSampleText != value) { _fontSampleText = value; Raise(nameof(FontSampleText)); } } }

        // Commands
        public OverlayEditorViewModel(AppState state)
        {
            State = state;
            OpenOverlay = new RelayCommand(_ => OpenOverlayWindow());

            // Initial overlay elements for the overlay window (not the editor preview)
            OverlayElements.Add(new OverlayElement { X = 40, Y = 30, Type = "MappingIndicator" });

            // Default overlay panel brush from theme (if present)
            var existing = Application.Current.Resources["Overlay.PanelBrush"] as SolidColorBrush;
            if (existing != null)
            {
                OverlayPanelBrush = existing;
                var hsv = ToHSV(existing.Color);
                _overlayHue = hsv.h; _overlaySaturation = hsv.s; _overlayValue = hsv.v; _overlayAlpha = existing.Color.A / 255.0;
            }
            else
            {
                _overlayHue = 215; _overlaySaturation = 0.15; _overlayValue = 0.13; _overlayAlpha = 0.95;
                UpdateOverlayFromHSV();
            }

            // Register available tools (modular)
            Tools.Add(new OverlayToolDescriptor(
                id: "mapswitcher",
                name: "Map Switcher",
                buildPreview: BuildMapSwitcherPreview));

            Tools.Add(new OverlayToolDescriptor(
                id: "keybinds",
                name: "Keybinds",
                buildPreview: BuildKeybindsPreview));

            SelectedTool = Tools.FirstOrDefault();
        }

        private void OpenOverlayWindow()
        {
            var owner = Application.Current?.Windows.OfType<Window>()?.FirstOrDefault(w => w.IsActive);

            var picker = new SimTools.Views.ScreenPickerWindow
            {
                Owner = owner
            };

            if(picker.ShowDialog() != true || picker.Selected == null)
                return;

            var win = new SimTools.Views.OverlayWindow
            {
                DataContext = this,
                TargetScreenBoundsPx = picker.Selected.Screen.Bounds
            };

            win.Show();
        }

        // --- Preview builders (kept small and self-contained) ---
        private FrameworkElement BuildMapSwitcherPreview()
        {
            var panel = new Border
            {
                Background = (Brush)Application.Current.Resources["Overlay.PanelBrush"],
                BorderBrush = (Brush)Application.Current.Resources["Brush.OverlayBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Child = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = new GridLength(8) },
                        new RowDefinition { Height = GridLength.Auto }
                    }
                }
            };
            var g = (Grid)panel.Child;

            var hdr = new TextBlock { Text = "Map Switcher", Style = (Style)Application.Current.Resources["OverlayText.Heading"] };
            g.Children.Add(hdr);

            var names = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            names.Children.Add(new TextBlock { Text = PrevMapName, Style = (Style)Application.Current.Resources["OverlayText.Label"] });
            var cur = new TextBlock { Text = CurrentMapName, Style = (Style)Application.Current.Resources["OverlayText.Title"] };
            names.Children.Add(cur);
            names.Children.Add(new TextBlock { Text = NextMapName, Style = (Style)Application.Current.Resources["OverlayText.Label"] });
            Grid.SetRow(names, 1);
            g.Children.Add(names);

            var sep = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 8, 0, 8) };
            Grid.SetRow(sep, 2);
            g.Children.Add(sep);

            var keys = new StackPanel { Orientation = Orientation.Horizontal };
            var prev = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            prev.Children.Add(new TextBlock { Text = "Prev", Style = (Style)Application.Current.Resources["OverlayText.Label"] });
            prev.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock { Text = PrevMapKeyDisplay, FontFamily = (FontFamily)Application.Current.Resources["Overlay.FontFamily"], FontSize = 12 }
            });
            keys.Children.Add(prev);

            var next = new StackPanel();
            next.Children.Add(new TextBlock { Text = "Next", Style = (Style)Application.Current.Resources["OverlayText.Label"] });
            next.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock { Text = NextMapKeyDisplay, FontFamily = (FontFamily)Application.Current.Resources["Overlay.FontFamily"], FontSize = 12 }
            });
            keys.Children.Add(next);
            Grid.SetRow(keys, 4);
            g.Children.Add(keys);

            // Name for easier identification in inspector
            panel.Name = "MapSwitcherPanel";
            return panel;
        }

        private FrameworkElement BuildKeybindsPreview()
        {
            var panel = new Border
            {
                Background = (Brush)Application.Current.Resources["Overlay.PanelBrush"],
                BorderBrush = (Brush)Application.Current.Resources["Brush.OverlayBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Keybinds", Style = (Style)Application.Current.Resources["OverlayText.Heading"] },
                        new TextBlock { Text = "Configure your keybinds in the Keybinds page.", Style = (Style)Application.Current.Resources["OverlayText.Label"], Margin=new Thickness(0,6,0,0) }
                    }
                }
            };

            panel.Name = "KeybindsPanel";
            return panel;
        }

        // --- Color helpers ---
        private static (double h, double s, double v) ToHSV(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double h = 0, s, v = max;
            double d = max - min;
            s = max == 0 ? 0 : d / max;
            if (d != 0)
            {
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
            return (h, s, v);
        }
        private static Color FromHSV(double h, double s, double v, byte? a = null)
        {
            h = (h % 360 + 360) % 360; // normalize
            int i = (int)Math.Floor(h / 60);
            double f = h / 60 - i;
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;
            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb(a ?? (byte)255, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
    }
}

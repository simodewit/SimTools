using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SimTools.ViewModels
{
    public interface IKeybindResolver { string GetDisplayForAction(string actionId); }
    internal sealed class NullKeybindResolver : IKeybindResolver
    {
        public static readonly NullKeybindResolver Instance = new NullKeybindResolver();
        public string GetDisplayForAction(string actionId) { return "(unassigned)"; }
    }

    public interface IMapNeighborResolver
    {
        string GetPrevMapName(AppState state);
        string GetNextMapName(AppState state);
    }
    internal sealed class NullMapNeighborResolver : IMapNeighborResolver
    {
        public static readonly NullMapNeighborResolver Instance = new NullMapNeighborResolver();
        public string GetPrevMapName(AppState state) { return null; }
        public string GetNextMapName(AppState state) { return null; }
    }

    public class OverlayEditorViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public RelayCommand OpenOverlay { get; private set; }
        public RelayCommand ResetOverlayPanelToTheme { get; private set; }

        public ObservableCollection<OverlayElement> OverlayElements { get; private set; }

        private OverlayElement _selectedElement;
        public OverlayElement SelectedElement
        {
            get { return _selectedElement; }
            set { if (!object.ReferenceEquals(_selectedElement, value)) { _selectedElement = value; Raise(nameof(SelectedElement)); } }
        }

        private bool _isKeybindsActive;
        public bool IsKeybindsActive
        {
            get { return _isKeybindsActive; }
            set { if (_isKeybindsActive != value) { _isKeybindsActive = value; Raise(nameof(IsKeybindsActive)); } }
        }

        // KEYBINDS
        private IKeybindResolver _keybinds = NullKeybindResolver.Instance;
        public void SetKeybindResolver(IKeybindResolver resolver) { _keybinds = resolver ?? NullKeybindResolver.Instance; Raise(nameof(PrevMapKeyDisplay)); Raise(nameof(NextMapKeyDisplay)); }
        public string PrevMapKeyDisplay { get { return _keybinds.GetDisplayForAction("Maps.Previous"); } }
        public string NextMapKeyDisplay { get { return _keybinds.GetDisplayForAction("Maps.Next"); } }

        // MAP NEIGHBORS
        private IMapNeighborResolver _neighbors = NullMapNeighborResolver.Instance;
        public void SetMapNeighborResolver(IMapNeighborResolver resolver) { _neighbors = resolver ?? NullMapNeighborResolver.Instance; Raise(nameof(PrevMapName)); Raise(nameof(NextMapName)); }
        public string CurrentMapName { get { return (State != null && State.CurrentMap != null && !string.IsNullOrEmpty(State.CurrentMap.Name)) ? State.CurrentMap.Name : "(no map)"; } }
        public string PrevMapName { get { var n = _neighbors.GetPrevMapName(State); return string.IsNullOrEmpty(n) ? "(top)" : n; } }
        public string NextMapName { get { var n = _neighbors.GetNextMapName(State); return string.IsNullOrEmpty(n) ? "(bottom)" : n; } }

        // OVERLAY PANEL BACKGROUND (HSV + Alpha)
        private SolidColorBrush _overlayPanelBrush;
        public SolidColorBrush OverlayPanelBrush
        {
            get { return _overlayPanelBrush; }
            private set { if (!object.ReferenceEquals(_overlayPanelBrush, value)) { _overlayPanelBrush = value; Raise(nameof(OverlayPanelBrush)); } }
        }

        private double _overlayHue;
        public double OverlayHue { get { return _overlayHue; } set { double v = NormalizeHue(value); if (Math.Abs(_overlayHue - v) > 0.0001) { _overlayHue = v; Raise(nameof(OverlayHue)); UpdateOverlayFromHSV(); } } }
        private double _overlaySaturation;
        public double OverlaySaturation { get { return _overlaySaturation; } set { double v = Clamp01(value); if (Math.Abs(_overlaySaturation - v) > 0.0001) { _overlaySaturation = v; Raise(nameof(OverlaySaturation)); UpdateOverlayFromHSV(); } } }
        private double _overlayValue;
        public double OverlayValue { get { return _overlayValue; } set { double v = Clamp01(value); if (Math.Abs(_overlayValue - v) > 0.0001) { _overlayValue = v; Raise(nameof(OverlayValue)); UpdateOverlayFromHSV(); } } }
        private double _overlayPanelOpacity = 1.0;  // solid by default
        public double OverlayPanelOpacity { get { return _overlayPanelOpacity; } set { double v = Clamp01(value); if (Math.Abs(_overlayPanelOpacity - v) > 0.0001) { _overlayPanelOpacity = v; Raise(nameof(OverlayPanelOpacity)); UpdateOverlayFromHSV(); } } }

        private string _overlayPanelColorHex = "#1D2024";
        public string OverlayPanelColorHex
        {
            get { return _overlayPanelColorHex; }
            set
            {
                if (_overlayPanelColorHex != value)
                {
                    _overlayPanelColorHex = value; Raise(nameof(OverlayPanelColorHex));
                    Color rgb;
                    if (TryParseHexRRGGBB(_overlayPanelColorHex, out rgb))
                    {
                        double h, s, v; RGBtoHSV(rgb.R, rgb.G, rgb.B, out h, out s, out v);
                        _overlayHue = h; Raise(nameof(OverlayHue));
                        _overlaySaturation = s; Raise(nameof(OverlaySaturation));
                        _overlayValue = v; Raise(nameof(OverlayValue));
                        UpdateOverlayFromHSV();
                    }
                }
            }
        }

        private void UpdateOverlayFromHSV()
        {
            byte r, g, b; HSVtoRGB(_overlayHue, _overlaySaturation, _overlayValue, out r, out g, out b);
            _overlayPanelColorHex = "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2"); Raise(nameof(OverlayPanelColorHex));
            byte a = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(_overlayPanelOpacity * 255.0)));
            var color = Color.FromArgb(a, r, g, b);
            var brush = new SolidColorBrush(color);
            OverlayPanelBrush = brush;
            // >>> Push to global resource so EVERY panel updates
            ApplyOverlayBrushToResources(brush);
        }

        private void ApplyOverlayBrushToResources(SolidColorBrush brush)
        {
            try
            {
                if (brush != null && Application.Current != null)
                {
                    // Replace the resource object so DynamicResource consumers update
                    Application.Current.Resources["Overlay.PanelBrush"] = brush;
                }
            }
            catch { /* ignore */ }
        }

        // FONTS
        public ObservableCollection<FontFamily> FontFamilies { get; private set; }
        private FontFamily _selectedFontFamily;
        public FontFamily SelectedFontFamily
        {
            get { return _selectedFontFamily; }
            set
            {
                if (!Equals(_selectedFontFamily, value))
                {
                    _selectedFontFamily = value; Raise(nameof(SelectedFontFamily));
                    // >>> Push to global resource so EVERY panel updates
                    ApplyOverlayFontToResources(_selectedFontFamily);
                }
            }
        }

        private void ApplyOverlayFontToResources(FontFamily font)
        {
            try
            {
                if (font != null && Application.Current != null)
                {
                    Application.Current.Resources["Overlay.FontFamily"] = font;
                }
            }
            catch { /* ignore */ }
        }

        private string _fontSampleText = "The quick brown fox jumps over the lazy dog — 0123456789";
        public string FontSampleText { get { return _fontSampleText; } set { if (_fontSampleText != value) { _fontSampleText = value; Raise(nameof(FontSampleText)); } } }
        private double _fontPreviewSize = 20;
        public double FontPreviewSize { get { return _fontPreviewSize; } set { if (Math.Abs(_fontPreviewSize - value) > 0.0001) { _fontPreviewSize = value; Raise(nameof(FontPreviewSize)); } } }

        public OverlayEditorViewModel(AppState state)
        {
            State = state;
            OverlayElements = new ObservableCollection<OverlayElement>();

            OpenOverlay = new RelayCommand(delegate { var win = new Views.OverlayWindow { DataContext = this }; win.Show(); });
            ResetOverlayPanelToTheme = new RelayCommand(delegate { LoadOverlayDefaults(); });

            if (OverlayElements.Count == 0) { AddElement("MapSwitcher", 40, 40); }

            var inpc = State as INotifyPropertyChanged;
            if (inpc != null) { inpc.PropertyChanged += delegate { RefreshAll(); }; }

            FontFamilies = new ObservableCollection<FontFamily>(Fonts.SystemFontFamilies.OrderBy(f => f.Source));
            SelectedFontFamily = FontFamilies.FirstOrDefault() ?? new FontFamily("Segoe UI");

            LoadOverlayDefaults();        // sets color/opacity + pushes resources
            ApplyOverlayFontToResources(SelectedFontFamily); // push font resource
        }

        private void LoadOverlayDefaults()
        {
            Color baseColor = Color.FromRgb(0x1D, 0x20, 0x24); // Color.SurfaceAlt
            try
            {
                object c = Application.Current != null ? Application.Current.Resources["Color.SurfaceAlt"] : null;
                if (c is Color) baseColor = (Color)c;
            }
            catch { }

            double h, s, v; RGBtoHSV(baseColor.R, baseColor.G, baseColor.B, out h, out s, out v);
            _overlayHue = h; Raise(nameof(OverlayHue));
            _overlaySaturation = s; Raise(nameof(OverlaySaturation));
            _overlayValue = v; Raise(nameof(OverlayValue));
            _overlayPanelOpacity = 1.0; Raise(nameof(OverlayPanelOpacity)); // solid default
            UpdateOverlayFromHSV(); // also pushes the brush to resources
        }

        public void RefreshAll()
        {
            Raise(nameof(CurrentMapName)); Raise(nameof(PrevMapName)); Raise(nameof(NextMapName));
            Raise(nameof(PrevMapKeyDisplay)); Raise(nameof(NextMapKeyDisplay));
            Raise(nameof(OverlayPanelBrush));
        }

        public void AddElement(string type, double x, double y)
        {
            var el = new OverlayElement { Type = type, X = x, Y = y };
            OverlayElements.Add(el);
            SelectedElement = el;
        }

        public void SelectElement(OverlayElement element) { SelectedElement = element; }

        // utilities
        private static double Clamp01(double v) { if (v < 0) return 0; if (v > 1) return 1; return v; }
        private static double NormalizeHue(double v) { if (double.IsNaN(v) || double.IsInfinity(v)) return 0; while (v < 0) v += 360.0; while (v >= 360.0) v -= 360.0; return v; }

        private static bool TryParseHexRRGGBB(string text, out Color rgb)
        {
            rgb = Color.FromRgb(0, 0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim(); if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length != 6) return false;
            byte r, g, b;
            if (byte.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                byte.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                byte.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            { rgb = Color.FromRgb(r, g, b); return true; }
            return false;
        }

        private static void HSVtoRGB(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            double c = v * s, hh = h / 60.0, x = c * (1 - Math.Abs(hh % 2 - 1)), m = v - c;
            double rr = 0, gg = 0, bb = 0;
            if (hh >= 0 && hh < 1) { rr = c; gg = x; bb = 0; }
            else if (hh < 2) { rr = x; gg = c; bb = 0; }
            else if (hh < 3) { rr = 0; gg = c; bb = x; }
            else if (hh < 4) { rr = 0; gg = x; bb = c; }
            else if (hh < 5) { rr = x; gg = 0; bb = c; }
            else { rr = c; gg = 0; bb = x; }
            r = (byte)Math.Round((rr + m) * 255.0); g = (byte)Math.Round((gg + m) * 255.0); b = (byte)Math.Round((bb + m) * 255.0);
        }

        private static void RGBtoHSV(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
            double max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb)), d = max - min;
            if (d < 1e-6) h = 0;
            else if (max == rr) h = 60 * (((gg - bb) / d) % 6);
            else if (max == gg) h = 60 * (((bb - rr) / d) + 2);
            else h = 60 * (((rr - gg) / d) + 4);
            if (h < 0) h += 360;
            s = (max <= 0) ? 0 : (d / max); v = max;
        }
    }
}

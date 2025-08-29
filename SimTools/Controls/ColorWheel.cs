using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimTools.Controls
{
    public class ColorWheel : FrameworkElement
    {
        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(nameof(Hue), typeof(double), typeof(ColorWheel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHSChanged, CoerceHue));

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register(nameof(Saturation), typeof(double), typeof(ColorWheel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHSChanged, Coerce01));

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorWheel),
                new FrameworkPropertyMetadata(Colors.White));

        public double Hue { get { return (double)GetValue(HueProperty); } set { SetValue(HueProperty, value); } }
        public double Saturation { get { return (double)GetValue(SaturationProperty); } set { SetValue(SaturationProperty, value); } }
        public Color SelectedColor { get { return (Color)GetValue(SelectedColorProperty); } set { SetValue(SelectedColorProperty, value); } }

        private static object CoerceHue(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue; if (double.IsNaN(v) || double.IsInfinity(v)) v = 0.0;
            while (v < 0) v += 360.0; while (v >= 360.0) v -= 360.0; return v;
        }
        private static object Coerce01(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue; if (double.IsNaN(v) || double.IsInfinity(v)) v = 0.0;
            if (v < 0) v = 0; if (v > 1) v = 1; return v;
        }
        private static void OnHSChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cw = (ColorWheel)d; cw.UpdateSelectedColor(); cw.InvalidateVisual();
        }

        private ImageSource _cache; private int _cachedSize;

        public ColorWheel()
        {
            Focusable = false; Width = 180; Height = 180; SnapsToDevicePixels = true;
            MouseDown += (s, e) => { CaptureMouse(); UpdateFromPoint(e.GetPosition(this)); };
            MouseMove += (s, e) => { if (IsMouseCaptured) UpdateFromPoint(e.GetPosition(this)); };
            MouseUp += (s, e) => { if (IsMouseCaptured) ReleaseMouseCapture(); };
        }

        private void UpdateFromPoint(Point p)
        {
            double size = Math.Min(ActualWidth, ActualHeight); if (size <= 2) return;
            double cx = ActualWidth / 2.0, cy = ActualHeight / 2.0, dx = p.X - cx, dy = p.Y - cy;
            double r = Math.Sqrt(dx * dx + dy * dy), radius = (size / 2.0) - 1.0;
            if (r > radius) r = radius; double sat = radius > 0 ? (r / radius) : 0;
            double angle = Math.Atan2(dy, dx); double hue = angle * 180.0 / Math.PI; if (hue < 0) hue += 360.0;
            Hue = hue; Saturation = sat;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            int pxSize = Math.Max(64, (int)Math.Round(Math.Min(ActualWidth, ActualHeight)));
            if (_cache == null || _cachedSize != pxSize) { _cache = RenderWheel(pxSize); _cachedSize = pxSize; }
            dc.DrawImage(_cache, new Rect((ActualWidth - pxSize) / 2.0, (ActualHeight - pxSize) / 2.0, pxSize, pxSize));

            double cx = ActualWidth / 2.0, cy = ActualHeight / 2.0, radius = (pxSize / 2.0) - 1.0;
            double radAngle = Hue * Math.PI / 180.0, r = Saturation * radius;
            Point sel = new Point(cx + Math.Cos(radAngle) * r, cy + Math.Sin(radAngle) * r);
            dc.DrawEllipse(null, new Pen(Brushes.White, 2), sel, 6, 6);
            dc.DrawEllipse(null, new Pen(Brushes.Black, 1), sel, 7, 7);
        }

        private static ImageSource RenderWheel(int size)
        {
            int w = size, h = size; double cx = w / 2.0, cy = h / 2.0, radius = Math.Min(cx, cy) - 1.0;
            var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
            int stride = w * 4; byte[] pixels = new byte[h * stride];

            for (int y = 0; y < h; y++)
            {
                int rowIndex = y * stride; double dy = y - cy;
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx; double dist = Math.Sqrt(dx * dx + dy * dy); int offset = rowIndex + x * 4;
                    if (dist <= radius)
                    {
                        double sat = dist / radius; double angle = Math.Atan2(dy, dx);
                        double hue = angle * 180.0 / Math.PI; if (hue < 0) hue += 360.0;
                        byte r, g, b; HSVtoRGB(hue, sat, 1.0, out r, out g, out b);
                        pixels[offset + 0] = b; pixels[offset + 1] = g; pixels[offset + 2] = r; pixels[offset + 3] = 255;
                    }
                    else { pixels[offset + 0] = 0; pixels[offset + 1] = 0; pixels[offset + 2] = 0; pixels[offset + 3] = 0; }
                }
            }
            bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0); return bmp;
        }

        private void UpdateSelectedColor()
        {
            byte r, g, b; HSVtoRGB(Hue, Saturation, 1.0, out r, out g, out b);
            SelectedColor = Color.FromRgb(r, g, b);
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
            r = (byte)Math.Round((rr + m) * 255.0);
            g = (byte)Math.Round((gg + m) * 255.0);
            b = (byte)Math.Round((bb + m) * 255.0);
        }
    }
}

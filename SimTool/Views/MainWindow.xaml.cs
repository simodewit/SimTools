using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimTools.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }

    static class MonitorHelpers
    {
        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;        // <- excludes the taskbar
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public static Rect GetWorkArea(Window w)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMon, ref mi)) throw new System.ComponentModel.Win32Exception();

            // convert device pixels -> WPF DIPs
            var source = HwndSource.FromHwnd(hwnd);
            var m = source.CompositionTarget.TransformFromDevice;
            var tl = m.Transform(new System.Windows.Point(mi.rcWork.Left, mi.rcWork.Top));
            var br = m.Transform(new System.Windows.Point(mi.rcWork.Right, mi.rcWork.Bottom));
            return new Rect(tl, br);
        }
    }

    public static class WindowWorkAreaHelper
    {
        public static void MaximizeToWorkArea(Window window)
        {
            var wa = MonitorHelpers.GetWorkArea(window);
            window.WindowState = WindowState.Normal;
            window.Left = wa.Left;
            window.Top = wa.Top;
            window.Width = wa.Width;
            window.Height = wa.Height;
        }
    }
}

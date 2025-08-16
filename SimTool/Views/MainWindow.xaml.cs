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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                ApplyMaximizedWorkArea(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void ApplyMaximizedWorkArea(IntPtr hwnd, IntPtr lParam)
        {
            // Get monitor info (in device pixels)
            IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMon, ref mi)) return;

            // MINMAXINFO also uses device pixels, so no DPI conversion needed here.
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // rcMonitor = whole monitor, rcWork = taskbar-safe work area
            RECT rcWork = mi.rcWork;
            RECT rcMon = mi.rcMonitor;

            // Top-left offset of work area within the monitor
            mmi.ptMaxPosition.x = Math.Max(0, rcWork.Left - rcMon.Left);
            mmi.ptMaxPosition.y = Math.Max(0, rcWork.Top - rcMon.Top);

            // Size of the work area
            mmi.ptMaxSize.x = Math.Max(0, rcWork.Right - rcWork.Left);
            mmi.ptMaxSize.y = Math.Max(0, rcWork.Bottom - rcWork.Top);

            // Optional: also clamp tracking size so dragging to edges behaves nicely
            mmi.ptMaxTrackSize = mmi.ptMaxSize;

            Marshal.StructureToPtr(mmi, lParam, fDeleteOld: false);
        }

        // -------- Win32 interop ----------
        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}

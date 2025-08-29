using SimTools.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
                ApplyMinSizeAndWorkArea(hwnd, lParam);
                handled = true; // we fully handled it
            }
            return IntPtr.Zero;
        }

        private void ApplyMinSizeAndWorkArea(IntPtr hwnd, IntPtr lParam)
        {
            // Get monitor info (device pixels)
            IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMon, ref mi)) return;

            // Read existing MINMAXINFO (device pixels)
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // ----- A) Work area for maximized size/position (keeps taskbar visible)
            RECT rcWork = mi.rcWork;
            RECT rcMon = mi.rcMonitor;

            // top-left of work area relative to monitor
            mmi.ptMaxPosition.x = Math.Max(0, rcWork.Left - rcMon.Left);
            mmi.ptMaxPosition.y = Math.Max(0, rcWork.Top - rcMon.Top);

            // size of the work area
            mmi.ptMaxSize.x = Math.Max(0, rcWork.Right - rcWork.Left);
            mmi.ptMaxSize.y = Math.Max(0, rcWork.Bottom - rcWork.Top);

            // optional: also clamp tracking max to work area
            mmi.ptMaxTrackSize = mmi.ptMaxSize;

            // ----- B) Minimum track size from WPF MinWidth/MinHeight (convert to device pixels)
            var src = HwndSource.FromHwnd(hwnd);
            var m = src?.CompositionTarget?.TransformToDevice ?? Matrix.Identity; // DPI

            int minW = (int)Math.Ceiling(this.MinWidth * m.M11);
            int minH = (int)Math.Ceiling(this.MinHeight * m.M22);

            if (minW > 0) mmi.ptMinTrackSize.x = Math.Max(mmi.ptMinTrackSize.x, minW);
            if (minH > 0) mmi.ptMinTrackSize.y = Math.Max(mmi.ptMinTrackSize.y, minH);

            // write back
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

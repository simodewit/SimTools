using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SimTools.Helpers
{
    public static class MinSizeHook
    {
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        public static void Attach(Window window)
        {
            window.SourceInitialized += (_, __) =>
            {
                var src = (HwndSource)PresentationSource.FromVisual(window);
                if (src == null) return;

                src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg == WM_GETMINMAXINFO)
                    {
                        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                        // Respect per-monitor DPI
                        Matrix m = src.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
                        int minW = (int)Math.Ceiling(window.MinWidth * m.M11);
                        int minH = (int)Math.Ceiling(window.MinHeight * m.M22);

                        if (minW > 0) mmi.ptMinTrackSize.X = Math.Max(mmi.ptMinTrackSize.X, minW);
                        if (minH > 0) mmi.ptMinTrackSize.Y = Math.Max(mmi.ptMinTrackSize.Y, minH);

                        Marshal.StructureToPtr(mmi, lParam, true);
                    }
                    return IntPtr.Zero;
                });
            };
        }
    }
}

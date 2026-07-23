// filename: Services/WindowResizeHelper.cs
// Enables resize on WindowStyle=None / AllowsTransparency=True WPF windows.
//
// WPF strips all native window chrome (including resize borders) when you set
// WindowStyle=None + AllowsTransparency=True.  The fix is two Win32 calls:
//   1. Add WS_THICKFRAME to the window style so the OS knows it can be resized.
//   2. Hook WM_NCHITTEST and return the correct HT* value when the cursor is
//      within ResizePx pixels of any edge or corner so WPF passes drag events
//      to the OS resizer rather than the client area.
//
// Usage:  call WindowResizeHelper.Enable(this) from OnSourceInitialized.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OverlayMVP.Services
{
    public static class WindowResizeHelper
    {
        // Width of the invisible grab zone around each edge (pixels).
        // 8 px is noticeably easier to hit than the OS default ~4 px.
        public const int ResizePx = 8;

        // Win32 constants
        private const int GWL_STYLE    = -16;
        private const int WS_THICKFRAME = 0x00040000;

        private const int WM_NCHITTEST  = 0x0084;
        private const int HTCLIENT      = 1;
        private const int HTLEFT        = 10;
        private const int HTRIGHT       = 11;
        private const int HTTOP         = 12;
        private const int HTTOPLEFT     = 13;
        private const int HTTOPRIGHT    = 14;
        private const int HTBOTTOM      = 15;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Call once from <c>OnSourceInitialized</c> of any transparent overlay window
        /// to restore native resize behaviour.
        /// </summary>
        public static void Enable(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            // Re-add the thick frame the OS needs to drive resize logic.
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style | WS_THICKFRAME);

            // Intercept hit-testing so edge/corner drags are handed to the OS.
            // Local function required — lambda with 'ref' parameters is C# 14+.
            IntPtr Hook(IntPtr h, int msg, IntPtr wP, IntPtr lP, ref bool handled)
                => HitTestHook(window, msg, lP, ref handled);
            HwndSource.FromHwnd(hwnd)?.AddHook(Hook);
        }

        private static IntPtr HitTestHook(Window w, int msg, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST) return IntPtr.Zero;

            // lParam encodes screen coordinates as two packed 16-bit shorts.
            int sx = unchecked((short)(lParam.ToInt32() & 0xFFFF));
            int sy = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));

            // Convert to window-local coordinates.
            var pt = w.PointFromScreen(new Point(sx, sy));
            double width  = w.ActualWidth;
            double height = w.ActualHeight;
            int e = ResizePx;

            bool left   = pt.X <= e;
            bool right  = pt.X >= width  - e;
            bool top    = pt.Y <= e;
            bool bottom = pt.Y >= height - e;

            if (top    && left)  { handled = true; return (IntPtr)HTTOPLEFT; }
            if (top    && right) { handled = true; return (IntPtr)HTTOPRIGHT; }
            if (bottom && left)  { handled = true; return (IntPtr)HTBOTTOMLEFT; }
            if (bottom && right) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (left)            { handled = true; return (IntPtr)HTLEFT; }
            if (right)           { handled = true; return (IntPtr)HTRIGHT; }
            if (top)             { handled = true; return (IntPtr)HTTOP; }
            if (bottom)          { handled = true; return (IntPtr)HTBOTTOM; }

            return IntPtr.Zero;
        }
    }
}

// filename: Services/ClickThrough.cs
// Win32 helpers to make a WPF window transparent to mouse clicks
// so the player can interact with EVE Online underneath the overlay.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OverlayMVP.Services
{
    public static class ClickThrough
    {
        // Extended window style constants
        private const int GWL_EXSTYLE   = -20;
        private const int WS_EX_LAYERED    = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Call once after the window is loaded to get the native HWND.
        /// </summary>
        public static IntPtr GetHwnd(Window window)
        {
            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle();
            return helper.Handle;
        }

        /// <summary>
        /// Enable click-through: mouse events pass straight to the window below.
        /// </summary>
        public static void Enable(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// Disable click-through: the overlay captures mouse input again
        /// (needed when the user wants to press a button on the overlay).
        /// </summary>
        public static void Disable(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }

        /// <summary>Toggle between click-through modes.</summary>
        public static bool Toggle(IntPtr hwnd)
        {
            int style      = GetWindowLong(hwnd, GWL_EXSTYLE);
            bool isThrough = (style & WS_EX_TRANSPARENT) != 0;

            if (isThrough)
                Disable(hwnd);
            else
                Enable(hwnd);

            return !isThrough; // returns new isInteractive state
        }
    }
}

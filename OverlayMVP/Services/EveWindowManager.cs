// filename: Services/EveWindowManager.cs
// Detects all running EVE Online client windows, allows switching between
// them, and can anchor the overlay to the active EVE window's position.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OverlayMVP.Models;

namespace OverlayMVP.Services
{
    public static class EveWindowManager
    {
        // ----------------------------------------------------------------
        // Win32 imports
        // ----------------------------------------------------------------

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_RESTORE   = 9;
        private const int SW_SHOW      = 5;

        // SetWindowPos flags
        private const uint SWP_NOSIZE       = 0x0001;
        private const uint SWP_NOZORDER     = 0x0004;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_SHOWWINDOW   = 0x0040;

        // AnchorMode offsets
        public const int ANCHOR_OFFSET_X = 8;   // pixels from EVE window right edge

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width  => Right - Left;
            public int Height => Bottom - Top;
        }

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Returns all visible EVE Online client windows currently open.
        /// EVE window titles follow the pattern "EVE - CharacterName"
        /// or just "EVE" for the launcher/loading screen.
        /// </summary>
        public static List<EveWindow> GetEveWindows()
        {
            var result          = new List<EveWindow>();
            var foregroundHwnd  = GetForegroundWindow();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                var title = sb.ToString();

                if (!IsEveWindow(title)) return true;

                var charName = ExtractCharacterName(title);
                result.Add(new EveWindow
                {
                    Handle        = hWnd,
                    Title         = title,
                    CharacterName = charName,
                    IsActive      = hWnd == foregroundHwnd,
                });

                return true;
            }, IntPtr.Zero);

            return result;
        }

        /// <summary>
        /// Brings the specified EVE window to the foreground.
        /// </summary>
        public static void FocusWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }

        /// <summary>
        /// Moves the overlay window to anchor to the right edge of the
        /// target EVE window.  Call this whenever the overlay or EVE
        /// window moves/resizes.
        /// </summary>
        /// <param name="overlayHwnd">HWND of the overlay WPF window.</param>
        /// <param name="eveHwnd">HWND of the target EVE window.</param>
        /// <param name="overlayWidth">Overlay window width in pixels.</param>
        /// <param name="overlayHeight">Overlay window height in pixels.</param>
        public static void AnchorOverlayToEveWindow(
            IntPtr overlayHwnd,
            IntPtr eveHwnd,
            int    overlayWidth,
            int    overlayHeight)
        {
            if (!GetWindowRect(eveHwnd, out var eveRect)) return;

            // Position overlay at the top-right corner of the EVE window,
            // just outside its right edge.
            int x = eveRect.Right + ANCHOR_OFFSET_X;
            int y = eveRect.Top;

            // If it would go off-screen to the right, place it inside-right instead.
            // (GetSystemMetrics(0) = screen width — use a safe fallback of 3840)
            int screenW = GetSystemMetrics(0);
            if (x + overlayWidth > screenW)
                x = eveRect.Right - overlayWidth - ANCHOR_OFFSET_X;

            SetWindowPos(
                overlayHwnd,
                IntPtr.Zero,
                x, y, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Returns the RECT of the given EVE window, or null if not found.
        /// </summary>
        public static RECT? GetEveWindowRect(IntPtr eveHwnd)
        {
            if (GetWindowRect(eveHwnd, out var r)) return r;
            return null;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private static bool IsEveWindow(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            // EVE client: "EVE - CharacterName" or "EVE" (launcher)
            // EVE launcher: "EVE Online"
            return title == "EVE"
                || title.StartsWith("EVE - ", StringComparison.OrdinalIgnoreCase)
                || title.Equals("EVE Online", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractCharacterName(string title)
        {
            // "EVE - CharacterName" → "CharacterName"
            const string prefix = "EVE - ";
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return title[prefix.Length..].Trim();

            return "";   // launcher / loading screen — no character yet
        }
    }
}

// filename: Services/MultiboxManager.cs
//
// Detects running EVE Online instances, renders live DWM thumbnails
// of each window inside the overlay, and switches focus on click.
//
// DWM thumbnail approach:
//   DwmRegisterThumbnail(destHwnd, srcHwnd) → registered thumbnail
//   DwmUpdateThumbnailProperties(thumb, ref props) → sets destination rect
//   The dest rect is in overlay-window client coordinates.
//
// Usage:
//   1. Call SetDestinationWindow(overlayHwnd) once after Loaded.
//   2. Call RefreshInstances() periodically (or on demand).
//   3. For each EveInstance, call UpdateThumbnailRect(inst, rect) whenever
//      the panel layout changes (SizeChanged / panel rendered).
//   4. Call SwitchTo(inst) on click.
//   5. Dispose() on window close.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace OverlayMVP.Services
{
    // ── Data model ────────────────────────────────────────────────────────
    public sealed class EveInstance
    {
        public IntPtr  Hwnd        { get; init; }
        public string  Title       { get; set;  } = "";
        public IntPtr  ThumbnailId { get; set;  } = IntPtr.Zero;

        // Friendly label shown under the thumbnail
        public string Label => Title.Length > 20 ? Title[..20] + "…" : Title;
    }

    // ── Manager ───────────────────────────────────────────────────────────
    public sealed class MultiboxManager : IDisposable
    {
        // Live list — bind ObservableCollection to the UI
        public ObservableCollection<EveInstance> Instances { get; } = new();

        private IntPtr _destHwnd = IntPtr.Zero;
        private bool   _disposed;

        // ── Init ──────────────────────────────────────────────────────────
        public void SetDestinationWindow(IntPtr overlayHwnd)
        {
            _destHwnd = overlayHwnd;
        }

        // ── Discovery ─────────────────────────────────────────────────────
        /// <summary>
        /// Scans for EVE Online windows and updates the Instances collection.
        /// Call this on a timer (e.g. every 5 seconds).
        /// </summary>
        public void RefreshInstances()
        {
            if (_destHwnd == IntPtr.Zero) return;

            var found = new List<IntPtr>();

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;

                var title = GetWindowTitle(hwnd);
                var cls   = GetWindowClass(hwnd);

                // FIX: Previous filter matched ANY window starting with "eve" (e.g. Event Viewer)
                // causing hundreds of spurious instances → DWM registrations → SO.
                // Now match only known EVE window signatures.
                bool isEveWindow = cls == "triuiScreen"
                    || title.Equals("EVE Online", StringComparison.OrdinalIgnoreCase)
                    || title.Equals("EVE", StringComparison.OrdinalIgnoreCase)
                    || title.StartsWith("EVE - ", StringComparison.OrdinalIgnoreCase);
                if (isEveWindow)
                {
                    found.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);

            // Remove stale instances
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                if (!found.Contains(Instances[i].Hwnd))
                {
                    UnregisterThumbnail(Instances[i]);
                    Instances.RemoveAt(i);
                }
            }

            // Add new instances
            foreach (var hwnd in found)
            {
                if (hwnd == _destHwnd) continue; // don't include the overlay itself

                var existing = FindByHwnd(hwnd);
                if (existing is null)
                {
                    var inst = new EveInstance
                    {
                        Hwnd  = hwnd,
                        Title = GetWindowTitle(hwnd),
                    };
                    RegisterThumbnail(inst);
                    Instances.Add(inst);
                }
                else
                {
                    // Refresh title (character may have changed)
                    existing.Title = GetWindowTitle(hwnd);
                }
            }
        }

        // ── Thumbnail registration ─────────────────────────────────────────
        private void RegisterThumbnail(EveInstance inst)
        {
            if (_destHwnd == IntPtr.Zero) return;
            int hr = NativeMethods.DwmRegisterThumbnail(_destHwnd, inst.Hwnd, out IntPtr thumb);
            if (hr == 0)
                inst.ThumbnailId = thumb;
        }

        private static void UnregisterThumbnail(EveInstance inst)
        {
            if (inst.ThumbnailId != IntPtr.Zero)
            {
                NativeMethods.DwmUnregisterThumbnail(inst.ThumbnailId);
                inst.ThumbnailId = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Call this whenever the panel element for this instance is rendered
        /// or resized to keep the DWM thumbnail in sync with the WPF layout.
        /// Pass the element's bounding rect in overlay-window client coordinates.
        /// </summary>
        public void UpdateThumbnailRect(EveInstance inst, Int32Rect destRect)
        {
            if (inst.ThumbnailId == IntPtr.Zero) return;
            var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags       = NativeMethods.DWM_TNP_VISIBLE |
                                NativeMethods.DWM_TNP_RECTDESTINATION |
                                NativeMethods.DWM_TNP_OPACITY,
                fVisible      = true,
                opacity       = 255,
                rcDestination = new NativeMethods.RECT(destRect),
            };
            NativeMethods.DwmUpdateThumbnailProperties(inst.ThumbnailId, ref props);
        }

        /// <summary>Hides a thumbnail without unregistering it.</summary>
        public void HideThumbnail(EveInstance inst)
        {
            if (inst.ThumbnailId == IntPtr.Zero) return;
            var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags  = NativeMethods.DWM_TNP_VISIBLE,
                fVisible = false,
            };
            NativeMethods.DwmUpdateThumbnailProperties(inst.ThumbnailId, ref props);
        }

        // ── Window switching ──────────────────────────────────────────────
        /// <summary>Brings an EVE window to the foreground.</summary>
        public static void SwitchTo(EveInstance inst)
        {
            if (inst.Hwnd == IntPtr.Zero) return;

            // Restore if minimised
            if (NativeMethods.IsIconic(inst.Hwnd))
                NativeMethods.ShowWindow(inst.Hwnd, NativeMethods.SW_RESTORE);

            NativeMethods.SetForegroundWindow(inst.Hwnd);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private EveInstance? FindByHwnd(IntPtr hwnd)
        {
            foreach (var inst in Instances)
                if (inst.Hwnd == hwnd) return inst;
            return null;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            int len = NativeMethods.GetWindowTextLength(hwnd);
            if (len == 0) return "";
            var buf = new System.Text.StringBuilder(len + 1);
            NativeMethods.GetWindowText(hwnd, buf, buf.Capacity);
            return buf.ToString();
        }

        private static string GetWindowClass(IntPtr hwnd)
        {
            var buf = new System.Text.StringBuilder(256);
            NativeMethods.GetClassName(hwnd, buf, buf.Capacity);
            return buf.ToString();
        }

        // ── Dispose ───────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var inst in Instances)
                UnregisterThumbnail(inst);
            Instances.Clear();
        }
    }

    // ── Win32 P/Invoke ────────────────────────────────────────────────────
    internal static class NativeMethods
    {
        public const int SW_RESTORE = 9;

        // DWM flags
        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_OPACITY         = 0x00000004;
        public const int DWM_TNP_VISIBLE         = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
            public RECT(Int32Rect r) { left = r.X; top = r.Y; right = r.X + r.Width; bottom = r.Y + r.Height; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int  dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
        }

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int  GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr phThumbnailId);
        [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

        [DllImport("user32.dll")] public static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);
    }
}

// filename: Views/ThumbnailWindow.xaml.cs
// Floating always-on-top window that hosts a single DWM thumbnail.
// The thumbnail is registered to this window's HWND so the live
// EVE frame is rendered directly inside ThumbnailHost.

using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class ThumbnailWindow : Window
    {
        public EveInstance Instance    { get; }
        public event Action<EveInstance>? ReAttachRequested;

        private IntPtr  _hwnd;
        private IntPtr  _thumbnailId = IntPtr.Zero;
        private bool    _dwmReady    = false;

        public ThumbnailWindow(EveInstance instance)
        {
            InitializeComponent();
            Instance        = instance;
            TitleLabel.Text = instance.Title.Length > 0 ? instance.Title : "EVE Instance";

            Loaded  += OnLoaded;
            Closed  += OnClosed;

            // Update thumbnail rect whenever the window is resized or moved
            SizeChanged     += (_, _) => UpdateThumbnail();
            LocationChanged += (_, _) => UpdateThumbnail();
        }

        // ── Startup ───────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            RegisterThumbnail();
        }

        private void RegisterThumbnail()
        {
            if (_hwnd == IntPtr.Zero || Instance.Hwnd == IntPtr.Zero) return;

            // Unregister any previous thumbnail
            if (_thumbnailId != IntPtr.Zero)
            {
                NativeMethods.DwmUnregisterThumbnail(_thumbnailId);
                _thumbnailId = IntPtr.Zero;
            }

            int hr = NativeMethods.DwmRegisterThumbnail(_hwnd, Instance.Hwnd, out _thumbnailId);
            _dwmReady = (hr == 0);
            if (_dwmReady) UpdateThumbnail();
        }

        public void UpdateThumbnail()
        {
            if (!_dwmReady || _thumbnailId == IntPtr.Zero) return;
            if (!ThumbnailHost.IsLoaded)                   return;

            // Get the ThumbnailHost position in screen pixels
            var topLeft     = ThumbnailHost.PointToScreen(new Point(0, 0));
            var bottomRight = ThumbnailHost.PointToScreen(
                                  new Point(ThumbnailHost.ActualWidth, ThumbnailHost.ActualHeight));

            // Convert to client coords relative to this window
            var winTL = PointFromScreen(topLeft);
            var winBR = PointFromScreen(bottomRight);

            var dpi  = VisualTreeHelper.GetDpi(this);
            var rect = new Int32Rect(
                (int)(winTL.X * dpi.DpiScaleX),
                (int)(winTL.Y * dpi.DpiScaleY),
                (int)((winBR.X - winTL.X) * dpi.DpiScaleX),
                (int)((winBR.Y - winTL.Y) * dpi.DpiScaleY));

            if (rect.Width <= 0 || rect.Height <= 0) return;

            var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags       = NativeMethods.DWM_TNP_VISIBLE |
                                NativeMethods.DWM_TNP_RECTDESTINATION |
                                NativeMethods.DWM_TNP_OPACITY,
                fVisible      = true,
                opacity       = 255,
                rcDestination = new NativeMethods.RECT(rect),
            };
            NativeMethods.DwmUpdateThumbnailProperties(_thumbnailId, ref props);
        }

        // ── Shutdown ──────────────────────────────────────────────────────
        private void OnClosed(object? sender, EventArgs e)
        {
            if (_thumbnailId != IntPtr.Zero)
            {
                NativeMethods.DwmUnregisterThumbnail(_thumbnailId);
                _thumbnailId = IntPtr.Zero;
            }
        }

        // ── Title bar drag ────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        // ── Re-attach ─────────────────────────────────────────────────────
        private void ReAttach_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ReAttachRequested?.Invoke(Instance);
            Close();
        }

        private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ReAttachRequested?.Invoke(Instance); // re-attach on close too
            Close();
        }

        // ── Resize grip ───────────────────────────────────────────────────
        private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newW = Math.Max(MinWidth,  Width  + e.HorizontalChange);
            double newH = Math.Max(MinHeight, Height + e.VerticalChange);
            Width  = newW;
            Height = newH;
        }
    }
}

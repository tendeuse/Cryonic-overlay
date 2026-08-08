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

            // WS_EX_NOACTIVATE — clicking this window never steals focus from EVE.
            // WS_EX_TOOLWINDOW  — hides from alt-tab so it doesn't clutter switching.
            const int GWL_EXSTYLE      = -20;
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            int style = NativeMethods.GetWindowLong(_hwnd, GWL_EXSTYLE);
            NativeMethods.SetWindowLong(_hwnd, GWL_EXSTYLE,
                style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // WS_THICKFRAME — WITHOUT THIS THE WINDOW CANNOT BE RESIZED AT ALL.
            //
            // Returning HTRIGHT/HTBOTTOM from WM_NCHITTEST only means something
            // to a window that has a sizing border. This window is
            // ResizeMode="NoResize", so it had none, so the hit-test below was
            // answering a question nobody acted on -- while still taking those
            // pixels away from WPF input. That is how widening the band to 14px
            // killed resizing outright: the 12x12 grip thumb sits in the
            // bottom-right corner and vanished inside the band.
            //
            // MainWindow and HelpWindow already do exactly this, with the same
            // WindowStyle=None + AllowsTransparency=True combination.
            const int GWL_STYLE     = -16;
            const int WS_THICKFRAME = 0x00040000;
            NativeMethods.SetWindowLong(_hwnd, GWL_STYLE,
                NativeMethods.GetWindowLong(_hwnd, GWL_STYLE) | WS_THICKFRAME);

            System.Windows.Interop.HwndSource.FromHwnd(_hwnd)
                ?.AddHook(WndProc);
            RegisterThumbnail();
        }

        // WndProc: resize hit-test + definitive focus-steal prevention
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 3;   // click but don't activate

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                               ref bool handled)
        {
            // Prevent ANY click on this window from stealing focus from EVE
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return (IntPtr)MA_NOACTIVATE;
            }

            if (msg == WM_NCHITTEST)
            {
                int screenX = unchecked((short)(lParam.ToInt32() & 0xFFFF));
                int screenY = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));
                var pos = PointFromScreen(new Point(screenX, screenY));
                bool atR = pos.X >= ActualWidth  - ResizeBorder;
                bool atB = pos.Y >= ActualHeight - ResizeBorder;
                if (atR && atB) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
                if (atR)        { handled = true; return (IntPtr)HTRIGHT; }
                if (atB)        { handled = true; return (IntPtr)HTBOTTOM; }
            }
            return IntPtr.Zero;
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

        // ── Title bar drag — pure WPF capture (never activates window) ─────
        private Point _dragOffset;

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
            e.Handled = true;
            _dragOffset = e.GetPosition(this);
            ((System.Windows.IInputElement)sender).CaptureMouse();
        }

        private void TitleBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            if (!((System.Windows.IInputElement)sender).IsMouseCaptured) return;
            var screen = PointToScreen(e.GetPosition(this));
            Left = screen.X - _dragOffset.X;
            Top  = screen.Y - _dragOffset.Y;
        }

        private void TitleBar_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ((System.Windows.IInputElement)sender).ReleaseMouseCapture();
        }

        // Constants for resize
        private const int WM_NCHITTEST  = 0x0084;
        private const int HTRIGHT       = 11;
        private const int HTBOTTOM      = 15;
        private const int HTBOTTOMRIGHT = 17;
        // 14px, not 8. These windows are small (280x185 by default) and get
        // dragged around a busy screen mid-fight; an 8px band is a pixel hunt.
        // The cost is that the outer 14px of the right and bottom edges resize
        // rather than pass a click through to the thumbnail, which is a good
        // trade for a preview you rarely click at its very edge.
        private const int ResizeBorder  = 14;

        // ── Click thumbnail → switch EVE focus (without activating this window) ──
        // WM_MOUSEACTIVATE returns MA_NOACTIVATE so we never steal focus.
        // We call SwitchTo() explicitly so the correct EVE client gets focus.
        private void ThumbnailHost_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            MultiboxManager.SwitchTo(Instance);
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

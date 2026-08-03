// filename: Views/HelpWindow.xaml.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OverlayMVP.Views
{
    /// <summary>
    /// In-app help: hotkeys, what each panel does, and how to get started.
    ///
    /// Takes MainWindow's view-model as its DataContext rather than building
    /// one, purely so `Loc` resolves — the help text follows the language
    /// toggle live, like every other string in the app.
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += EnableEdgeResize;
        }

        // ── Resizing ──────────────────────────────────────────────────────
        //
        // WindowStyle=None removes the frame Windows normally resizes by, so a
        // borderless window is stuck at its start size however long its content
        // is. Adding WS_THICKFRAME back and answering WM_NCHITTEST ourselves
        // restores dragging the right and bottom edges. Same approach
        // MainWindow uses; help text is exactly the kind of content whose
        // height depends on the user's font size, so a fixed window clips it.

        private const int GWL_STYLE       = -16;
        private const int WS_THICKFRAME   = 0x00040000;
        private const int WM_NCHITTEST    = 0x0084;
        private const int HTRIGHT         = 11;
        private const int HTBOTTOM        = 15;
        private const int HTBOTTOMRIGHT   = 17;
        private const int ResizeBorder    = 8;   // px of grabbable edge

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void EnableEdgeResize(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | WS_THICKFRAME);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST) return IntPtr.Zero;

            int sx = unchecked((short)(lParam.ToInt32() & 0xFFFF));
            int sy = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));
            var p  = PointFromScreen(new Point(sx, sy));

            bool atR = p.X >= ActualWidth  - ResizeBorder;
            bool atB = p.Y >= ActualHeight - ResizeBorder;

            if (atR && atB) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (atR)        { handled = true; return (IntPtr)HTRIGHT; }
            if (atB)        { handled = true; return (IntPtr)HTBOTTOM; }
            return IntPtr.Zero;
        }

        // The window is WindowStyle=None, so dragging is ours to implement.
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}

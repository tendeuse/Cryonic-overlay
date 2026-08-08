// filename: MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OverlayMVP.Services;
using OverlayMVP.ViewModels;
using OverlayMVP.Views;

namespace OverlayMVP
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel  _vm;
        private HotkeyManager?          _hotkeys;
        private MultiboxManager         _multibox = new();
        private System.Windows.Threading.DispatcherTimer? _multiboxTimer;
        private IntPtr                  _hwnd;
        private bool                    _visible = true;
        private readonly Dictionary<IntPtr, ThumbnailWindow> _detached = new();
        private readonly Dictionary<string, Window> _panels = new();

        public MainWindow(AppDb db)
        {
            InitializeComponent();
            var cfg = OverlayConfig.Load(db)
                      ?? throw new InvalidOperationException("Config missing — run first-run wizard.");
            _vm         = new MainViewModel(db, cfg);
            DataContext = _vm;
            Loaded  += OnLoaded;
            Closed  += OnClosed;
            SizeChanged += (_, _) => UpdateAllThumbnailRects();
        }

        // ── Startup ───────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = ClickThrough.GetHwnd(this);
            // Hook WndProc so Windows knows which edges are resizable
            System.Windows.Interop.HwndSource.FromHwnd(_hwnd)
                ?.AddHook(WndProc);
            // NoResize removes WS_THICKFRAME — add it back so Windows
            // actually processes our WM_NCHITTEST HTRIGHT/HTBOTTOM responses.
            const int GWL_STYLE     = -16;
            const int WS_THICKFRAME = 0x00040000;
            int style = GetWindowLong(_hwnd, GWL_STYLE);
            SetWindowLong(_hwnd, GWL_STYLE, style | WS_THICKFRAME);
            _hotkeys = new HotkeyManager(this);
            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_VISIBILITY,   ToggleVisibility);
            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_CLICKTHROUGH, ToggleClickThrough);
            _hotkeys.SetHandler(HotkeyManager.ID_REPORT_INTEL,  () => _vm.ReportRoamingCommand.Execute(null));
            _hotkeys.SetHandler(HotkeyManager.ID_HIDE_ALL,      ToggleAll);

            _multibox.SetDestinationWindow(_hwnd);

            // The multibox panel renders one card per running EVE client, so a
            // capture taken while the game is open differs from one taken while
            // it is closed. That makes the visual baseline depend on whether the
            // developer happens to be playing, which is not a property a safety
            // net can have. Skip the scan entirely when capturing.
            if (!App.IsScreenshotMode)
            {
                _multibox.RefreshInstances();
                RefreshMultiboxPanel();
            }

            _multiboxTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _multiboxTimer.Tick += (_, _) =>
            {
                _multibox.RefreshInstances();
                RefreshMultiboxPanel();
                // Sync pilot list into ViewModel's ObservableCollection<EveWindow>
                var fg = NativeMethods.GetForegroundWindow();
                _vm.EveWindows.Clear();
                foreach (var inst in _multibox.Instances)
                    _vm.EveWindows.Add(new OverlayMVP.Models.EveWindow
                    {
                        Handle        = inst.Hwnd,
                        Title         = inst.Title,
                        CharacterName = inst.Title.StartsWith("EVE - ", StringComparison.OrdinalIgnoreCase)
                                            ? inst.Title[6..] : inst.Title,
                        IsActive      = inst.Hwnd == fg,
                    });
                foreach (var kv in _detached)
                    kv.Value.UpdateThumbnail();

                // Session tracker: count wall-clock time while any EVE client is running.
                if (_multibox.Instances.Count > 0) _vm.Session.TickPlaytime(5);
            };
            // Not started when capturing: the app exits before 5s elapses today,
            // but relying on that timing would make the baseline fragile.
            if (!App.IsScreenshotMode) _multiboxTimer.Start();
            // Screenshot mode (--screenshot) must capture a deterministic baseline:
            // the poll loop hits ESI and the network on a 10s cadence, so leaving
            // it running would race the capture and make every diff noise.
            if (!App.IsScreenshotMode) _vm.StartPolling();
            _vm.SystemChangedNotify = NotifySystemChanged;
        }

        // ── Shutdown ──────────────────────────────────────────────────────
        private void OnClosed(object? sender, EventArgs e)
        {
            _multiboxTimer?.Stop();
            foreach (var kv in _detached) kv.Value.Close();
            _detached.Clear();
            _multibox.Dispose();
            _hotkeys?.Dispose();
            _vm.Dispose();
            Application.Current.Shutdown();
        }

        // ── Title bar ─────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        // ── Monetisation (EULA §4.4) ──────────────────────────────────────
        // Sponsor banner: opens the ad's target. Shown to all users equally.
        private void SponsorBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenUrl(_vm.SponsorUrl);

        // Support button: opens GitHub Sponsors. A voluntary donation, decoupled from
        // features and from the banner (never gate anything on this).
        private void SupportBtn_Click(object sender, RoutedEventArgs e)
            => OpenUrl(_vm.SupportUrl);

        // Update notice: opens the server-provided download URL for the newer version.
        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
            => OpenUrl(_vm.UpdateUrl);

        // Control panel: super-users only (see MainViewModel.IsSuperUser).
        private void ControlPanelBtn_Click(object sender, RoutedEventArgs e)
            => OpenUrl("https://cryonic-panel.pages.dev/");

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            // Only allow http/https so a bad config value can't launch something unexpected.
            if (!url.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = url,
                    UseShellExecute = true,   // hand off to the default browser
                });
            }
            catch { /* browser launch failed — ignore */ }
        }

        private Views.IntelWindow?  _intelWindow;
        private Views.SystemWindow? _systemWindow;

        private void IntelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_intelWindow is not null && _intelWindow.IsVisible)
            {
                _intelWindow.Close();
                _intelWindow = null;
                return;
            }
            _intelWindow = new Views.IntelWindow();
            _intelWindow.Left  = Left + ActualWidth + 8;
            _intelWindow.Top   = Top;
            _intelWindow.Closed += (_, _) => _intelWindow = null;
            _intelWindow.Show();
        }

        private void SystemBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_systemWindow is not null && _systemWindow.IsVisible)
            {
                _systemWindow.Close();
                _systemWindow = null;
                return;
            }
            _systemWindow = new Views.SystemWindow();
            _systemWindow.Left  = Left + ActualWidth + 8;
            _systemWindow.Top   = Top;
            _systemWindow.Closed += (_, _) => _systemWindow = null;
            _systemWindow.Show();

            // Load the current system immediately if known
            if (_vm.SolarSystem is { Length: > 0 } sysName)
                _ = _systemWindow.LoadSystemByNameAsync(sysName);
        }

        // Called by MainWindow when ESI detects a system change
        public void NotifySystemChanged(string systemName)
        {
            if (_systemWindow is { IsVisible: true } && !string.IsNullOrEmpty(systemName))
                _ = _systemWindow.LoadSystemByNameAsync(systemName);
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            // Passes the view-model so the help text follows the language
            // toggle, exactly like the rest of the UI.
            var win = new Views.HelpWindow(_vm) { Owner = this };
            win.ShowDialog();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new Views.SettingsWindow(_vm.Db, _vm.Config);
            win.Owner = this;
            win.ShowDialog();
            _vm.ApplyFeatureFlags();  // Apply any feature toggles that were changed
        }

        // ── Hotkeys ───────────────────────────────────────────────────────
        /// <summary>
        /// Ctrl+Shift+O — hide the PANEL, keep the instance previews.
        ///
        /// LEAVING THE PREVIEWS UP IS THE POINT, not a limitation. Opacity does
        /// not affect them because DWM composites them onto this HWND outside
        /// WPF's visual tree, and that happens to give exactly the behaviour
        /// wanted here: the overlay's own chrome gets out of the way while you
        /// can still watch your other clients.
        ///
        /// Do not "fix" this to hide the thumbnails. Ctrl+Shift+H already
        /// exists for hiding everything; see ToggleAll.
        /// </summary>
        private void ToggleVisibility()
        {
            _visible = !_visible;
            Dispatcher.Invoke(() => Opacity = _visible ? 1.0 : 0.0);
        }

        private bool _allVisible = true;

        /// <summary>
        /// Hide or show EVERYTHING — the overlay and every detached instance
        /// window.
        ///
        /// The detached windows are HIDDEN, not faded. Their thumbnails are
        /// composited by DWM, outside WPF's visual tree, so Opacity = 0 leaves
        /// the live preview of the game still painted on screen. Only removing
        /// the window actually removes the thumbnail.
        ///
        /// The main window keeps using Opacity, matching Ctrl+Shift+O: hiding
        /// it outright would drop its always-on-top position and it would come
        /// back behind the game.
        /// </summary>
        private void ToggleAll()
        {
            _allVisible = !_allVisible;
            Dispatcher.Invoke(() =>
            {
                Opacity  = _allVisible ? 1.0 : 0.0;
                _visible = _allVisible;   // keep Ctrl+Shift+O in step

                foreach (var kv in _detached)
                {
                    if (_allVisible)
                    {
                        kv.Value.Show();
                        // Re-apply the DWM destination rect. Show() does not
                        // necessarily raise SizeChanged or LocationChanged, and
                        // those are the only things that normally refresh it --
                        // so without this a restored window can come back as an
                        // empty frame with no live preview in it.
                        kv.Value.UpdateThumbnail();
                    }
                    else kv.Value.Hide();
                }

                // The ATTACHED thumbnails need hiding too, and Opacity above
                // does not touch them: DWM composites them onto this window's
                // HWND, outside WPF's visual tree, so a fully transparent
                // window still has live EVE frames painted on it. That is the
                // whole reason this hotkey exists, and missing it here left
                // the Active Instances previews sitting on screen.
                //
                // UpdateAllThumbnailRects re-applies fVisible from _allVisible,
                // so the 5s multibox timer cannot quietly turn them back on.
                UpdateAllThumbnailRects();
            });
        }
        private void ToggleClickThrough()
        {
            bool nowInteractive = ClickThrough.Toggle(_hwnd);
            Dispatcher.Invoke(() => _vm.ToggleClickThrough(!nowInteractive));
        }

        // ── Multibox panel ────────────────────────────────────────────────
        private void RefreshMultiboxPanel()
        {
            if (FindName("MultiboxItemsControl") is ItemsControl ic)
                ic.ItemsSource = _multibox.Instances;
            UpdateAllThumbnailRects();
        }

        private bool _updatingThumbnails = false;
        private void UpdateAllThumbnailRects()
        {
            if (_updatingThumbnails) return;
            _updatingThumbnails = true;
            try { UpdateAllThumbnailRectsCore(); }
            finally { _updatingThumbnails = false; }
        }

        private void UpdateAllThumbnailRectsCore()
        {
            // Ctrl+Shift+H wins over the refresh timer. This method is the only
            // thing that sets fVisible = true, and it runs every 5 seconds, so
            // without this guard a hidden overlay would light its previews back
            // up on the next tick.
            if (!_allVisible)
            {
                foreach (var inst in _multibox.Instances) _multibox.HideThumbnail(inst);
                return;
            }

            if (FindName("MultiboxItemsControl") is not ItemsControl ic) return;
            for (int i = 0; i < _multibox.Instances.Count; i++)
            {
                var inst = _multibox.Instances[i];
                if (_detached.ContainsKey(inst.Hwnd)) continue; // skip detached

                if (ic.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                { _multibox.HideThumbnail(inst); continue; }

                var host = FindChildByName<Border>(container, "ThumbnailHost");
                if (host is null || !host.IsVisible)
                { _multibox.HideThumbnail(inst); continue; }

                // TranslatePoint(..., this) gives WPF DIPs relative to the window origin.
                // Multiply by DPI scale to get physical pixels for the DWM destination rect.
                // This is reliable with ResizeMode=NoResize (no hidden non-client border).
                var topLeft     = host.TranslatePoint(new Point(0, 0), this);
                var bottomRight = host.TranslatePoint(new Point(host.ActualWidth, host.ActualHeight), this);
                var dpi         = VisualTreeHelper.GetDpi(this);
                var rect        = new Int32Rect(
                    (int)(topLeft.X     * dpi.DpiScaleX),
                    (int)(topLeft.Y     * dpi.DpiScaleY),
                    (int)((bottomRight.X - topLeft.X) * dpi.DpiScaleX),
                    (int)((bottomRight.Y - topLeft.Y) * dpi.DpiScaleY));

                if (rect.Width > 0 && rect.Height > 0) _multibox.UpdateThumbnailRect(inst, rect);
                else _multibox.HideThumbnail(inst);
            }
        }

        // ── Card single-click → switch EVE focus ──────────────────────────
        // ── Card click: single = focus, double = detach ──────────────────
        private void MultiboxCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not EveInstance inst) return;
            if (e.ClickCount >= 2) { e.Handled = true; DetachInstance(inst, fe); }
            else MultiboxManager.SwitchTo(inst);
        }

        // (kept for potential future use from context menu etc.)
        private void DetachThumbnail_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement fe && fe.DataContext is EveInstance inst)
                DetachInstance(inst, fe);
        }

        private void DetachInstance(EveInstance inst, FrameworkElement anchor)
        {
            // If already detached, just bring it to front without activating
            if (_detached.TryGetValue(inst.Hwnd, out var existing))
            {
                // Raise the window z-order without stealing focus from EVE
                const int HWND_TOP = 0;
                const int SWP_NOACTIVATE = 0x0010;
                const int SWP_NOMOVE = 0x0002;
                const int SWP_NOSIZE = 0x0001;
                SetWindowPos(
                    new System.Windows.Interop.WindowInteropHelper(existing).Handle,
                    (IntPtr)HWND_TOP, 0, 0, 0, 0,
                    SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
                return;
            }

            _multibox.HideThumbnail(inst);

            var floater = new ThumbnailWindow(inst);
            floater.ReAttachRequested += ReAttachThumbnail;

            var pos = anchor.PointToScreen(new Point(0, 0));
            floater.Left = pos.X;
            floater.Top  = pos.Y + 30;

            _detached[inst.Hwnd] = floater;
            floater.Closed += (_, _) => _detached.Remove(inst.Hwnd);

            // Respect an active Ctrl+Shift+H. "Everything is hidden" has to mean
            // everything, including windows created while it is in force -- the
            // point of the hotkey is a clean screen for a screenshot or a
            // stream, and one thumbnail appearing anyway defeats it. ToggleAll
            // shows every detached window when it unhides, so this one appears
            // then along with the rest.
            if (_allVisible) floater.Show();
        }

        private void ReAttachThumbnail(EveInstance inst)
        {
            _detached.Remove(inst.Hwnd);

            // Register the thumbnail again rather than merely un-hiding it.
            // Making the old registration visible leaves the card showing a
            // dead black rectangle: the preview is gone and never comes back,
            // not even on the next refresh tick. Re-registering is the only
            // thing that reliably brings the live frame back.
            _multibox.ReRegisterThumbnail(inst);

            Dispatcher.BeginInvoke(UpdateAllThumbnailRects,
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Panel launcher (5 floating content windows) ───────────────────
        private void PanelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            string key = btn.Tag as string ?? "";
            TogglePanel(key, btn);
        }

        private void TogglePanel(string key, System.Windows.Controls.Button btn)
        {
            if (_panels.TryGetValue(key, out var existing) && existing.IsLoaded)
            {
                existing.Close();
                _panels.Remove(key);
                btn.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#221E3148"));
                return;
            }

            Window win = key switch
            {
                "standing" => new Views.StandingGuideWindow(),
                "progress" => new Views.MissionProgressWindow(),
                "skills"   => new Views.SkillPlanWindow(),
                "orders"   => new Views.OrdersWindow(),
                _          => throw new ArgumentException($"Unknown panel key: {key}")
            };

            win.DataContext = _vm;
            // Position: cascade from main window
            win.Left = this.Left + this.ActualWidth + 8;
            win.Top  = this.Top;
            win.Show();
            if (key == "orders") _vm.MarkOrdersSeen();
            _panels[key] = win;

            // Highlight active button
            btn.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#443060A0"));

            // Remove from tracking when closed
            win.Closed += (_, _) =>
            {
                _panels.Remove(key);
                btn.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#221E3148"));
            };
        }

        // ── Resize — WndProc hit-test + grip handlers ─────────────────────
        // AllowsTransparency=True removes the native window chrome, so Windows
        // needs us to return correct HITTEST values from WM_NCHITTEST ourselves.
        private const int WM_NCHITTEST    = 0x0084;
        private const int WM_NCLBUTTONDOWN= 0x00A1;
        private const int HTCLIENT        = 1;
        private const int HTRIGHT         = 11;
        private const int HTBOTTOM        = 15;
        private const int HTBOTTOMRIGHT   = 17;
        private const int ResizeBorder    = 8;   // px — hit-test border thickness

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                               ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                // Screen coords from lParam
                int screenX = unchecked((short)(lParam.ToInt32() & 0xFFFF));
                int screenY = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));

                var pos   = PointFromScreen(new Point(screenX, screenY));
                bool atR  = pos.X >= ActualWidth  - ResizeBorder;
                bool atB  = pos.Y >= ActualHeight - ResizeBorder;

                if (atR && atB) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
                if (atR)        { handled = true; return (IntPtr)HTRIGHT; }
                if (atB)        { handled = true; return (IntPtr)HTBOTTOM; }
            }
            return IntPtr.Zero;
        }

        private void ResizeGrip_MouseDown(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
            e.Handled = true;
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTBOTTOMRIGHT, IntPtr.Zero);
        }

        private void ResizeRight_MouseDown(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
            e.Handled = true;
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTRIGHT, IntPtr.Zero);
        }

        private void ResizeBottom_MouseDown(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
            e.Handled = true;
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTBOTTOM, IntPtr.Zero);
        }

        // ── Tree helper (iterative BFS) ───────────────────────────────────
        private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(parent);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is T tfe && tfe.Name == name) return tfe;
                    queue.Enqueue(child);
                }
            }
            return null;
        }
    }
}

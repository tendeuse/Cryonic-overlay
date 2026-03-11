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
            _hotkeys = new HotkeyManager(this);
            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_VISIBILITY,   ToggleVisibility);
            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_CLICKTHROUGH, ToggleClickThrough);
            _hotkeys.SetHandler(HotkeyManager.ID_REPORT_INTEL,  () => _vm.ReportRoamingCommand.Execute(null));
            _hotkeys.SetHandler(HotkeyManager.ID_REPORT_CLEAR,  () => _vm.ReportClearCommand.Execute(null));

            _multibox.SetDestinationWindow(_hwnd);
            _multibox.RefreshInstances();
            RefreshMultiboxPanel();

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
            };
            _multiboxTimer.Start();
            _vm.StartPolling();
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

        // ── Hotkeys ───────────────────────────────────────────────────────
        private void ToggleVisibility()
        {
            _visible = !_visible;
            Dispatcher.Invoke(() => Opacity = _visible ? 1.0 : 0.0);
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
            if (_detached.TryGetValue(inst.Hwnd, out var existing)) { existing.Activate(); return; }

            _multibox.HideThumbnail(inst);

            var floater = new ThumbnailWindow(inst);
            floater.ReAttachRequested += ReAttachThumbnail;

            var pos = anchor.PointToScreen(new Point(0, 0));
            floater.Left = pos.X;
            floater.Top  = pos.Y + 30;

            _detached[inst.Hwnd] = floater;
            floater.Closed += (_, _) => _detached.Remove(inst.Hwnd);
            floater.Show();
        }

        private void ReAttachThumbnail(EveInstance inst)
        {
            _detached.Remove(inst.Hwnd);
            Dispatcher.BeginInvoke(UpdateAllThumbnailRects,
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Catalogue panel expand/collapse ──────────────────────────────
        private bool _catalogueExpanded = false;
        private void CatalogueHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _catalogueExpanded = !_catalogueExpanded;
            if (FindName("CatalogueBody") is System.Windows.Controls.StackPanel body)
                body.Visibility = _catalogueExpanded
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            if (FindName("CatalogueChevron") is System.Windows.Controls.TextBlock chevron)
                chevron.Text = _catalogueExpanded ? "▴" : "▾";
        }

        // ── Manual standing input ─────────────────────────────────────────
        private void ManualStandingSet_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (FindName("ManualStandingBox") is System.Windows.Controls.TextBox tb &&
                DataContext is OverlayMVP.ViewModels.MainViewModel vm)
            {
                vm.SetManualStandingCommand.Execute(tb.Text);
            }
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

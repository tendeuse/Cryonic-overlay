// filename: Views/SystemWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class SystemWindow : Window
    {
        /// <summary>Translations for {Binding Loc.X}. This window had no
        /// DataContext, so every string in it was a hardcoded literal.</summary>
        public LocalizationManager Loc => LocalizationManager.Instance;

        private readonly SystemInfoService _svc = new();
        private CancellationTokenSource   _cts  = new();
        private Point  _dragOffset;
        private int    _currentSystemId;
        private string _activeTab = "system";
        private List<SovSystem> _sovData = new();

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 3;
        private const int WM_NCHITTEST  = 0x0084;
        private const int HTLEFT        = 10;
        private const int HTRIGHT       = 11;
        private const int HTTOP         = 12;
        private const int HTTOPLEFT     = 13;
        private const int HTTOPRIGHT    = 14;
        private const int HTBOTTOM      = 15;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeEdge    = 6;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr h, int i);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr h, int i, int v);
        private const int GWL_STYLE     = -16;
        private const int WS_THICKFRAME = 0x00040000;

        private static double G  => App("GlobalFontSize",  11);
        private static double Sm => App("GlobalFontSizeSm", 9);
        private static double Xs => App("GlobalFontSizeXs", 8);
        private static double App(string key, double fallback)
            => Application.Current?.Resources[key] is double d ? d : fallback;

        public SystemWindow()
        {
            InitializeComponent();
            DataContext = this;
            SystemSearchBox.KeyDown += (s, e) => { if (e.Key == Key.Return) _ = DoSystemSearchAsync(); };
            RouteDestBox.KeyDown    += (s, e) => { if (e.Key == Key.Return) _ = DoRouteSearchAsync(); };
            SovFilterBox.KeyDown    += (s, e) => { if (e.Key == Key.Return) RenderSov(SovFilterBox.Text); };
            SetActiveTab("system");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style | WS_THICKFRAME);
            System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE) { handled = true; return (IntPtr)MA_NOACTIVATE; }
            if (msg == WM_NCHITTEST)
            {
                int sx = unchecked((short)(lParam.ToInt32() & 0xFFFF));
                int sy = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));
                var p  = PointFromScreen(new Point(sx, sy));
                bool l = p.X <= ResizeEdge;
                bool r = p.X >= ActualWidth  - ResizeEdge;
                bool t = p.Y <= ResizeEdge;
                bool b = p.Y >= ActualHeight - ResizeEdge;
                if (t && l) { handled = true; return (IntPtr)HTTOPLEFT; }
                if (t && r) { handled = true; return (IntPtr)HTTOPRIGHT; }
                if (b && l) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
                if (b && r) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
                if (l)      { handled = true; return (IntPtr)HTLEFT; }
                if (r)      { handled = true; return (IntPtr)HTRIGHT; }
                if (t)      { handled = true; return (IntPtr)HTTOP; }
                if (b)      { handled = true; return (IntPtr)HTBOTTOM; }
            }
            return IntPtr.Zero;
        }

        // ── Drag ─────────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            e.Handled = true; _dragOffset = e.GetPosition(this);
            ((IInputElement)s).CaptureMouse();
        }
        private void TitleBar_MouseMove(object s, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !((IInputElement)s).IsMouseCaptured) return;
            var sc = PointToScreen(e.GetPosition(this));
            Left = sc.X - _dragOffset.X; Top = sc.Y - _dragOffset.Y;
        }
        private void TitleBar_MouseUp(object s, MouseButtonEventArgs e)
            => ((IInputElement)s).ReleaseMouseCapture();

        // ── Tab switching ─────────────────────────────────────────────────
        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                SetActiveTab(tag);
        }

        private void SetActiveTab(string tab)
        {
            _activeTab = tab;

            // Reset all tab styles
            foreach (var b in new[] { TabSystem, TabRoute, TabSov })
            {
                b.BorderBrush  = new SolidColorBrush(Colors.Transparent);
                b.Foreground   = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xAA));
                b.FontWeight   = FontWeights.Normal;
            }

            // Highlight active
            var active = tab == "system" ? TabSystem : tab == "route" ? TabRoute : TabSov;
            active.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
            active.Foreground  = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
            active.FontWeight  = FontWeights.Bold;

            // Show correct input row
            SystemInput.Visibility = tab == "system" ? Visibility.Visible : Visibility.Collapsed;
            RouteInput.Visibility  = tab == "route"  ? Visibility.Visible : Visibility.Collapsed;
            SovInput.Visibility    = tab == "sov"    ? Visibility.Visible : Visibility.Collapsed;

            TitleLabel.Text = tab == "system" ? "🌐  SYSTEM INFO"
                            : tab == "route"  ? "🗺  ROUTE PLANNER"
                            :                   "🏴  SOVEREIGNTY MAP";

            // Restore sov data if already loaded
            if (tab == "sov" && _sovData.Count > 0)
                RenderSov(SovFilterBox.Text);
        }

        // ── Buttons ───────────────────────────────────────────────────────
        private void Close_Click(object s, RoutedEventArgs e) => Close();
        private void Refresh_Click(object s, RoutedEventArgs e)
        {
            _svc.ClearCache();
            _sovData.Clear();
            if (_activeTab == "system" && _currentSystemId > 0)
                _ = LoadSystemAsync(_currentSystemId);
        }
        private void SystemSearch_Click(object s, RoutedEventArgs e) => _ = DoSystemSearchAsync();
        private void RouteSearch_Click(object s, RoutedEventArgs e)  => _ = DoRouteSearchAsync();
        private void SovLoad_Click(object s, RoutedEventArgs e)      => _ = DoLoadSovAsync();
        private void SovFilter_TextChanged(object s, TextChangedEventArgs e)
        {
            if (_sovData.Count > 0) RenderSov(SovFilterBox.Text);
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel(); _svc.Dispose(); base.OnClosed(e);
        }

        // ── Public API (called from MainWindow on system change) ──────────
        public async Task LoadSystemAsync(int systemId)
        {
            if (systemId <= 0) return;
            _currentSystemId = systemId;
            SetActiveTab("system");
            _cts.Cancel(); _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            ShowLoading("Loading system…");
            try
            {
                var info = await _svc.GetSystemAsync(systemId, ct,
                    msg => Dispatcher.Invoke(() => ShowLoading(msg)));
                Dispatcher.Invoke(() => { ShowSystemResult(info); SystemSearchBox.Text = info.Name; });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Dispatcher.Invoke(() => ShowError(ex.Message)); }
        }

        public async Task LoadSystemByNameAsync(string name)
        {
            SystemSearchBox.Text = name;
            SetActiveTab("system");
            await DoSystemSearchAsync();
        }

        // ── Search handlers ───────────────────────────────────────────────
        private async Task DoSystemSearchAsync()
        {
            var name = SystemSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            _cts.Cancel(); _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            ShowLoading($"Searching {name}…");
            try
            {
                var info = await _svc.GetSystemByNameAsync(name, ct,
                    msg => Dispatcher.Invoke(() => ShowLoading(msg)));
                _currentSystemId = info.SystemId;
                Dispatcher.Invoke(() => ShowSystemResult(info));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Dispatcher.Invoke(() => ShowError(ex.Message)); }
        }

        private async Task DoRouteSearchAsync()
        {
            var origin = RouteOriginBox.Text.Trim();
            var dest   = RouteDestBox.Text.Trim();
            if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(dest)) return;

            string routeType = RouteShortest.IsChecked == true ? "shortest"
                             : RouteSecure.IsChecked   == true ? "secure"
                             :                                    "insecure";

            _cts.Cancel(); _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            ShowLoading("Calculating route…");
            try
            {
                var route = await _svc.GetRouteAsync(origin, dest, routeType, ct,
                    msg => Dispatcher.Invoke(() => ShowLoading(msg)));
                Dispatcher.Invoke(() => ShowRouteResult(route));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Dispatcher.Invoke(() => ShowError(ex.Message)); }
        }

        private async Task DoLoadSovAsync()
        {
            _cts.Cancel(); _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            ShowLoading("Loading sovereignty data (may take 30s)…");
            try
            {
                _sovData = await _svc.GetSovereigntyMapAsync(ct,
                    msg => Dispatcher.Invoke(() => ShowLoading(msg)));
                Dispatcher.Invoke(() => RenderSov(SovFilterBox.Text));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Dispatcher.Invoke(() => ShowError(ex.Message)); }
        }

        // ── Result renderers ──────────────────────────────────────────────
        private void ShowSystemResult(SystemInfo info)
        {
            var sp = new StackPanel();

            // Header
            var hg = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ns = new StackPanel();
            ns.Children.Add(T(info.Name,          "#FFCCD6F6", G + 3, bold: true));
            ns.Children.Add(T(info.Region,         "#FF8A99AA", Sm));
            ns.Children.Add(T(info.Constellation,  "#FF8A99AA", Xs));
            var sb = SecBadge(info.Security, info.SecColor);
            Grid.SetColumn(ns, 0); Grid.SetColumn(sb, 1);
            hg.Children.Add(ns); hg.Children.Add(sb);
            sp.Children.Add(hg);
            sp.Children.Add(Link($"🌐 View on dotlan.net",
                $"https://evemaps.dotlan.net/system/{Uri.EscapeDataString(info.Name)}"));
            sp.Children.Add(Divider());

            // Activity stats
            sp.Children.Add(SectionHead("ACTIVITY  (last hour)", "#FFFFD54F"));
            var ag = new Grid { Margin = new Thickness(0, 4, 0, 8) };
            for (int i = 0; i < 4; i++)
                ag.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ag.Children.Add(StatCell("⚔ Ships", info.ShipKills.ToString(), "#FFEF5350", 0));
            ag.Children.Add(StatCell("💀 Pods",  info.PodKills.ToString(),  "#FFEF5350", 1));
            ag.Children.Add(StatCell("🤖 NPC",   info.NpcKills.ToString(),  "#FF8A99AA", 2));
            ag.Children.Add(StatCell("🚀 Jumps", info.Jumps.ToString(),     "#FF4FC3F7", 3));
            sp.Children.Add(ag);
            sp.Children.Add(Divider());

            // Sovereignty
            if (!string.IsNullOrEmpty(info.SovHolder))
            {
                sp.Children.Add(SectionHead("SOVEREIGNTY", "#FF4FC3F7"));
                sp.Children.Add(T(info.SovHolder, "#FFFFD54F", Sm));
                sp.Children.Add(Divider());
            }

            // Adjacent systems
            sp.Children.Add(SectionHead($"ADJACENT SYSTEMS ({info.Adjacent.Count})", "#FF4FC3F7"));
            foreach (var adj in info.Adjacent)
            {
                var row = AdjRow(adj.Name, adj.Security, adj.SecColor, adj.Kills, adj.Jumps);
                row.Cursor = Cursors.Hand;
                var cId = adj.SystemId; var cName = adj.Name;
                row.MouseLeftButtonDown += async (s, e) =>
                {
                    e.Handled = true;
                    SystemSearchBox.Text = cName;
                    await LoadSystemAsync(cId);
                };
                HoverBorder(row);
                sp.Children.Add(row);
            }

            sp.Children.Add(Divider());
            sp.Children.Add(T($"Updated {info.FetchedAt:HH:mm} UTC  ·  Stats: last 1h", "#FF445566", Xs));
            ContentArea.Content = sp;
        }

        private void ShowRouteResult(RouteResult route)
        {
            var sp = new StackPanel();

            // Summary bar
            var summary = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xD5, 0x4F)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xD5, 0x4F)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 8),
            };
            var sumGrid = new Grid();
            sumGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sumGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sumLeft = new StackPanel();
            sumLeft.Children.Add(T($"{route.Origin}  →  {route.Destination}", "#FFFFD54F", Sm, bold: true));
            sumLeft.Children.Add(T($"{route.RouteType} route  ·  {route.TotalJumps} jumps", "#FF8A99AA", Xs));
            var sumRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            sumRight.Children.Add(T(route.TotalJumps.ToString(), "#FFFFD54F", G + 4, bold: true));
            sumRight.Children.Add(T("jumps", "#FF8A99AA", Xs));
            if (route.TotalKills > 0)
                sumRight.Children.Add(T($"⚔ {route.TotalKills}", "#FFEF5350", Xs));
            Grid.SetColumn(sumLeft, 0); Grid.SetColumn(sumRight, 1);
            sumGrid.Children.Add(sumLeft); sumGrid.Children.Add(sumRight);
            summary.Child = sumGrid;
            sp.Children.Add(summary);

            // Hop list
            sp.Children.Add(SectionHead("ROUTE", "#FFFFD54F"));
            int hopNum = 0;
            foreach (var hop in route.Hops)
            {
                var hopBorder = new Border
                {
                    Margin          = new Thickness(0, 1, 0, 1),
                    Padding         = new Thickness(6, 3, 6, 3),
                    CornerRadius    = new CornerRadius(2),
                    Background      = hop.IsOrigin || hop.IsDestination
                        ? new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xD5, 0x4F))
                        : new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
                    BorderBrush     = hop.IsOrigin || hop.IsDestination
                        ? new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xD5, 0x4F))
                        : new SolidColorBrush(Color.FromArgb(0x11, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Cursor          = Cursors.Hand,
                };

                var hg = new Grid();
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var numTb = new TextBlock {
                    Text = hopNum == 0 ? "START" : hopNum == route.Hops.Count - 1 ? "END" : $"#{hopNum}",
                    FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                    Foreground = hop.IsOrigin || hop.IsDestination
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F))
                        : new SolidColorBrush(Color.FromRgb(0x44, 0x55, 0x66)),
                    VerticalAlignment = VerticalAlignment.Center };
                var secTb = new TextBlock {
                    Text = hop.Security.ToString("F1"),
                    FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                    Foreground = BrushFromHex(hop.SecColor),
                    VerticalAlignment = VerticalAlignment.Center };
                var nameTb = new TextBlock {
                    Text = hop.Name,
                    FontFamily = new FontFamily("Consolas"), FontSize = Sm,
                    FontWeight = hop.IsOrigin || hop.IsDestination ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xD6, 0xF6)),
                    VerticalAlignment = VerticalAlignment.Center };

                Grid.SetColumn(numTb,  0);
                Grid.SetColumn(secTb,  1);
                Grid.SetColumn(nameTb, 2);
                hg.Children.Add(numTb);
                hg.Children.Add(secTb);
                hg.Children.Add(nameTb);

                if (hop.ShipKills > 0)
                {
                    var kb = Badge(hop.ShipKills.ToString() + "k", "#FFEF5350");
                    Grid.SetColumn(kb, 3);
                    hg.Children.Add(kb);
                }
                if (hop.Jumps > 0)
                {
                    var jb = Badge(hop.Jumps.ToString() + "j", "#FF4FC3F7");
                    Grid.SetColumn(jb, 4);
                    hg.Children.Add(jb);
                }

                hopBorder.Child = hg;
                var cId = hop.SystemId; var cName = hop.Name;
                hopBorder.MouseLeftButtonDown += async (s, e) =>
                {
                    e.Handled = true;
                    SystemSearchBox.Text = cName;
                    SetActiveTab("system");
                    await LoadSystemAsync(cId);
                };
                HoverBorder(hopBorder, "#08FFFFFF", "#15FFFFFF");
                sp.Children.Add(hopBorder);
                hopNum++;
            }
            ContentArea.Content = sp;
        }

        private void RenderSov(string filter)
        {
            var sp = new StackPanel();

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _sovData
                : _sovData.Where(s =>
                    s.AllianceName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    s.SystemName.Contains(filter,   StringComparison.OrdinalIgnoreCase)).ToList();

            // Group by alliance
            var groups = filtered
                .GroupBy(s => s.AllianceName)
                .OrderByDescending(g => g.Count());

            sp.Children.Add(T($"{_sovData.Select(s => s.AllianceId).Distinct().Count()} alliances  ·  {_sovData.Count} systems sampled",
                "#FF8A99AA", Xs));
            sp.Children.Add(Divider());

            foreach (var grp in groups)
            {
                // Alliance header
                var alliHeader = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(0x15, 0xEF, 0x53, 0x50)),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x53, 0x50)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding         = new Thickness(4, 3, 4, 3),
                    Margin          = new Thickness(0, 4, 0, 2),
                };
                var ahGrid = new Grid();
                ahGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ahGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var alliName = new TextBlock {
                    Text = grp.Key, FontFamily = new FontFamily("Consolas"),
                    FontSize = Sm, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                    TextTrimming = TextTrimming.CharacterEllipsis };
                var sysCount = new TextBlock {
                    Text = $"{grp.Count()} sys",
                    FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xAA)),
                    VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(alliName, 0); Grid.SetColumn(sysCount, 1);
                ahGrid.Children.Add(alliName); ahGrid.Children.Add(sysCount);
                alliHeader.Child = ahGrid;
                sp.Children.Add(alliHeader);

                // Systems in this alliance
                foreach (var sys in grp)
                {
                    var sysRow = AdjRow(sys.SystemName, sys.Security, sys.SecColor, 0, 0);
                    sysRow.Cursor = Cursors.Hand;
                    var cName = sys.SystemName;
                    sysRow.MouseLeftButtonDown += async (s, e) =>
                    {
                        e.Handled = true;
                        SystemSearchBox.Text = cName;
                        SetActiveTab("system");
                        await DoSystemSearchAsync();
                    };
                    HoverBorder(sysRow);
                    sp.Children.Add(sysRow);
                }
            }
            ContentArea.Content = sp;
        }

        // ── Shared UI helpers ─────────────────────────────────────────────
        private void ShowLoading(string msg) => ContentArea.Content = new TextBlock
        {
            Text = $"⏳  {msg}", FontFamily = new FontFamily("Consolas"),
            FontSize = G, Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        private void ShowError(string msg) => ContentArea.Content = new TextBlock
        {
            Text = $"⚠️  {msg}", FontFamily = new FontFamily("Consolas"),
            FontSize = Sm, Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            TextWrapping = TextWrapping.Wrap,
        };

        private static TextBlock T(string text, string hex, double size, bool bold = false) => new()
        {
            Text = text, FontFamily = new FontFamily("Consolas"), FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = BrushFromHex(hex),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0),
        };

        private static TextBlock SectionHead(string t, string hex) => new()
        {
            Text = t, FontFamily = new FontFamily("Consolas"), FontSize = Sm,
            FontWeight = FontWeights.Bold, Foreground = BrushFromHex(hex),
            Margin = new Thickness(0, 6, 0, 3),
        };

        private static Border Divider() => new()
        {
            Height = 1, Margin = new Thickness(0, 6, 0, 6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x31, 0x48)),
        };

        private static Border SecBadge(double sec, string color) => new()
        {
            Background      = BrushFromHex(color + "33"),
            BorderBrush     = BrushFromHex(color),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock {
                Text = sec.ToString("F1"), FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold, FontSize = G + 2,
                Foreground = BrushFromHex(color) }
        };

        private static StackPanel StatCell(string label, string value, string hex, int col)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock {
                Text = value, FontFamily = new FontFamily("Consolas"),
                FontSize = G, FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex(hex), HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock {
                Text = label, FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xAA)),
                HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetColumn(sp, col);
            return sp;
        }

        private static Border AdjRow(string name, double sec, string secColor, int kills, int jumps)
        {
            var b = new Border {
                Margin = new Thickness(0, 1, 0, 1), Padding = new Thickness(6, 4, 6, 4),
                CornerRadius = new CornerRadius(3),
                Background   = new SolidColorBrush(Color.FromArgb(0x11, 0x4F, 0xC3, 0xF7)),
                BorderBrush  = new SolidColorBrush(Color.FromArgb(0x22, 0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var secTb = new TextBlock { Text = sec.ToString("F1") + " ",
                FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                Foreground = BrushFromHex(secColor), VerticalAlignment = VerticalAlignment.Center };
            var nameTb = new TextBlock { Text = name,
                FontFamily = new FontFamily("Consolas"), FontSize = Sm, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xD6, 0xF6)),
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(secTb,  0); Grid.SetColumn(nameTb, 1);
            g.Children.Add(secTb); g.Children.Add(nameTb);
            if (kills > 0) { var kb = Badge(kills + "k", "#FFEF5350"); Grid.SetColumn(kb, 2); g.Children.Add(kb); }
            if (jumps > 0) { var jb = Badge(jumps + "j", "#FF4FC3F7"); Grid.SetColumn(jb, 3); g.Children.Add(jb); }
            var arrow = new TextBlock { Text = "→", FontSize = Sm, Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            Grid.SetColumn(arrow, 4); g.Children.Add(arrow);
            b.Child = g;
            return b;
        }

        private static Border Badge(string text, string hex) => new()
        {
            Background = BrushFromHex(hex + "33"), BorderBrush = BrushFromHex(hex),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontFamily = new FontFamily("Consolas"),
                FontSize = Xs, Foreground = BrushFromHex(hex), FontWeight = FontWeights.Bold }
        };

        private UIElement Link(string label, string url)
        {
            var tb = new TextBlock {
                Text = label, FontFamily = new FontFamily("Consolas"), FontSize = Xs,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                Cursor = Cursors.Hand, TextDecorations = TextDecorations.Underline,
                Margin = new Thickness(0, 2, 0, 2) };
            tb.MouseLeftButtonDown += (s, e) =>
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return tb;
        }

        private static void HoverBorder(Border b,
            string normalHex = "#114FC3F7", string hoverHex = "#224FC3F7")
        {
            b.MouseEnter += (s, e) => b.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hoverHex));
            b.MouseLeave += (s, e) => b.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(normalHex));
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            if (hex.Length > 7) hex = "#" + hex[^6..];
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return Brushes.White; }
        }
    }
}

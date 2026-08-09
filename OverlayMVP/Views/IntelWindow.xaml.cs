// filename: Views/IntelWindow.xaml.cs
using System;
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
    public partial class IntelWindow : Window
    {
        /// <summary>Translations for {Binding Loc.X}. This window had no
        /// DataContext, so every string in it was a hardcoded literal.</summary>
        public LocalizationManager Loc => LocalizationManager.Instance;

        private readonly IntelSearchService _intel = new();
        private CancellationTokenSource    _cts    = new();
        private Point _dragOffset;
        private int   _lookbackDays = 90;

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 3;
        private const int WM_NCHITTEST     = 0x0084;

        // Font size helpers — read from App resources (match overlay font setting)
        private static double G  => GetGlobal();
        private static double Sm => Math.Max(6, GetGlobal() - 2);
        private static double Xs => Math.Max(5, GetGlobal() - 3);
        private static double GetGlobal()
            => Application.Current?.Resources["GlobalFontSize"] is double d ? d : 11.0;

        public IntelWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowResizeHelper.Enable(this);
            // Also hook MA_NOACTIVATE so clicking the Intel window doesn't steal focus from EVE.
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.AddHook(NoActivateHook);
        }

        private IntPtr NoActivateHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE) { handled = true; return (IntPtr)MA_NOACTIVATE; }
            return IntPtr.Zero;
        }

        // ── Drag ─────────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            e.Handled = true;
            _dragOffset = e.GetPosition(this);
            ((IInputElement)s).CaptureMouse();
        }
        private void TitleBar_MouseMove(object s, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !((IInputElement)s).IsMouseCaptured) return;
            var sc = PointToScreen(e.GetPosition(this));
            Left = sc.X - _dragOffset.X;
            Top  = sc.Y - _dragOffset.Y;
        }
        private void TitleBar_MouseUp(object s, MouseButtonEventArgs e)
            => ((IInputElement)s).ReleaseMouseCapture();

        // ── Buttons ───────────────────────────────────────────────────────
        // FIX: replaced broken <KeyBinding Command="{Binding SearchCommand}"/> (no DataContext)
        // with a KeyDown event handler — identical pattern to SystemWindow.
        private void SearchBox_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                _ = DoSearchAsync(SearchBox.Text.Trim());
        }

        private async void Search_Click(object s, RoutedEventArgs e)
            => await DoSearchAsync(SearchBox.Text.Trim());
        private void Close_Click(object s, RoutedEventArgs e) => Close();

        private void LookbackSlider_ValueChanged(object s,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            _lookbackDays = (int)e.NewValue;
            if (LookbackLabel is not null)
            {
                LookbackLabel.Text = _lookbackDays >= 365 ? "1 year"
                    : _lookbackDays >= 30 ? $"{_lookbackDays / 30}mo {_lookbackDays % 30}d"
                    : $"{_lookbackDays} days";
            }
        }
        private void Clear_Click(object s, RoutedEventArgs e)
        {
            ResultContainer.Content = null;
            SearchBox.Text = "";
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            _intel.Dispose();
            base.OnClosed(e);
        }

        // ── Search ────────────────────────────────────────────────────────
        public async Task DoSearchAsync(string name, bool addToRight = false)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            Debug.WriteLine($"[Intel] Searching: {name}");

            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            var placeholder = CreateLoadingPanel(name);
            ResultContainer.Content = placeholder;

            Border replacement;
            try
            {
                var result = await _intel.SearchAsync(name, ct,
                    msg => Dispatcher.Invoke(() =>
                    {
                        if (placeholder.Child is TextBlock tb)
                            tb.Text = $"⏳  {msg}";
                    }),
                    _lookbackDays);
                await _intel.EnrichShipNamesAsync(result.Ships, ct);
                replacement = CreateResultPanel(result);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { replacement = CreateErrorPanel(name, ex.Message); }

            Dispatcher.Invoke(() => ResultContainer.Content = replacement);
        }

        // ── Panel factories ───────────────────────────────────────────────
        private static Border CreateLoadingPanel(string name) => new Border
        {
            MinHeight = 200,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x31, 0x48)),
            Child = new TextBlock
            {
                Text = $"⏳  Loading {name}…",
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Consolas"),
                FontSize = G,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        };

        private static Border CreateErrorPanel(string name, string error)
        {
            Debug.WriteLine($"[Intel ERROR] {name}: {error}");
            var sp = new StackPanel { Margin = new Thickness(12) };
            sp.Children.Add(new TextBlock {
                Text = "⚠️  " + name, FontWeight = FontWeights.Bold,
                FontSize = G, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)) });
            sp.Children.Add(new TextBlock {
                Text = error, FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            return WrapInBorder(sp);
        }

        private Border CreateResultPanel(IntelPlayerResult r)
        {
            var sp = new StackPanel { Margin = new Thickness(10) };

            // ── Header ────────────────────────────────────────────────────
            sp.Children.Add(Bold(r.CharacterName, "#FFCCD6F6", G + 2));
            sp.Children.Add(Dim(r.Corporation));
            if (!string.IsNullOrEmpty(r.Alliance))
                sp.Children.Add(Colored($"[{r.Alliance}]", "#FFFFD54F"));
            sp.Children.Add(Dim($"Sec: {r.SecurityStatus}  ·  {r.KillsAnalyzed}k / {r.LossesAnalyzed}l", Xs));
            if (r.LastSeen > DateTime.MinValue)
                sp.Children.Add(Dim($"Last seen: {r.LastSeen:yyyy-MM-dd}", Xs));
            sp.Children.Add(Separator());

            // ── Ships ─────────────────────────────────────────────────────
            sp.Children.Add(SectionHeader("SHIPS USED", "#FF4FC3F7"));

            // Group by ship type so kills and deaths appear on the same row
            var shipGroups = r.Ships
                .GroupBy(s => s.TypeId)
                .OrderByDescending(g => g.Sum(s => s.Count));

            foreach (var group in shipGroups)
            {
                var killer = group.FirstOrDefault(s => s.AsKiller);
                var loss   = group.FirstOrDefault(s => !s.AsKiller);
                var rep    = killer ?? loss!;

                var shipBlock = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

                // Row: name | kill count | death count | zkill link
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameBlock = new TextBlock
                {
                    Text = rep.ShipName, Foreground = Brushes.White,
                    FontSize = Sm, FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                // Kill count — green, only shown if this ship was used as attacker
                var killBlock = new TextBlock
                {
                    Text     = killer != null ? $"⚔ {killer.Count}" : "",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
                    FontSize = Sm, FontFamily = new FontFamily("Consolas"),
                    Margin   = new Thickness(6, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip  = killer != null ? $"{killer.Count} kill(s) in this ship" : null,
                };

                // Death count — red, only shown if this ship was lost
                var deathBlock = new TextBlock
                {
                    Text     = loss != null ? $"💀 {loss.Count}" : "",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                    FontSize = Sm, FontFamily = new FontFamily("Consolas"),
                    Margin   = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip  = loss != null ? $"{loss.Count} loss(es) in this ship" : null,
                };

                // zkill link — prefer kill km, fall back to loss km
                int kmId = killer?.LatestKillmailId ?? loss?.LatestKillmailId ?? 0;
                var zkillBtn = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(0x22, 0x4F, 0xC3, 0xF7)),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(0x44, 0x4F, 0xC3, 0xF7)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(2),
                    Padding         = new Thickness(4, 1, 4, 1),
                    Cursor          = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "🔗 zkill", FontSize = Xs,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                        FontFamily = new FontFamily("Consolas"),
                    }
                };
                var capturedKmId = kmId;
                zkillBtn.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    if (capturedKmId > 0)
                        Process.Start(new ProcessStartInfo(
                            $"https://zkillboard.com/kill/{capturedKmId}/")
                            { UseShellExecute = true });
                };
                zkillBtn.MouseEnter += (s, e) =>
                    zkillBtn.Background = new SolidColorBrush(Color.FromArgb(0x44, 0x4F, 0xC3, 0xF7));
                zkillBtn.MouseLeave += (s, e) =>
                    zkillBtn.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x4F, 0xC3, 0xF7));

                Grid.SetColumn(nameBlock,  0);
                Grid.SetColumn(killBlock,  1);
                Grid.SetColumn(deathBlock, 2);
                Grid.SetColumn(zkillBtn,   3);
                row.Children.Add(nameBlock);
                row.Children.Add(killBlock);
                row.Children.Add(deathBlock);
                row.Children.Add(zkillBtn);
                shipBlock.Children.Add(row);

                // Fit — losses only. Modules and charges are two completely separate
                // collapsible sections so they are never visually mixed.
                if (loss?.Fit.Count > 0)
                {
                    var modules = loss.Fit
                        .Where(m => !m.IsCharge)
                        .OrderBy(m => SlotOrder(m.Slot))
                        .ThenBy(m => m.Name)
                        .ToList();

                    var charges = loss.Fit
                        .Where(m => m.IsCharge)
                        .OrderBy(m => m.Name)
                        .ToList();

                    // ── Section 1: fitted modules ─────────────────────────────
                    if (modules.Count > 0)
                    {
                        var fitPanel = new StackPanel
                        {
                            Margin     = new Thickness(14, 2, 0, 2),
                            Visibility = Visibility.Collapsed,
                        };

                        var currentSlot = "";
                        foreach (var mod in modules)
                        {
                            if (mod.Slot != currentSlot)
                            {
                                currentSlot = mod.Slot;
                                fitPanel.Children.Add(new TextBlock
                                {
                                    Text = $"── {mod.Slot} ──",
                                    FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                                    Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0x8A, 0x99, 0xAA)),
                                    Margin = new Thickness(0, 2, 0, 1),
                                });
                            }
                            fitPanel.Children.Add(new TextBlock
                            {
                                Text = mod.Quantity > 1 ? $"{mod.Name} ×{mod.Quantity}" : mod.Name,
                                FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xD6, 0xF6)),
                                Margin = new Thickness(0, 0, 0, 1),
                            });
                        }

                        var fitToggle = new TextBlock
                        {
                            Text = "▶ Show fit",
                            FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xD5, 0x4F)),
                            Margin = new Thickness(14, 1, 0, 0),
                            Cursor = Cursors.Hand,
                        };
                        bool fitVisible = false;
                        fitToggle.MouseLeftButtonDown += (s, e) =>
                        {
                            e.Handled = true;
                            fitVisible = !fitVisible;
                            fitPanel.Visibility = fitVisible ? Visibility.Visible : Visibility.Collapsed;
                            fitToggle.Text = fitVisible ? "▼ Hide fit" : "▶ Show fit";
                        };
                        shipBlock.Children.Add(fitToggle);
                        shipBlock.Children.Add(fitPanel);
                    }

                    // ── Section 2: charges / scripts / cap boosters ───────────
                    if (charges.Count > 0)
                    {
                        var chargePanel = new StackPanel
                        {
                            Margin     = new Thickness(14, 2, 0, 4),
                            Visibility = Visibility.Collapsed,
                        };

                        foreach (var charge in charges)
                        {
                            chargePanel.Children.Add(new TextBlock
                            {
                                Text = charge.Quantity > 1
                                    ? $"{charge.Name} ×{charge.Quantity}" : charge.Name,
                                FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)),
                                Margin = new Thickness(0, 0, 0, 1),
                            });
                        }

                        var chargeToggle = new TextBlock
                        {
                            Text = "▶ Show charges",
                            FontSize = Xs, FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xA5, 0x00)),
                            Margin = new Thickness(14, 1, 0, 0),
                            Cursor = Cursors.Hand,
                        };
                        bool chargeVisible = false;
                        chargeToggle.MouseLeftButtonDown += (s, e) =>
                        {
                            e.Handled = true;
                            chargeVisible = !chargeVisible;
                            chargePanel.Visibility = chargeVisible ? Visibility.Visible : Visibility.Collapsed;
                            chargeToggle.Text = chargeVisible ? "▼ Hide charges" : "▶ Show charges";
                        };
                        shipBlock.Children.Add(chargeToggle);
                        shipBlock.Children.Add(chargePanel);
                    }
                }

                sp.Children.Add(shipBlock);
            }

            if (r.Associates.Count == 0) return WrapInBorder(new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = sp
            });

            // ── Associates ────────────────────────────────────────────────
            sp.Children.Add(Separator());
            sp.Children.Add(SectionHeader("GANG MEMBERS", "#FFEF5350"));

            foreach (var assoc in r.Associates)
            {
                var btn = new Border
                {
                    Padding         = new Thickness(6, 4, 6, 4),
                    Margin          = new Thickness(0, 1, 0, 1),
                    CornerRadius    = new CornerRadius(3),
                    Cursor          = Cursors.Hand,
                    Background      = new SolidColorBrush(Color.FromArgb(0x11, 0x4F, 0xC3, 0xF7)),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(0x33, 0x4F, 0xC3, 0xF7)),
                    BorderThickness = new Thickness(1),
                };

                var inner = new Grid();
                inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                info.Children.Add(Bold(assoc.CharacterName, "#FF4FC3F7", G));
                info.Children.Add(Dim(assoc.Corporation, Xs));
                if (!string.IsNullOrEmpty(assoc.MostUsedShip))
                    info.Children.Add(Colored(assoc.MostUsedShip, "#FFFFD54F", Xs));

                var badge = new Border
                {
                    Background    = new SolidColorBrush(Color.FromArgb(0x44, 0xEF, 0x53, 0x50)),
                    CornerRadius  = new CornerRadius(3),
                    Padding       = new Thickness(5, 2, 5, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = assoc.Appearances.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                        FontSize = Sm, FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Consolas"),
                    }
                };

                Grid.SetColumn(info,  0);
                Grid.SetColumn(badge, 1);
                inner.Children.Add(info);
                inner.Children.Add(badge);
                btn.Child = inner;

                // Click → open new IntelWindow for this associate
                var capturedName = assoc.CharacterName;
                btn.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    var w = new IntelWindow();
                    w.Left = Left + ActualWidth + 8;
                    w.Top  = Top;
                    w.Show();
                    _ = w.DoSearchAsync(capturedName);
                };
                btn.MouseEnter += (s, e) =>
                    btn.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x4F, 0xC3, 0xF7));
                btn.MouseLeave += (s, e) =>
                    btn.Background = new SolidColorBrush(Color.FromArgb(0x11, 0x4F, 0xC3, 0xF7));

                sp.Children.Add(btn);
            }

            return WrapInBorder(new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = sp
            });
        }

        // ── UI helpers ────────────────────────────────────────────────────
        private static Border WrapInBorder(UIElement child) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0D, 0x11, 0x17)),
            Child = child,
        };

        private static TextBlock Bold(string text, string hex, double size = 0) => new TextBlock
        {
            Text = text, FontWeight = FontWeights.Bold,
            FontSize = size > 0 ? size : G,
            FontFamily = new FontFamily("Consolas"),
            Foreground = BrushFromHex(hex),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        private static TextBlock Dim(string text, double size = 0) => new TextBlock
        {
            Text = text, FontSize = size > 0 ? size : Sm,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xAA)),
            Margin = new Thickness(0, 1, 0, 0),
        };

        private static TextBlock Colored(string text, string hex, double size = 0) => new TextBlock
        {
            Text = text, FontSize = size > 0 ? size : Sm,
            FontFamily = new FontFamily("Consolas"),
            Foreground = BrushFromHex(hex), Margin = new Thickness(0, 1, 0, 0),
        };

        private static TextBlock SectionHeader(string text, string hex) => new TextBlock
        {
            Text = text, FontSize = Sm, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = BrushFromHex(hex),
            Margin = new Thickness(0, 6, 0, 3),
        };

        private static Border Separator() => new Border
        {
            Height = 1, Margin = new Thickness(0, 6, 0, 6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x31, 0x48)),
        };

        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hex)); }
            catch { return Brushes.White; }
        }

        // Display order for fit slots — mirrors EVE's in-game layout
        private static int SlotOrder(string slot) => slot switch
        {
            "High"  => 0,
            "Mid"   => 1,
            "Low"   => 2,
            "Rig"   => 3,
            "Sub"   => 4,
            "Cargo" => 5,
            _       => 6,
        };
    }
}

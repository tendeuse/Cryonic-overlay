// filename: Views/ShipFitWindow.xaml.cs
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OverlayMVP.Models;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class ShipFitWindow : Window
    {
        /// <summary>Translations for {Binding Loc.X}. This window had no
        /// DataContext, so every string in it was a hardcoded literal.</summary>
        public LocalizationManager Loc => LocalizationManager.Instance;

        private static ShipFitWindow? _current;

        public ShipFitWindow(string hullName, ShipSpec? spec = null)
        {
            InitializeComponent();
            DataContext = this;

            // Only one fit window at a time
            _current?.Close();
            _current = this;
            Closed += (_, _) => { if (_current == this) _current = null; };

            var fit = ShipFitCatalogue.TryGet(hullName);
            TitleText.Text = hullName;
            Title = $"Fit — {hullName}";
            BuildContent(hullName, fit, spec);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowResizeHelper.Enable(this);
        }

        private void BuildContent(string hull, ShipFitData? fit, ShipSpec? spec)
        {
            ContentPanel.Children.Clear();

            // Hull header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x4F, 0xC3, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4),
            };
            var hStack = new StackPanel();
            hStack.Children.Add(MakeLine(hull, "#FF4FC3F7", 14, FontWeights.Bold));
            if (fit != null)
            {
                hStack.Children.Add(MakeLine($"{fit.ShipClass}  ·  {fit.Role}", "#FF8A99AA", 11));
                hStack.Children.Add(MakeLine(fit.IskEstimate, "#FFFFD54F", 11));
            }
            else if (spec != null)
            {
                hStack.Children.Add(MakeLine($"Target: {spec.DpsLabel}  ·  {spec.EhpLabel}", "#FF8A99AA", 11));
            }
            header.Child = hStack;
            ContentPanel.Children.Add(header);

            // Copy buttons row (only when we have actual fit data)
            if (fit != null)
            {
                var btnRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 8),
                };

                var btnFit = new Button
                {
                    Content    = "📋 Copy Fit",
                    FontSize   = 11,
                    Padding    = new Thickness(8, 3, 8, 3),
                    Margin     = new Thickness(0, 0, 6, 0),
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4F, 0xC3, 0xF7)),
                    BorderBrush= new SolidColorBrush(Color.FromArgb(0x88, 0x4F, 0xC3, 0xF7)),
                    Foreground = HexBrush("#FF4FC3F7"),
                    Cursor     = System.Windows.Input.Cursors.Hand,
                    ToolTip    = "Copy fit in EFT format — paste into EVE fitting window (Ctrl+V)",
                };
                btnFit.Click += (_, _) =>
                {
                    try { Clipboard.SetText(BuildEftString(hull, fit)); }
                    catch { return; }
                    var orig = btnFit.Content;
                    btnFit.Content = "✅ Copied!";
                    var t = new System.Windows.Threading.DispatcherTimer
                        { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) => { btnFit.Content = orig; t.Stop(); };
                    t.Start();
                };

                var btnSkills = new Button
                {
                    Content    = "🎓 Copy Skills",
                    FontSize   = 11,
                    Padding    = new Thickness(8, 3, 8, 3),
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xD5, 0x4F)),
                    BorderBrush= new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xD5, 0x4F)),
                    Foreground = HexBrush("#FFFFD54F"),
                    Cursor     = System.Windows.Input.Cursors.Hand,
                    ToolTip    = "Copy required skills list — paste into EVE skill plan import",
                };
                btnSkills.Click += (_, _) =>
                {
                    try { Clipboard.SetText(BuildSkillsString(hull, fit)); }
                    catch { return; }
                    var orig = btnSkills.Content;
                    btnSkills.Content = "✅ Copied!";
                    var t = new System.Windows.Threading.DispatcherTimer
                        { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) => { btnSkills.Content = orig; t.Stop(); };
                    t.Start();
                };

                btnRow.Children.Add(btnFit);
                btnRow.Children.Add(btnSkills);
                ContentPanel.Children.Add(btnRow);
            }

            if (fit == null)
            {
                // No fit data — show what we know from spec
                ContentPanel.Children.Add(MakeSectionLabel("📋 NO DETAILED FIT DATA"));
                ContentPanel.Children.Add(MakeLine("This hull has no fit entry in the catalogue yet.", "#FF8A99AA", 11));
                if (spec != null)
                {
                    ContentPanel.Children.Add(Spacer(6));
                    ContentPanel.Children.Add(MakeSectionLabel("MINIMUM REQUIREMENTS (from mission spec)"));
                    ContentPanel.Children.Add(MakeLine($"⚔  DPS goal:     {spec.DpsLabel}", "#FF66BB6A", 11));
                    ContentPanel.Children.Add(MakeLine($"🛡  EHP goal:     {spec.EhpLabel}", "#FF4FC3F7", 11));
                    if (spec.NeedsCargo)
                        ContentPanel.Children.Add(MakeLine($"📦  Cargo:        {spec.CargoLabel}", "#FFFFD54F", 11));
                    if (spec.ResistProfile.Length > 0)
                        ContentPanel.Children.Add(MakeLine($"🔥  Tank:         {spec.ResistList}", "#FFEF5350", 11));
                    if (!string.IsNullOrWhiteSpace(spec.FitNote))
                    {
                        ContentPanel.Children.Add(Spacer(4));
                        ContentPanel.Children.Add(MakeWrapped(spec.FitNote, "#FFB0BEC5", 11));
                    }
                }
                return;
            }

            // Required skills
            if (fit.RequiredSkills.Length > 0)
            {
                ContentPanel.Children.Add(MakeSectionLabel("🎓 REQUIRED SKILLS"));
                foreach (var s in fit.RequiredSkills)
                    ContentPanel.Children.Add(MakeBullet(s, "#FFFFD54F"));
                ContentPanel.Children.Add(Spacer(6));
            }

            // High slots
            if (fit.HighSlots.Length > 0)
            {
                ContentPanel.Children.Add(MakeSectionLabel("🔴 HIGH SLOTS"));
                foreach (var s in fit.HighSlots)
                    ContentPanel.Children.Add(MakeBullet(s, "#FFEF5350"));
                ContentPanel.Children.Add(Spacer(4));
            }

            // Mid slots
            if (fit.MidSlots.Length > 0)
            {
                ContentPanel.Children.Add(MakeSectionLabel("🔵 MID SLOTS"));
                foreach (var s in fit.MidSlots)
                    ContentPanel.Children.Add(MakeBullet(s, "#FF4FC3F7"));
                ContentPanel.Children.Add(Spacer(4));
            }

            // Low slots
            if (fit.LowSlots.Length > 0)
            {
                ContentPanel.Children.Add(MakeSectionLabel("🟡 LOW SLOTS"));
                foreach (var s in fit.LowSlots)
                    ContentPanel.Children.Add(MakeBullet(s, "#FFFFD54F"));
                ContentPanel.Children.Add(Spacer(4));
            }

            // Rigs
            if (fit.RigSlots.Length > 0)
            {
                ContentPanel.Children.Add(MakeSectionLabel("⚙ RIGS"));
                foreach (var s in fit.RigSlots)
                    ContentPanel.Children.Add(MakeBullet(s, "#FF66BB6A"));
                ContentPanel.Children.Add(Spacer(4));
            }

            // Notes
            if (!string.IsNullOrWhiteSpace(fit.Notes))
            {
                ContentPanel.Children.Add(MakeSectionLabel("💡 NOTES"));
                ContentPanel.Children.Add(MakeWrapped(fit.Notes, "#FFB0BEC5", 11));
            }
        }

        // ── Clipboard formatters ───────────────────────────────────────────────

        /// <summary>
        /// Converts a catalogue slot entry to EFT module format.
        /// "Heavy Missile Launcher II [Scourge Heavy Missile]" → "Heavy Missile Launcher II, Scourge Heavy Missile"
        /// "— (empty)" → null (slot should be skipped in EFT output)
        /// </summary>
        private static string? ToEftModule(string slotEntry)
        {
            if (slotEntry.StartsWith("—") || slotEntry.StartsWith("-"))
                return null;   // empty slot — omit from EFT output

            var bracketIdx = slotEntry.IndexOf('[');
            if (bracketIdx > 0)
            {
                var module = slotEntry[..bracketIdx].TrimEnd();
                var charge = slotEntry[(bracketIdx + 1)..].TrimEnd(']', ' ');
                return $"{module}, {charge}";
            }
            return slotEntry;
        }

        /// <summary>
        /// Builds an EFT-format string that EVE's in-game fitting window can import (Ctrl+V).
        /// Format:  [HullName, Fit Name] / High slots / blank / Mid slots / blank / Low / blank / Rigs
        /// </summary>
        private static string BuildEftString(string hull, ShipFitData fit)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{hull}, ARC Mission Fit]");
            sb.AppendLine();

            foreach (var m in fit.HighSlots)
            {
                var eft = ToEftModule(m);
                if (eft != null) sb.AppendLine(eft);
            }
            sb.AppendLine();

            foreach (var m in fit.MidSlots)
            {
                var eft = ToEftModule(m);
                if (eft != null) sb.AppendLine(eft);
            }
            sb.AppendLine();

            foreach (var m in fit.LowSlots)
            {
                var eft = ToEftModule(m);
                if (eft != null) sb.AppendLine(eft);
            }
            sb.AppendLine();

            foreach (var m in fit.RigSlots)
            {
                var eft = ToEftModule(m);
                if (eft != null) sb.AppendLine(eft);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds a plain-text required skills list for the fit.
        /// Compatible with EVE's skill plan importer ("Skill Name Level X" per line).
        /// Strips descriptive suffixes (e.g. " — to fly") from catalogue entries.
        /// </summary>
        private static string BuildSkillsString(string hull, ShipFitData fit)
        {
            var sb = new StringBuilder();
            foreach (var skill in fit.RequiredSkills)
            {
                // Strip " — description" or " - description" suffixes
                var clean = skill;
                var dashIdx = clean.IndexOf(" — ");
                if (dashIdx < 0) dashIdx = clean.IndexOf(" - ");
                if (dashIdx > 0) clean = clean[..dashIdx];
                sb.AppendLine(clean.TrimEnd());
            }
            return sb.ToString().TrimEnd();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static TextBlock MakeLine(string text, string hex, double size,
            FontWeight? weight = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = HexBrush(hex),
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
            };
            if (weight.HasValue) tb.FontWeight = weight.Value;
            return tb;
        }

        private static TextBlock MakeWrapped(string text, string hex, double size)
            => new TextBlock
            {
                Text = text,
                Foreground = HexBrush(hex),
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = size + 3,
            };

        private static StackPanel MakeBullet(string text, string hex)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            sp.Children.Add(new TextBlock
            {
                Text = "  •  ",
                Foreground = HexBrush("#FF445566"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Top,
            });
            sp.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = HexBrush(hex),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            });
            return sp;
        }

        private static TextBlock MakeSectionLabel(string text)
            => new TextBlock
            {
                Text = text,
                Foreground = HexBrush("#FF8A99AA"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 2),
            };

        private static UIElement Spacer(double h)
            => new Border { Height = h };

        private static SolidColorBrush HexBrush(string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return new SolidColorBrush(Colors.White); }
        }

        // ── Window chrome ──────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => DragMove();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}

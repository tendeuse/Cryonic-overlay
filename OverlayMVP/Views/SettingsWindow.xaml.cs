// filename: Views/SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly AppDb         _db;
        private readonly OverlayConfig _cfg;

        private const string FontSizeKey     = "ui_font_size";
        private const double FontSizeDefault = 11.0;

        /// <summary>
        /// Push a font size into the application resources. Every FontSize in
        /// the app is a {DynamicResource} onto these three keys, so writing
        /// them re-renders the running UI immediately -- no restart.
        ///
        /// Lives here, next to Load/SaveFontSize, because the Sm/Xs offsets are
        /// part of what "the font size" means. Startup and the Settings window
        /// both call it, so the two cannot drift apart.
        /// </summary>
        public static void ApplyFontSize(double size)
        {
            var res = Application.Current?.Resources;
            if (res is null) return;
            res["GlobalFontSize"]   = size;
            res["GlobalFontSizeSm"] = Math.Max(6.0, size - 2.0);
            res["GlobalFontSizeXs"] = Math.Max(5.0, size - 3.0);
        }

        public static double LoadFontSize(AppDb db)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", FontSizeKey);
                var raw = cmd.ExecuteScalar() as string;
                if (raw != null && double.TryParse(raw,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return Math.Clamp(d, 8, 18);
            }
            catch { }
            return FontSizeDefault;
        }

        private static void SaveFontSize(AppDb db, double size)
        {
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) " +
                              "ON CONFLICT(k) DO UPDATE SET v=excluded.v";
            cmd.Parameters.AddWithValue("$k", FontSizeKey);
            cmd.Parameters.AddWithValue("$v",
                size.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        public SettingsWindow(AppDb db, OverlayConfig cfg)
        {
            InitializeComponent();
            _db  = db;
            _cfg = cfg;

            foreach (ComboBoxItem item in FactionBox.Items)
                if (item.Tag?.ToString() == cfg.FactionFocus)
                    { FactionBox.SelectedItem = item; break; }
            if (FactionBox.SelectedItem is null) FactionBox.SelectedIndex = 0;

            foreach (ComboBoxItem item in AlphaBox.Items)
                if (item.Tag?.ToString() == cfg.AlphaOmega)
                    { AlphaBox.SelectedItem = item; break; }
            if (AlphaBox.SelectedItem is null) AlphaBox.SelectedIndex = 0;

            var currentSize = LoadFontSize(db);
            FontSizeSlider.Value = currentSize;
            FontSizeLabel.Text   = $"{currentSize:F0} pt";

            ChkMultibox.IsChecked        = cfg.ShowMultibox;
            ChkIntelAlerts.IsChecked     = cfg.ShowIntelAlerts;
            ChkPilotStatus.IsChecked     = cfg.ShowPilotStatus;
            ChkStandingGuide.IsChecked   = cfg.ShowStandingGuide;
            ChkMissionProgress.IsChecked = cfg.ShowMissionProgress;
            ChkSkillPlan.IsChecked       = cfg.ShowSkillPlan;
            ChkPilotIntelBtn.IsChecked   = cfg.ShowPilotIntelBtn;
            ChkSystemInfoBtn.IsChecked   = cfg.ShowSystemInfoBtn;

            RefreshCharacterList();
        }

        // ── Multi-account character list ──────────────────────────────────
        private void RefreshCharacterList()
        {
            var tokens = EsiClient.LoadTokens(_db);
            CharacterList.ItemsSource = tokens;
            if (tokens.Count == 0)
            {
                EveStatusLabel.Text      = "No characters linked.";
                EveStatusLabel.Foreground= System.Windows.Media.Brushes.Gray;
            }
            else
            {
                EveStatusLabel.Text      = $"{tokens.Count} character(s) linked.";
                EveStatusLabel.Foreground= System.Windows.Media.Brushes.LightGreen;
            }
        }

        private async void LinkEve_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("⏳  Opening browser — log in as the character you want to add…");
            LinkEveBtn.IsEnabled = false;
            try
            {
                using var esi = new EsiClient(_db);
                string charName = await esi.AuthorizeAsync();
                RefreshCharacterList();
                SetStatus($"✅  {charName} linked successfully!");
            }
            catch (Exception ex)
            {
                SetStatus($"❌  {ex.Message}", error: true);
            }
            finally { LinkEveBtn.IsEnabled = true; }
        }

        private void SetActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EsiToken token)
                SetStatus($"✅  {token.CharacterName} set as active character. " +
                          "Changes take effect on the next 30s refresh.");
            // The overlay VM will pick this up via ActiveCharacter binding in settings
        }

        private void UnlinkChar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not EsiToken token) return;
            var result = MessageBox.Show(
                $"Unlink {token.CharacterName}?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            EsiClient.DeleteToken(_db, token.CharacterId);
            RefreshCharacterList();
            SetStatus($"{token.CharacterName} unlinked.");
        }

        // ── Font size ─────────────────────────────────────────────────────
        private void FontSizeSlider_Changed(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (FontSizeLabel is null) return;
            FontSizeLabel.Text = $"{e.NewValue:F0} pt";
        }

        private void FontPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out var size))
                FontSizeSlider.Value = size;
        }

        // ── Feature flags ─────────────────────────────────────────────────
        private void SaveFeatureFlags()
        {
            _cfg.ShowMultibox        = ChkMultibox.IsChecked        == true;
            _cfg.ShowIntelAlerts     = ChkIntelAlerts.IsChecked     == true;
            _cfg.ShowPilotStatus     = ChkPilotStatus.IsChecked     == true;
            _cfg.ShowStandingGuide   = ChkStandingGuide.IsChecked   == true;
            _cfg.ShowMissionProgress = ChkMissionProgress.IsChecked == true;
            _cfg.ShowSkillPlan       = ChkSkillPlan.IsChecked       == true;
            _cfg.ShowPilotIntelBtn   = ChkPilotIntelBtn.IsChecked   == true;
            _cfg.ShowSystemInfoBtn   = ChkSystemInfoBtn.IsChecked   == true;
        }

        // ── Save ──────────────────────────────────────────────────────────
        //
        // One save path, two buttons. Both must persist exactly the same
        // things: a second copy would silently drift the moment a setting is
        // added to one and not the other.
        private void SaveAll()
        {
            _cfg.FactionFocus = (FactionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                                ?? _cfg.FactionFocus;
            _cfg.AlphaOmega   = (AlphaBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                                ?? _cfg.AlphaOmega;
            SaveFeatureFlags();
            _cfg.Save(_db);
            SaveFontSize(_db, FontSizeSlider.Value);
            ApplyFontSize(FontSizeSlider.Value);
        }

        private void SaveOnly_Click(object sender, RoutedEventArgs e)
        {
            SaveAll();
            SetStatus("✅  Saved. Close this window to apply the panel toggles.");
        }

        // Save & Close. The feature toggles cannot take effect while this
        // window is open: MainWindow calls ApplyFeatureFlags() only after
        // ShowDialog() returns. So "save then close" was the sequence the user
        // had to perform by hand every time -- this is that sequence, once.
        private void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            SaveAll();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void SetStatus(string msg, bool error = false)
        {
            StatusLabel.Text       = msg;
            StatusLabel.Foreground = error
                ? System.Windows.Media.Brushes.Salmon
                : System.Windows.Media.Brushes.LightSkyBlue;
        }
    }
}

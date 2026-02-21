// filename: Views/FirstRunWindow.xaml.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using OverlayMVP.Services;
using OverlayMVP.ViewModels;

namespace OverlayMVP.Views
{
    public partial class FirstRunWindow : Window
    {
        private readonly AppDb _db;
        private readonly FirstRunViewModel _vm;
        private readonly PairingClient _pairing = new PairingClient();

        public FirstRunWindow(AppDb db)
        {
            InitializeComponent();
            _db = db;
            _vm = new FirstRunViewModel();
            DataContext = _vm;

            // Auto-fill safe defaults (URL + initial selections)
            _vm.ApiBaseUrl = Defaults.ApiBaseUrl;
            _vm.AlphaOmega = Defaults.AlphaOmega;
            _vm.FactionFocus = Defaults.FactionFocus;
        }

        private async void SaveAndLaunch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_vm.Validate(out var err))
                {
                    _vm.Status = err;
                    MessageBox.Show(err, "Overlay Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _db.EnsureSchema();

                _vm.Status = "Pairing… exchanging code for token.";
                var (token, expiresAt) = await _pairing.ExchangeCodeAsync(_vm.ApiBaseUrl, _vm.PairCode);

                var cfg = new OverlayConfig
                {
                    ApiBaseUrl = _vm.ApiBaseUrl,
                    OverlayToken = token,
                    AlphaOmega = _vm.AlphaOmega,
                    FactionFocus = _vm.FactionFocus
                };
                cfg.Save(_db);

                var main = new MainWindow(_db);
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "overlay-error.log");
                File.WriteAllText(logPath, ex.ToString());

                MessageBox.Show(
                    $"Pair & Launch failed:\n\n{ex.Message}\n\nLog written to:\n{logPath}",
                    "Overlay Setup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}

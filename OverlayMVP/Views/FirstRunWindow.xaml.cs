// filename: OverlayMVP/Views/FirstRunWindow.xaml.cs
using System;
using System.IO;
using System.Windows;
using OverlayMVP.Services;
using OverlayMVP.ViewModels;

namespace OverlayMVP.Views
{
    public partial class FirstRunWindow : Window
    {
        private readonly AppDb _db;
        private readonly FirstRunViewModel _vm;

        public FirstRunWindow(AppDb db)
        {
            InitializeComponent();
            _db = db;
            _vm = new FirstRunViewModel();
            DataContext = _vm;

            // Auto-fill defaults:
            if (string.IsNullOrWhiteSpace(_vm.ApiBaseUrl))
                _vm.ApiBaseUrl = Defaults.ApiBaseUrl;

            _vm.AlphaOmega = Defaults.AlphaOmega;
            _vm.FactionFocus = Defaults.FactionFocus;

            // If you choose to embed a key (not recommended), you can prefill it here:
            if (!string.IsNullOrWhiteSpace(Defaults.ApiKey))
                ApiKeyBox.Password = Defaults.ApiKey;
        }

        private void SaveAndLaunch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _vm.ApiKey = ApiKeyBox.Password;

                if (!_vm.Validate(out var err))
                {
                    _vm.Status = err;
                    MessageBox.Show(err, "Overlay Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _db.EnsureSchema();

                var cfg = new OverlayConfig
                {
                    ApiBaseUrl = _vm.ApiBaseUrl,
                    ApiKey = _vm.ApiKey,
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
                    $"Save & Launch failed:\n\n{ex.Message}\n\nLog written to:\n{logPath}",
                    "Overlay Setup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}

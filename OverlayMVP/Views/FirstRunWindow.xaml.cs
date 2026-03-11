// filename: Views/FirstRunWindow.xaml.cs
//
// FIX 1: Button event is SaveAndLaunch_Click (matches FirstRunWindow.xaml)
// FIX 2: PairingClient() takes no constructor args
// FIX 3: Method is ExchangeCodeAsync(apiBaseUrl, code) not ExchangeAsync(code)
// FIX 4: Opens MainWindow BEFORE closing so app stays alive
//        (requires App.xaml.cs ShutdownMode = OnExplicitShutdown)
//
using System.Windows;
using OverlayMVP.Services;
using OverlayMVP.ViewModels;

namespace OverlayMVP.Views
{
    public partial class FirstRunWindow : Window
    {
        private readonly AppDb             _db;
        private readonly FirstRunViewModel _vm;

        public FirstRunWindow(AppDb db)
        {
            InitializeComponent();
            _db         = db;
            _vm         = new FirstRunViewModel();
            DataContext = _vm;
        }

        private async void SaveAndLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.Validate(out var err))
            {
                _vm.Status = $"❌ {err}";
                return;
            }

            _vm.Status = "⏳ Connecting…";
            IsEnabled  = false;

            try
            {
                var client = new PairingClient();
                var (token, expiresAt) = await client.ExchangeCodeAsync(
                    _vm.ApiBaseUrl.Trim(),
                    _vm.PairCode.Trim());

                if (string.IsNullOrWhiteSpace(token))
                {
                    _vm.Status = "❌ Pair exchange returned an empty token. Check your code.";
                    IsEnabled  = true;
                    return;
                }

                // Persist config
                var cfg = new OverlayConfig
                {
                    ApiBaseUrl   = _vm.ApiBaseUrl.Trim(),
                    OverlayToken = token,
                    AlphaOmega   = _vm.AlphaOmega,
                    FactionFocus = _vm.FactionFocus,
                };
                cfg.Save(_db);

                _vm.Status = "✅ Connected! Opening overlay…";

                // FIX: open MainWindow BEFORE closing this window.
                // With ShutdownMode = OnExplicitShutdown the app stays alive,
                // but opening MainWindow first ensures at least one window is
                // open even if the shutdown mode wasn't changed yet.
                if (Application.Current is App app)
                    app.OpenMainWindow(_db);

                Close();
            }
            catch (System.Exception ex)
            {
                _vm.Status = $"❌ {ex.Message}";
                IsEnabled  = true;
            }
        }
    }
}

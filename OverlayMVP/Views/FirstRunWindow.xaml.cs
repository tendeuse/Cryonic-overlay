// filename: Views/FirstRunWindow.xaml.cs
//
// First-run flow: no bot pairing — just EVE SSO login (EsiClient.AuthorizeAsync).
// Opens MainWindow BEFORE closing so the app stays alive
// (requires App.xaml.cs ShutdownMode = OnExplicitShutdown).
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

        private async void LoginWithEve_Click(object sender, RoutedEventArgs e)
        {
            _vm.Status = "⏳ Opening browser to log in with EVE…";
            IsEnabled  = false;

            try
            {
                using var esi = new EsiClient(_db);
                var charName = await esi.AuthorizeAsync();

                // Persist config (no bot pairing — just the local preferences)
                var cfg = new OverlayConfig
                {
                    AlphaOmega   = _vm.AlphaOmega,
                    FactionFocus = _vm.FactionFocus,
                };
                cfg.Save(_db);

                _vm.Status = $"✅ {charName} linked! Opening overlay…";

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

// filename: App.xaml.cs
using System.Windows;
using OverlayMVP.Services;
using OverlayMVP.Views;

namespace OverlayMVP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var db = new AppDb(AppDb.DefaultPath());
            db.EnsureSchema();

            var cfg = OverlayConfig.Load(db);
            if (cfg is null)
            {
                var wizard = new FirstRunWindow(db);
                wizard.Show();
                return;
            }

            var main = new MainWindow(db);
            main.Show();
        }
    }
}

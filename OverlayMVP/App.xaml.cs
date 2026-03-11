// filename: App.xaml.cs
//
// FIX: ShutdownMode was OnLastWindowClose (default).
// When FirstRunWindow closed after pairing, WPF silently killed the app
// before MainWindow could open. Set to OnExplicitShutdown so we control
// the lifecycle explicitly. MainWindow.OnClosed calls Shutdown().
//
using System;
using System.Windows;
using System.Windows.Threading;
using OverlayMVP.Services;
using OverlayMVP.Views;

namespace OverlayMVP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── FIX: take control of shutdown ourselves ──────────────────
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // ── Global exception handlers (catch silent crashes) ─────────
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            // ── Database + config ────────────────────────────────────────
            var db = new AppDb(AppDb.DefaultPath());
            db.EnsureSchema();

            var cfg = OverlayConfig.Load(db);
            if (cfg is null)
            {
                var wizard = new FirstRunWindow(db);
                wizard.Show();
                // App stays alive via OnExplicitShutdown — wizard calls
                // App.OpenMainWindow(db) when pairing succeeds, then closes itself.
                return;
            }

            OpenMainWindow(db);
        }

        // Called by FirstRunWindow after a successful pair exchange.
        public void OpenMainWindow(AppDb db)
        {
            var main = new MainWindow(db);
            main.Show();
        }

        // ── Exception surfaces ───────────────────────────────────────────
        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // FIX: Write to file so we can see the error even if MessageBox is missed
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "overlay_crash.txt");
                System.IO.File.WriteAllText(logPath,
                    $"[{DateTime.Now}]\n{e.Exception}\n\nInner: {e.Exception.InnerException}");
            }
            catch { }

            MessageBox.Show(
                $"Unhandled error:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Overlay — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(1);
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"Fatal error (domain):\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                "Overlay — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

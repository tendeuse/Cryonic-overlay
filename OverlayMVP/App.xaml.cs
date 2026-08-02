// filename: App.xaml.cs
//
// FIX: ShutdownMode was OnLastWindowClose (default).
// When FirstRunWindow closed after pairing, WPF silently killed the app
// before MainWindow could open. Set to OnExplicitShutdown so we control
// the lifecycle explicitly. MainWindow.OnClosed calls Shutdown().
//
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OverlayMVP.Services;
using OverlayMVP.Views;

namespace OverlayMVP
{
    public partial class App : Application
    {
        // --screenshot <path>: render MainWindow once and exit. Used by the
        // structure refactor's visual baseline. Polling and network are left
        // off so the capture is deterministic -- live intel or a changing
        // character list would make every diff noisy and useless.
        private static string? _screenshotPath;

        public static bool IsScreenshotMode => _screenshotPath is not null;

        // --skin <id>: render with a given skin instead of the saved one. Lets
        // the screenshot harness capture a skin without changing what is stored,
        // so a capture can never leave the user's own preference altered.
        private static string? _skinOverride;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var idx = Array.IndexOf(e.Args, "--screenshot");
            if (idx >= 0 && idx + 1 < e.Args.Length) _screenshotPath = e.Args[idx + 1];

            var skinIdx = Array.IndexOf(e.Args, "--skin");
            if (skinIdx >= 0 && skinIdx + 1 < e.Args.Length) _skinOverride = e.Args[skinIdx + 1];

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

            // Apply saved font size globally before opening main window
            Views.SettingsWindow.ApplyFontSize(Views.SettingsWindow.LoadFontSize(db));

            OpenMainWindow(db);
        }

        // Called by FirstRunWindow after a successful pair exchange.
        public MainWindow OpenMainWindow(AppDb db)
        {
            // Apply saved font size (may have just been set in SettingsWindow)
            Views.SettingsWindow.ApplyFontSize(Views.SettingsWindow.LoadFontSize(db));

            // Skin before the window is constructed. Applying it afterwards
            // works too -- every colour is a DynamicResource -- but doing it
            // first avoids a visible repaint on startup.
            //
            // --skin only overrides inside --screenshot. It exists so the
            // harness can capture a skin without touching the saved setting;
            // honouring it during a normal run would make a paid skin a
            // command-line flag away. Client-side cosmetics can never be fully
            // protected -- the XAML ships in the assembly either way -- but
            // that is no reason to build the bypass in.
            // A corp-imposed skin outranks the stored preference: the pilot
            // does not choose it, so an older personal choice must not win.
            var wanted = IsScreenshotMode && _skinOverride is not null
                ? _skinOverride
                : SkinEntitlements.LockedTo(db)
                  ?? SkinEntitlements.Resolve(db, SkinStore.Load(db));
            ThemeManager.Apply(wanted);

            var main = new MainWindow(db);

            if (IsScreenshotMode)
            {
                // Subscribe BEFORE Show() -- Show() can raise Loaded synchronously
                // for the first window, so attaching after Show() risks missing it
                // and hanging forever with no window to capture.
                main.Loaded += (_, __) =>
                {
                    // Let WPF finish layout and render before capturing.
                    main.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Resolve against the working directory explicitly. A bare
                            // filename lands wherever the caller happened to be -- which
                            // for a shell launched in Program Files is not writable.
                            var full = System.IO.Path.GetFullPath(_screenshotPath!);
                            var dir  = System.IO.Path.GetDirectoryName(full);
                            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

                            var rtb = new RenderTargetBitmap(
                                (int)main.ActualWidth, (int)main.ActualHeight,
                                96, 96, PixelFormats.Pbgra32);
                            rtb.Render(main);
                            var enc = new PngBitmapEncoder();
                            enc.Frames.Add(BitmapFrame.Create(rtb));
                            using (var fs = System.IO.File.Create(full)) enc.Save(fs);
                            Shutdown();
                        }
                        catch (Exception ex)
                        {
                            // This is a headless capture tool. NEVER let a failure reach
                            // the app's fatal-error dialog -- a scripted run would hang
                            // on a modal box, and a human sees a crash from a feature
                            // they never invoked. Print and exit non-zero instead.
                            Console.Error.WriteLine($"--screenshot failed: {ex.Message}");
                            Environment.Exit(2);
                        }
                    }), DispatcherPriority.ContextIdle);
                };
            }

            main.Show();
            return main;
        }

        // ── Exception surfaces ───────────────────────────────────────────
        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
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

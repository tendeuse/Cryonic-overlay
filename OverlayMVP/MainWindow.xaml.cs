// filename: MainWindow.xaml.cs
using System;
using System.Windows;
using OverlayMVP.Services;
using OverlayMVP.ViewModels;

namespace OverlayMVP
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel  _vm;
        private readonly HotkeyManager _hotkeys;
        private          IntPtr         _hwnd;
        private          bool           _visible = true;

        public MainWindow(AppDb db)
        {
            InitializeComponent();

            // Load config and wire up ViewModel
            var cfg = OverlayConfig.Load(db)
                      ?? throw new InvalidOperationException("Config missing — run first-run wizard.");

            _vm          = new MainViewModel(db, cfg);
            DataContext  = _vm;

            // Set up hotkeys and click-through once the window handle exists
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = ClickThrough.GetHwnd(this);

            // Wire global hotkeys
            _hotkeys = new HotkeyManager(this);

            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_VISIBILITY, ToggleVisibility);
            _hotkeys.SetHandler(HotkeyManager.ID_TOGGLE_CLICKTHROUGH, ToggleClickThrough);
            _hotkeys.SetHandler(HotkeyManager.ID_REPORT_INTEL,  () => _vm.ReportRoamingCommand.Execute(null));
            _hotkeys.SetHandler(HotkeyManager.ID_REPORT_CLEAR,  () => _vm.ReportClearCommand.Execute(null));

            // Start background polling
            _vm.StartPolling();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _hotkeys?.Dispose();
            _vm.Dispose();
        }

        // ----------------------------------------------------------------
        // Title bar drag
        // ----------------------------------------------------------------
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        // ----------------------------------------------------------------
        // Close button
        // ----------------------------------------------------------------
        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        // ----------------------------------------------------------------
        // Hotkey handlers
        // ----------------------------------------------------------------
        private void ToggleVisibility()
        {
            _visible = !_visible;
            Dispatcher.Invoke(() => Opacity = _visible ? 1.0 : 0.0);
        }

        private void ToggleClickThrough()
        {
            bool nowInteractive = ClickThrough.Toggle(_hwnd);
            // nowInteractive = true means click-through is OFF (user can click overlay)
            // nowInteractive = false means click-through is ON (clicks pass through)
            Dispatcher.Invoke(() => _vm.ToggleClickThrough(!nowInteractive));
        }
    }
}

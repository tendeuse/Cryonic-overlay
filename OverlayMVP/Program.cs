// filename: Program.cs
//
// FIX: WPF's software renderer (forced by AllowsTransparency="True") requires
// more stack than the default 1 MB CLR thread provides. The native rendering
// pipeline (milcore) overflows during the first Measure/Arrange/Render pass on
// complex XAML, producing 0x800703e9 (ERROR_STACK_OVERFLOW) before any managed
// exception handler can fire.
//
// Solution: launch the WPF dispatcher on a new STA thread with a 32 MB stack.
// The auto-generated entry point is suppressed in the .csproj.

using System;
using System.Threading;
using System.Windows;

namespace OverlayMVP
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // 32 MB stack — gives the WPF software renderer plenty of headroom.
            var uiThread = new Thread(RunApp, 32 * 1024 * 1024);
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Name = "WPF UI Thread";
            uiThread.Start();
            uiThread.Join();
        }

        private static void RunApp()
        {
            var app = new App();
            app.InitializeComponent();   // load App.xaml BAML → populates Application.Resources
            app.Run();
        }
    }
}

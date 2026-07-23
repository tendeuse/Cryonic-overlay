// filename: Views/StandingGuideWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class StandingGuideWindow : Window
    {
        public StandingGuideWindow() => InitializeComponent();

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowResizeHelper.Enable(this);
        }

        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        private void CloseBtn_Click(object s, RoutedEventArgs e) => Close();

        private void ManualStandingSet_Click(object s, RoutedEventArgs e)
        {
            if (FindName("ManualStandingBox") is System.Windows.Controls.TextBox tb &&
                DataContext is OverlayMVP.ViewModels.MainViewModel vm)
            {
                vm.SetManualStandingCommand.Execute(tb.Text);
            }
        }
    }
}

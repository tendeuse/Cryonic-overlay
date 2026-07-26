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

        // The faction ListBox has its own (disabled) ScrollViewer which still marks wheel
        // events handled — forward them to the parent so the whole panel keeps scrolling.
        private void FactionList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
            var parent = ((System.Windows.FrameworkElement)sender).Parent as System.Windows.UIElement;
            parent?.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = System.Windows.UIElement.MouseWheelEvent,
                Source      = sender,
            });
        }
    }
}

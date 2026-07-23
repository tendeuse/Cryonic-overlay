// filename: Views/MissionProgressWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class MissionProgressWindow : Window
    {
        public MissionProgressWindow() => InitializeComponent();

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
    }
}

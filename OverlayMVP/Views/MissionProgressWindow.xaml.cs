// filename: Views/MissionProgressWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using OverlayMVP.Services;

namespace OverlayMVP.Views
{
    public partial class MissionProgressWindow : Window
    {
        /// <summary>Translations for {Binding Loc.X}. This window had no
        /// DataContext, so every string in it was a hardcoded literal.</summary>
        public LocalizationManager Loc => LocalizationManager.Instance;

        public MissionProgressWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

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

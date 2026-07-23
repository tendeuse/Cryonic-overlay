// filename: Views/OrdersWindow.xaml.cs
using System.Windows;
using System.Windows.Input;

namespace OverlayMVP.Views
{
    public partial class OrdersWindow : Window
    {
        public OrdersWindow() => InitializeComponent();

        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        private void CloseBtn_Click(object s, RoutedEventArgs e) => Close();
    }
}

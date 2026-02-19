// filename: MainWindow.xaml.cs
using System.Windows;
using OverlayMVP.Services;

namespace OverlayMVP
{
    public partial class MainWindow : Window
    {
        private readonly AppDb _db;

        public MainWindow(AppDb db)
        {
            InitializeComponent();
            _db = db;
            // you can set a ViewModel later; for now it will just show UI
        }
    }
}

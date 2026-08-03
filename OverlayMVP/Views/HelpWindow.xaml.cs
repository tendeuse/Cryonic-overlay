// filename: Views/HelpWindow.xaml.cs
using System.Windows;
using System.Windows.Input;

namespace OverlayMVP.Views
{
    /// <summary>
    /// In-app help: hotkeys, what each panel does, and how to get started.
    ///
    /// Takes MainWindow's view-model as its DataContext rather than building
    /// one, purely so `Loc` resolves — the help text follows the language
    /// toggle live, like every other string in the app.
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // The window is WindowStyle=None, so dragging is ours to implement.
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}

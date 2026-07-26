// filename: Views/NotePromptWindow.xaml.cs
using System.Windows;

namespace OverlayMVP.Views
{
    /// <summary>
    /// Small modal asking the pilot for an optional completion note. Native WPF so the
    /// overlay takes no extra dependency (Microsoft.VisualBasic's InputBox is not referenced).
    /// </summary>
    public partial class NotePromptWindow : Window
    {
        public string OrderTitle { get; }
        public string Note { get; private set; } = "";

        public NotePromptWindow(string orderTitle)
        {
            OrderTitle = orderTitle;
            InitializeComponent();
            Loaded += (_, _) => NoteBox.Focus();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Note = NoteBox.Text ?? "";
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>Shows the prompt. Returns the note, or null if the pilot cancelled.</summary>
        public static string? Prompt(string orderTitle)
        {
            var win = new NotePromptWindow(orderTitle);
            return win.ShowDialog() == true ? win.Note : null;
        }
    }
}

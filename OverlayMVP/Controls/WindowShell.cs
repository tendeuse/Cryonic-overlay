// filename: Controls/WindowShell.cs
using System.Windows;

namespace OverlayMVP.Controls
{
    /// <summary>
    /// The overlay's outer frame. Exists so a skin can add corner bolts, plate
    /// texture and outer decoration without the window's markup changing.
    /// </summary>
    public class WindowShell : ChromeControl
    {
        static WindowShell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(WindowShell), new FrameworkPropertyMetadata(typeof(WindowShell)));
        }
    }
}

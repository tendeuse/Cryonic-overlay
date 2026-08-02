// filename: Controls/TitleBar.cs
using System.Windows;

namespace OverlayMVP.Controls
{
    /// <summary>
    /// The overlay's title bar frame. Exists so a skin can supply the brand
    /// strip and window-button treatment. The bar's CONTENTS stay in the
    /// window -- they are content, not chrome.
    /// </summary>
    public class TitleBar : ChromeControl
    {
        static TitleBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(TitleBar), new FrameworkPropertyMetadata(typeof(TitleBar)));
        }
    }
}

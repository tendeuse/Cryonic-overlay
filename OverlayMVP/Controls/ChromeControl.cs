// filename: Controls/ChromeControl.cs
using System.Windows;
using System.Windows.Controls;

namespace OverlayMVP.Controls
{
    /// <summary>
    /// Base for the overlay's chrome wrappers. Adds the CornerRadius that
    /// ContentControl lacks; everything else (Background, BorderBrush,
    /// BorderThickness, Padding) is inherited and TemplateBound by the skins.
    ///
    /// SectionPanel does NOT derive from this -- it needs HeaderedContentControl
    /// for its Header slot, which is a sibling branch of the hierarchy, so it
    /// declares its own CornerRadius.
    /// </summary>
    public abstract class ChromeControl : ContentControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(ChromeControl));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

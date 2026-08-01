// filename: Controls/SectionPanel.cs
using System.Windows;
using System.Windows.Controls;

namespace OverlayMVP.Controls
{
    /// <summary>
    /// A titled section of the overlay. Exists so a skin can supply the section's
    /// chrome -- eyebrow label, rules, texture -- without any window's markup
    /// changing.
    ///
    /// THREE slots, not two. Today's intel and pilot-status sections put a label
    /// on the left and a trailing element on the right (an intel status string, a
    /// refresh button), and the cockpit design does the same. Folding that into a
    /// single Header would hand the skin an opaque blob it cannot style.
    /// </summary>
    public class SectionPanel : HeaderedContentControl
    {
        static SectionPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SectionPanel), new FrameworkPropertyMetadata(typeof(SectionPanel)));
        }

        /// <summary>Optional trailing content on the header row.</summary>
        public static readonly DependencyProperty HeaderAccessoryProperty =
            DependencyProperty.Register(nameof(HeaderAccessory), typeof(object), typeof(SectionPanel));

        public object HeaderAccessory
        {
            get => GetValue(HeaderAccessoryProperty);
            set => SetValue(HeaderAccessoryProperty, value);
        }
    }
}

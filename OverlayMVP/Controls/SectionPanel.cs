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

        /// <summary>
        /// Corner rounding for the template's root Border. HeaderedContentControl has
        /// no CornerRadius of its own, so this exists purely to give the footer (which
        /// needs 0,0,8,8) somewhere to put it. Defaults to 0 so rows 2/3/4, which never
        /// set it, render unchanged.
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(SectionPanel),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

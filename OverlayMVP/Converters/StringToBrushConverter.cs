using OverlayMVP.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

// filename: Converters/StringToBrushConverter.cs
// Converts a hex color string (e.g. "#00FF80") to a WPF SolidColorBrush.
// Used so model properties can return simple strings without a WPF dependency.

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OverlayMVP.Converters
{
    public sealed class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

// ── NullToVisibilityConverter ─────────────────────────────────────────────
// null → Collapsed, non-null → Visible
namespace OverlayMVP.Converters
{
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── ZeroToCollapsedConverter ───────────────────────────────────────────
    // 0 (int) → Collapsed, anything else → Visible
    public sealed class ZeroToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i == 0 ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── StepStatusEmojiConverter ───────────────────────────────────────────
    // ConverterParameter = TutorialStep, value = float standing
    // Returns: 🔒 / ▶ / ✅
    public sealed class StepStatusEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not float standing || parameter is not TutorialStep step)
                return "▶";
            if (!step.IsUnlocked(standing)) return "🔒";
            if (step.IsCompleted(standing)) return "✅";
            return "▶";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── ProgressWidthConverter ─────────────────────────────────────────────
    // ConverterParameter = TutorialStep, value = float standing
    // Returns double width (0–440px = full panel width)
    public sealed class ProgressWidthConverter : IValueConverter
    {
        private const double MaxWidth = 420.0;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not float standing || parameter is not TutorialStep step)
                return 0.0;
            return (double)step.Progress(standing) * MaxWidth;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

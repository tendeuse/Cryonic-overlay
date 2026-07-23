// filename: Converters/StringToBrushConverter.cs
using OverlayMVP.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OverlayMVP.Converters
{
    // Converts a hex color string (e.g. "#00FF80") to a WPF SolidColorBrush.
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

    // null → Collapsed, non-null → Visible
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // 0 (int) → Collapsed, anything else → Visible
    public sealed class ZeroToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i == 0 ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → Visibility (True=Visible, False=Collapsed)
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    // MultiBinding: values[0]=float standing, values[1]=TutorialStep → 🔒 / ▶ / ✅
    public sealed class StepStatusEmojiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return "▶";
            var standing = values[0] is float f ? f : values[0] is double d ? (float)d : 0f;
            if (values[1] is not TutorialStep step) return "▶";
            if (!step.IsUnlocked(standing)) return "🔒";
            if (step.IsCompleted(standing))  return "✅";
            return "▶";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // MultiBinding: values[0]=float standing, values[1]=TutorialStep → double 0.0–1.0
    public sealed class StepProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 0.0;
            var standing = values[0] is float f ? f : values[0] is double d ? (float)d : 0f;
            if (values[1] is not TutorialStep step) return 0.0;
            return (double)step.Progress(standing);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Converts GlobalFontSize + offset for relative sizes (e.g. -2 for small, +2 for large)
    public sealed class RelativeFontSizeConverter : IValueConverter
    {
        public double Offset { get; set; } = 0;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double base_ = value is double d ? d : 11.0;
            double off   = parameter is string s && double.TryParse(s,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : Offset;
            return Math.Max(6, base_ + off);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → Visibility inverted (False=Visible, True=Collapsed)
    public sealed class InvertBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Collapsed;
    }

    // Intel status colour — red/orange when alerts active, dim when clear
    public sealed class IntelStatusColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true
                ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))  // #EF5350 red
                : new SolidColorBrush(Color.FromRgb(0x44, 0x55, 0x66)); // dim
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → FontWeight: true = Bold (alerts), false = Normal
    public sealed class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // MultiBinding: values[0]=float standing, values[1]=TutorialStep
    // Returns true when the step is the active one (unlocked but not yet completed)
    public sealed class StepIsActiveConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var standing = values[0] is float f ? f : values[0] is double d ? (float)d : 0f;
            if (values[1] is not TutorialStep step) return false;
            return step.IsUnlocked(standing) && !step.IsCompleted(standing);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → threshold status: true=unlocked ✅, false=locked ○
    public sealed class ThresholdStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "✅" : "○";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → kill role icon: true=attacker (⚔), false=victim (💀)
    public sealed class KillRoleIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "⚔" : "💀";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // bool → brush: true = ESI linked (green), false = not linked (dim)
    public sealed class BoolToEsiColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))   // green
                : new SolidColorBrush(Color.FromRgb(0x44, 0x55, 0x66));  // dim
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

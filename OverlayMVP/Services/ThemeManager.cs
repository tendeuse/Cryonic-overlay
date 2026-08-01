// filename: Services/ThemeManager.cs
using System;
using System.Linq;
using System.Windows;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Swaps the merged token dictionary at runtime.
    ///
    /// Only the TOKEN dictionary is swapped; styles stay put, because the
    /// cockpit skins share one component layer and differ only in colour.
    ///
    /// This works only because every colour is consumed via DynamicResource.
    /// A StaticResource is resolved once at window load and will NOT follow a
    /// theme change — which is why the refactor converts all of them.
    /// </summary>
    public static class ThemeManager
    {
        public const string DefaultTheme = "Default";

        public static string Current { get; private set; } = DefaultTheme;

        /// <summary>Apply a theme by name, e.g. "Default". No-op if already applied.</summary>
        public static void Apply(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName)) return;
            if (themeName == Current) return;

            ResourceDictionary dict;
            try
            {
                dict = new ResourceDictionary
                {
                    Source = new Uri($"Themes/Tokens.{themeName}.xaml", UriKind.Relative),
                };
            }
            catch { return; }   // unknown theme: keep the current one rather than crashing

            var merged = Application.Current.Resources.MergedDictionaries;
            // The token dictionary is identified by the brush keys it carries, not by
            // position — Styles.Default.xaml is also merged and must not be replaced.
            var existing = merged.FirstOrDefault((d) => d.Contains("Bg") && d.Contains("TextDim"));
            if (existing is not null) merged.Remove(existing);
            merged.Insert(0, dict);

            Current = themeName;
        }
    }
}

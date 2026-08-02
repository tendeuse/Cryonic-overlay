// filename: Services/ThemeManager.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Swaps the skin at runtime.
    ///
    /// A SKIN IS A PAIR: a token dictionary (colours) and a styles dictionary
    /// (the component layer). Both are swapped together. An earlier version
    /// swapped only tokens, on the assumption that skins differ by colour
    /// alone — the cockpit skins disprove that. They draw key bevels, glass
    /// readouts and hazard striping the default component layer has no concept
    /// of, so the styles must travel with the colours.
    ///
    /// A skin may still share the default component layer by naming
    /// Styles = "Default"; a pure recolour then costs one token file.
    ///
    /// This works only because every colour is consumed via DynamicResource. A
    /// StaticResource is resolved once at window load and will NOT follow a
    /// skin change.
    /// </summary>
    public static class ThemeManager
    {
        public const string DefaultTheme = "Default";

        /// <summary>
        /// A skin: which token file, and which component layer it draws with.
        ///
        /// Registered here rather than discovered from disk. The XAML is
        /// compiled into the assembly as a resource, so there is no directory
        /// to enumerate at runtime — and a name that is not listed could not be
        /// loaded anyway.
        /// </summary>
        public sealed record Skin(string Id, string Display, string Tokens, string Styles, bool Paid);

        public static readonly IReadOnlyList<Skin> Available = new[]
        {
            new Skin("Default",     "Default",      "Default",     "Default", Paid: false),
            new Skin("CaldariNavy", "Caldari Navy", "CaldariNavy", "Cockpit", Paid: true),
        };

        public static Skin Find(string id) =>
            Available.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Available[0];

        public static string Current { get; private set; } = DefaultTheme;

        /// <summary>
        /// Apply a skin by id. Unknown ids and load failures fall back to
        /// whatever is already on screen rather than throwing: a skin is
        /// cosmetic, and a broken one must never stop the overlay opening.
        /// Returns whether the skin is now applied.
        /// </summary>
        public static bool Apply(string skinId)
        {
            if (string.IsNullOrWhiteSpace(skinId)) return false;
            var skin = Find(skinId);
            if (skin.Id == Current) return true;

            ResourceDictionary tokens, styles;
            try
            {
                tokens = Load($"Themes/Tokens.{skin.Tokens}.xaml");
                styles = Load($"Themes/Styles.{skin.Styles}.xaml");
            }
            catch
            {
                // Load BOTH before swapping EITHER. A half-applied pair leaves
                // styles referencing token keys that are not there, and WPF
                // renders a missing DynamicResource as nothing at all — no
                // exception, just invisible controls.
                return false;
            }

            var merged = Application.Current.Resources.MergedDictionaries;
            Replace(merged, RoleTokens, tokens);
            Replace(merged, RoleStyles, styles);

            Current = skin.Id;
            return true;
        }

        private const string RoleKey    = "__SkinDictionaryRole";
        private const string RoleTokens = "Tokens";
        private const string RoleStyles = "Styles";

        private static ResourceDictionary Load(string relativePath) =>
            new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };

        /// <summary>
        /// Replace the dictionary playing a given role, in place.
        ///
        /// Each theme dictionary declares its own role in a well-known key. The
        /// previous version sniffed for brush keys it expected to find ("does
        /// it contain Bg and TextDim?"), which holds only while every skin
        /// carries the same token names — and skins that add their own tokens
        /// are precisely what this exists to support.
        ///
        /// Replacing in place preserves order, which matters: WPF merge is
        /// last-wins, so styles must stay after tokens.
        /// </summary>
        private static void Replace(Collection<ResourceDictionary> merged, string role, ResourceDictionary next)
        {
            for (var i = 0; i < merged.Count; i++)
            {
                if (merged[i][RoleKey] as string == role) { merged[i] = next; return; }
            }

            // Nothing claims the role. Append so the skin still applies rather
            // than silently doing nothing.
            merged.Add(next);
        }
    }
}

// filename: Services/SkinEntitlements.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Which skins this pilot may use.
    ///
    /// Entitlements come from the backend, but are read from a LOCAL CACHE.
    /// The overlay has to work with the network down, mid-fight, or when the
    /// API is having a bad day — and losing a skin you paid for because a
    /// request timed out is far worse than briefly keeping one you no longer
    /// own. So a failed refresh changes nothing; only a successful response
    /// that omits a skin removes it.
    ///
    /// Free skins are never gated. That is not a convenience: CCP's licence
    /// forbids paywalling FEATURES (§4.2), so the overlay must stay fully
    /// usable on the default skin forever. Only cosmetics are ever gated, and
    /// the default is always one of them.
    /// </summary>
    public static class SkinEntitlements
    {
        private const string CacheKey  = "ui_skins_owned";      // comma-separated ids
        private const string OverrideKey = "ui_skin_dev_unlock"; // local, developer-only

        public static bool IsEntitled(AppDb db, ThemeManager.Skin skin)
        {
            if (!skin.Paid) return true;
            if (DevUnlocked(db)) return true;
            return Cached(db).Contains(skin.Id, StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<ThemeManager.Skin> Available(AppDb db) =>
            ThemeManager.Available.Where(s => IsEntitled(db, s)).ToList();

        /// <summary>
        /// Fall back to a skin the pilot may actually use.
        ///
        /// The chosen skin is stored locally, so without this a revoked grant
        /// would keep rendering a skin they are no longer entitled to.
        /// </summary>
        public static string Resolve(AppDb db, string requestedId)
        {
            var skin = ThemeManager.Find(requestedId);
            return IsEntitled(db, skin) ? skin.Id : ThemeManager.DefaultTheme;
        }

        /// <summary>
        /// Refresh from the backend. Safe to call and forget: a null answer
        /// means "could not tell", and the cache is left exactly as it was.
        /// Returns whether anything actually changed.
        /// </summary>
        public static async Task<bool> RefreshAsync(AppDb db, OverlayApiClient api)
        {
            List<string>? owned;
            try { owned = await api.GetMySkinsAsync(); }
            catch { return false; }

            // null is UNKNOWN, not "owns nothing". Do not write it.
            if (owned is null) return false;

            var next = string.Join(",", owned.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            if (next == string.Join(",", Cached(db))) return false;

            Write(db, next);
            return true;
        }

        // ── storage ───────────────────────────────────────────────────────

        private static string[] Cached(AppDb db)
        {
            var raw = Read(db, CacheKey);
            return string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static bool DevUnlocked(AppDb db) => Read(db, OverrideKey) == "1";

        private static string? Read(AppDb db, string key)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", key);
                return cmd.ExecuteScalar() as string;
            }
            catch { return null; }
        }

        private static void Write(AppDb db, string value)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) " +
                                  "ON CONFLICT(k) DO UPDATE SET v=excluded.v";
                cmd.Parameters.AddWithValue("$k", CacheKey);
                cmd.Parameters.AddWithValue("$v", value);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}

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
        private const string CacheKey    = "ui_skins_owned";     // comma-separated ids
        private const string LockedKey   = "ui_skin_locked_to";  // corp-imposed skin, or empty
        private const string ExpiresKey  = "ui_skins_expires_at"; // unix seconds, or empty
        private const string OverrideKey = "ui_skin_dev_unlock";  // local, developer-only

        public static bool IsEntitled(AppDb db, ThemeManager.Skin skin)
        {
            if (!skin.Paid) return true;
            if (DevUnlocked(db)) return true;
            if (Expired(db)) return false;
            return Cached(db).Contains(skin.Id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The skin the pilot's CORP has chosen, which they do not get to
        /// change. Null when they choose for themselves.
        ///
        /// This is the $15 corp perk: the CEO picks one skin and the members
        /// wear it. A member who sponsors personally is never locked, because
        /// what they bought is the choice.
        /// </summary>
        public static string? LockedTo(AppDb db)
        {
            if (DevUnlocked(db)) return null;   // never fight a developer override
            if (Expired(db)) return null;
            var v = Read(db, LockedKey);
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        /// <summary>
        /// Has the cached entitlement gone stale?
        ///
        /// The cache exists so the overlay works offline, but "works offline"
        /// must not mean "a cancelled subscription lasts forever". The backend
        /// sends an expiry that already includes its grace window; past it, the
        /// pilot falls back to the free skins until a refresh succeeds.
        /// </summary>
        private static bool Expired(AppDb db)
        {
            var raw = Read(db, ExpiresKey);
            // No expiry recorded means the entitlement did not come from a
            // subscription -- a manual grant, say -- and does not lapse.
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return !long.TryParse(raw, out var at) ||
                   DateTimeOffset.UtcNow.ToUnixTimeSeconds() > at;
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
            OverlayApiClient.SkinEntitlementDto? ent;
            try { ent = await api.GetMySkinsAsync(); }
            catch { return false; }

            // null is UNKNOWN, not "entitled to nothing". Do not write it --
            // that is what stops a dropped connection revoking a paid skin.
            if (ent is null) return false;

            var next    = string.Join(",", ent.Skins.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            var locked  = ent.LockedTo ?? "";
            var expires = ent.ExpiresAt?.ToString() ?? "";

            var changed = next != string.Join(",", Cached(db))
                       || locked  != (Read(db, LockedKey)  ?? "")
                       || expires != (Read(db, ExpiresKey) ?? "");

            Write(db, CacheKey,   next);
            Write(db, LockedKey,  locked);
            Write(db, ExpiresKey, expires);
            return changed;
        }

        // ── storage ───────────────────────────────────────────────────────

        private static string[] Cached(AppDb db)
        {
            var raw = Read(db, CacheKey);
            return string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>
        /// Local developer unlock. COMPILED OUT OF RELEASE BUILDS.
        ///
        /// It exists so paid skins can be exercised before a real entitlement
        /// can be granted. In a shipped build it would be a backdoor: the value
        /// lives in the user's own SQLite file, so anyone willing to open it
        /// could unlock every paid skin by typing "1" into a row. That is fine
        /// for a dev convenience and not fine for something people pay for.
        ///
        /// #if DEBUG rather than a config flag on purpose — a flag can be
        /// turned on in the field, and the compiler removing the code is the
        /// only version that cannot.
        /// </summary>
        private static bool DevUnlocked(AppDb db)
        {
#if DEBUG
            return Read(db, OverrideKey) == "1";
#else
            return false;
#endif
        }

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

        private static void Write(AppDb db, string key, string value)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) " +
                                  "ON CONFLICT(k) DO UPDATE SET v=excluded.v";
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$v", value);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}

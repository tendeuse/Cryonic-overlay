// filename: Services/SkinEntitlements.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Which skins this pilot may use.
    ///
    /// THIS IS A SEAM, DELIBERATELY STUBBED. Real entitlements arrive with the
    /// backend slice, where a purchased skin is granted per player and managed
    /// by an admin from the control panel. Only <see cref="IsEntitled"/> changes
    /// then; every caller stays as it is.
    ///
    /// It is stubbed rather than left out because the alternative -- shipping
    /// the paid skin unlocked and gating it later -- means taking something
    /// away from pilots who already had it.
    ///
    /// Free skins are always allowed. That is not a placeholder: CCP's licence
    /// forbids paywalling FEATURES (§4.2), so the overlay must stay fully usable
    /// on the default skin forever. Only cosmetics are ever gated.
    /// </summary>
    public static class SkinEntitlements
    {
        /// <summary>
        /// Local unlock for development. A pilot cannot set this from the UI --
        /// it exists so the paid skin can be exercised before the backend can
        /// grant it, not as a way to bypass paying.
        /// </summary>
        private const string OverrideKey = "ui_skin_dev_unlock";

        public static bool IsEntitled(AppDb db, ThemeManager.Skin skin)
        {
            if (!skin.Paid) return true;
            return DevUnlocked(db);
        }

        public static IReadOnlyList<ThemeManager.Skin> Available(AppDb db) =>
            ThemeManager.Available.Where(s => IsEntitled(db, s)).ToList();

        private static bool DevUnlocked(AppDb db)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", OverrideKey);
                return (cmd.ExecuteScalar() as string) == "1";
            }
            catch { return false; }
        }

        /// <summary>
        /// Fall back to a skin the pilot may actually use.
        ///
        /// Entitlement can be lost after a skin was chosen -- a grant is
        /// revoked, or a dev unlock is cleared. Without this the overlay would
        /// keep rendering a skin the pilot is no longer entitled to, since the
        /// choice is stored locally.
        /// </summary>
        public static string Resolve(AppDb db, string requestedId)
        {
            var skin = ThemeManager.Find(requestedId);
            return IsEntitled(db, skin) ? skin.Id : ThemeManager.DefaultTheme;
        }
    }
}

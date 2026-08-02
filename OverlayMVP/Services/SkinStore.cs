// filename: Services/SkinStore.cs
using System;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Which skin the pilot chose, in the meta table alongside the font size.
    ///
    /// Reads never throw. A skin is cosmetic: a database that cannot be read
    /// must degrade to the default look, not stop the overlay from opening.
    /// </summary>
    public static class SkinStore
    {
        private const string Key = "ui_skin";

        public static string Load(AppDb db)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", Key);
                var raw = cmd.ExecuteScalar() as string;
                if (!string.IsNullOrWhiteSpace(raw)) return raw;
            }
            catch { }
            return ThemeManager.DefaultTheme;
        }

        public static void Save(AppDb db, string skinId)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) " +
                                  "ON CONFLICT(k) DO UPDATE SET v=excluded.v";
                cmd.Parameters.AddWithValue("$k", Key);
                cmd.Parameters.AddWithValue("$v", skinId);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}

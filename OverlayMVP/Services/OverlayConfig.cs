// filename: Services/OverlayConfig.cs
using Microsoft.Data.Sqlite;

namespace OverlayMVP.Services
{
    public sealed class OverlayConfig
    {
        public string ApiBaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string AlphaOmega { get; set; } = "ALPHA";
        public string FactionFocus { get; set; } = "CALDARI";

        public static OverlayConfig? Load(AppDb db)
        {
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT api_base_url, api_key, alpha_omega, faction_focus FROM overlay_config WHERE id=1";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new OverlayConfig
            {
                ApiBaseUrl = r.GetString(0),
                ApiKey = r.GetString(1),
                AlphaOmega = r.GetString(2),
                FactionFocus = r.GetString(3)
            };
        }

        public void Save(AppDb db)
        {
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO overlay_config(id, api_base_url, api_key, alpha_omega, faction_focus)
VALUES(1, $url, $key, $ao, $ff)
ON CONFLICT(id) DO UPDATE SET
  api_base_url=excluded.api_base_url,
  api_key=excluded.api_key,
  alpha_omega=excluded.alpha_omega,
  faction_focus=excluded.faction_focus
";
            cmd.Parameters.AddWithValue("$url", ApiBaseUrl.Trim().TrimEnd('/'));
            cmd.Parameters.AddWithValue("$key", ApiKey.Trim());
            cmd.Parameters.AddWithValue("$ao", AlphaOmega.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("$ff", FactionFocus.Trim().ToUpperInvariant());
            cmd.ExecuteNonQuery();
        }
    }
}

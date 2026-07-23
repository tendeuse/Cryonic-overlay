// filename: Services/AppDb.cs
using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OverlayMVP.Services
{
    public sealed class AppDb
    {
        public string DbPath { get; }

        public AppDb(string dbPath)
        {
            DbPath = dbPath;
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        }

        public static string DefaultPath()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EveMissionOverlay");
            return Path.Combine(root, "overlay.db");
        }

        public SqliteConnection Open()
        {
            var con = new SqliteConnection($"Data Source={DbPath}");
            con.Open();
            return con;
        }

        public void EnsureSchema()
        {
            using var con = Open();
            using var cmd = con.CreateCommand();

            // Fresh schema (token-based)
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS overlay_config(
  id INTEGER PRIMARY KEY CHECK (id=1),
  api_base_url TEXT NOT NULL,
  overlay_token TEXT NOT NULL,
  alpha_omega TEXT NOT NULL,
  faction_focus TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS meta(
  k TEXT PRIMARY KEY,
  v TEXT NOT NULL
);
";
            cmd.ExecuteNonQuery();

            // Migration: if an older table exists with api_key, migrate it into overlay_token
            // This is safe even if it doesn't exist.
            try
            {
                using var mig = con.CreateCommand();
                mig.CommandText = @"
PRAGMA table_info(overlay_config);
";
                using var r = mig.ExecuteReader();
                bool hasApiKey = false;
                while (r.Read())
                {
                    var name = r.GetString(1);
                    if (string.Equals(name, "api_key", StringComparison.OrdinalIgnoreCase))
                        hasApiKey = true;
                }

                if (hasApiKey)
                {
                    // Create new table, copy, swap
                    using var tx = con.BeginTransaction();

                    using var c1 = con.CreateCommand();
                    c1.Transaction = tx;
                    c1.CommandText = @"
CREATE TABLE IF NOT EXISTS overlay_config_new(
  id INTEGER PRIMARY KEY CHECK (id=1),
  api_base_url TEXT NOT NULL,
  overlay_token TEXT NOT NULL,
  alpha_omega TEXT NOT NULL,
  faction_focus TEXT NOT NULL
);";
                    c1.ExecuteNonQuery();

                    using var c2 = con.CreateCommand();
                    c2.Transaction = tx;
                    c2.CommandText = @"
INSERT INTO overlay_config_new(id, api_base_url, overlay_token, alpha_omega, faction_focus)
SELECT id, api_base_url, api_key, alpha_omega, faction_focus
FROM overlay_config
WHERE id=1;
";
                    c2.ExecuteNonQuery();

                    using var c3 = con.CreateCommand();
                    c3.Transaction = tx;
                    c3.CommandText = "DROP TABLE overlay_config;";
                    c3.ExecuteNonQuery();

                    using var c4 = con.CreateCommand();
                    c4.Transaction = tx;
                    c4.CommandText = "ALTER TABLE overlay_config_new RENAME TO overlay_config;";
                    c4.ExecuteNonQuery();

                    tx.Commit();
                }
            }
            catch
            {
                // If migration fails, leave schema as-is; the wizard will rewrite config.
            }

            // Migration: add feature-toggle columns if they don't exist yet
            var featureCols = new[]
            {
                "show_multibox",
                "show_intel_alerts",
                "show_pilot_status",
                "show_active_missions",
                "show_standing_guide",
                "show_mission_progress",
                "show_skill_plan",
                "show_pilot_intel_btn",
                "show_system_info_btn",
            };
            foreach (var col in featureCols)
            {
                try
                {
                    using var altCmd = con.CreateCommand();
                    altCmd.CommandText = $"ALTER TABLE overlay_config ADD COLUMN {col} INTEGER NOT NULL DEFAULT 1";
                    altCmd.ExecuteNonQuery();
                }
                catch
                {
                    // Column already exists — ignore.
                }
            }
        }
    }
}

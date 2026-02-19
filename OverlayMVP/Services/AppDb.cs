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
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS overlay_config(
  id INTEGER PRIMARY KEY CHECK (id=1),
  api_base_url TEXT NOT NULL,
  api_key TEXT NOT NULL,
  alpha_omega TEXT NOT NULL,
  faction_focus TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS meta(
  k TEXT PRIMARY KEY,
  v TEXT NOT NULL
);
";
            cmd.ExecuteNonQuery();
        }
    }
}

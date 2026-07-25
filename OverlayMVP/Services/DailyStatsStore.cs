// filename: Services/DailyStatsStore.cs
using System;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace OverlayMVP.Services
{
    /// <summary>EVE's day boundary is downtime at 11:00 UTC — not midnight.</summary>
    public static class EveDay
    {
        public const int ResetHourUtc = 11;

        /// <summary>Day key ("yyyy-MM-dd") of the most recent 11:00 UTC boundary.</summary>
        public static string KeyFor(DateTime utcNow)
        {
            var d = utcNow.Hour < ResetHourUtc ? utcNow.Date.AddDays(-1) : utcNow.Date;
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>The next 11:00 UTC boundary after <paramref name="utcNow"/>.</summary>
        public static DateTime NextResetUtc(DateTime utcNow)
        {
            var todayReset = utcNow.Date.AddHours(ResetHourUtc);
            return utcNow < todayReset ? todayReset : todayReset.AddDays(1);
        }
    }

    public sealed record DailyRow(
        string DayKey, int CharacterId,
        double WalletBaseline, long LpBaseline,
        double WalletLast, long LpLast,
        bool HasBaseline);

    /// <summary>All reads/writes for the daily_stats + daily_playtime tables.</summary>
    public sealed class DailyStatsStore
    {
        private readonly AppDb _db;
        public DailyStatsStore(AppDb db) { _db = db; }

        public DailyRow? Get(string dayKey, int characterId)
        {
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "SELECT wallet_baseline, lp_baseline, wallet_last, lp_last, has_baseline " +
                "FROM daily_stats WHERE day_key=$d AND character_id=$c";
            cmd.Parameters.AddWithValue("$d", dayKey);
            cmd.Parameters.AddWithValue("$c", characterId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new DailyRow(dayKey, characterId,
                r.GetDouble(0), r.GetInt64(1), r.GetDouble(2), r.GetInt64(3), r.GetInt32(4) != 0);
        }

        public void UpsertBaseline(string dayKey, int characterId, double wallet, long lp)
        {
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "INSERT INTO daily_stats(day_key,character_id,wallet_baseline,lp_baseline,wallet_last,lp_last,has_baseline) " +
                "VALUES($d,$c,$w,$l,$w,$l,1) " +
                "ON CONFLICT(day_key,character_id) DO UPDATE SET " +
                "wallet_baseline=$w, lp_baseline=$l, wallet_last=$w, lp_last=$l, has_baseline=1";
            cmd.Parameters.AddWithValue("$d", dayKey);
            cmd.Parameters.AddWithValue("$c", characterId);
            cmd.Parameters.AddWithValue("$w", wallet);
            cmd.Parameters.AddWithValue("$l", lp);
            cmd.ExecuteNonQuery();
        }

        public void UpsertLast(string dayKey, int characterId, double wallet, long lp)
        {
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "UPDATE daily_stats SET wallet_last=$w, lp_last=$l " +
                "WHERE day_key=$d AND character_id=$c";
            cmd.Parameters.AddWithValue("$d", dayKey);
            cmd.Parameters.AddWithValue("$c", characterId);
            cmd.Parameters.AddWithValue("$w", wallet);
            cmd.Parameters.AddWithValue("$l", lp);
            cmd.ExecuteNonQuery();
        }

        public long GetPlaytimeSeconds(string dayKey)
        {
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT seconds FROM daily_playtime WHERE day_key=$d";
            cmd.Parameters.AddWithValue("$d", dayKey);
            var v = cmd.ExecuteScalar();
            return v is null || v is DBNull ? 0 : Convert.ToInt64(v);
        }

        public void AddPlaytimeSeconds(string dayKey, long seconds)
        {
            if (seconds <= 0) return;
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "INSERT INTO daily_playtime(day_key,seconds) VALUES($d,$s) " +
                "ON CONFLICT(day_key) DO UPDATE SET seconds = seconds + $s";
            cmd.Parameters.AddWithValue("$d", dayKey);
            cmd.Parameters.AddWithValue("$s", seconds);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Keep the DB small — drop rows older than N days.</summary>
        public void PruneOlderThan(int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var con = _db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM daily_stats WHERE day_key < $c; DELETE FROM daily_playtime WHERE day_key < $c";
            cmd.Parameters.AddWithValue("$c", cutoff);
            cmd.ExecuteNonQuery();
        }
    }
}

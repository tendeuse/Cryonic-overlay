// filename: Services/SessionTracker.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayMVP.Services
{
    public sealed class OreLine
    {
        public string OreName  { get; set; } = "";
        public long   Units    { get; set; }
        public double PerHour  { get; set; }
        public string UnitsLabel   => Units.ToString("N0", CultureInfo.InvariantCulture);
        public string PerHourLabel => PerHour.ToString("N0", CultureInfo.InvariantCulture) + "/h";
    }

    /// <summary>
    /// Tracks today's activity (since 11:00 UTC downtime) summed across every linked
    /// character: net ISK, LP, kills, and ore mined — with per-hour rates driven by
    /// how long an EVE client has been running today.
    /// </summary>
    public sealed partial class SessionTracker : ObservableObject, IDisposable
    {
        private readonly AppDb            _db;
        private readonly EsiClient        _esi;
        private readonly DailyStatsStore  _store;
        private readonly ZkillClient      _zkill = new();
        private CancellationTokenSource?  _cts;

        private string _dayKey = EveDay.KeyFor(DateTime.UtcNow);

        [ObservableProperty] private double iskToday;
        [ObservableProperty] private long   lpToday;
        [ObservableProperty] private int    killsToday;
        [ObservableProperty] private double hoursPlayedToday;
        [ObservableProperty] private string relinkPrompt = "";
        [ObservableProperty] private string resetsInLabel = "";

        public ObservableCollection<OreLine> MinedToday { get; } = new();

        public double IskPerHour   => HoursPlayedToday > 0.01 ? IskToday   / HoursPlayedToday : 0;
        public double LpPerHour    => HoursPlayedToday > 0.01 ? LpToday    / HoursPlayedToday : 0;
        public double KillsPerHour => HoursPlayedToday > 0.01 ? KillsToday / HoursPlayedToday : 0;

        public string IskTodayLabel   => IskToday.ToString("N0", CultureInfo.InvariantCulture) + " ISK";
        public string IskPerHourLabel => IskPerHour.ToString("N0", CultureInfo.InvariantCulture) + " ISK/h";
        public string LpTodayLabel    => LpToday.ToString("N0", CultureInfo.InvariantCulture) + " LP";
        public string LpPerHourLabel  => LpPerHour.ToString("N0", CultureInfo.InvariantCulture) + " LP/h";
        public string KillsPerHourLabel => KillsPerHour.ToString("0.0", CultureInfo.InvariantCulture) + "/h";
        public string PlayedLabel
        {
            get
            {
                var t = TimeSpan.FromHours(HoursPlayedToday);
                return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            }
        }

        public SessionTracker(AppDb db, EsiClient esi)
        {
            _db    = db;
            _esi   = esi;
            _store = new DailyStatsStore(db);
            _store.PruneOlderThan(14);
            HoursPlayedToday = _store.GetPlaytimeSeconds(_dayKey) / 3600.0;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = LoopAsync(_cts.Token);
        }

        /// <summary>Called from the overlay's 5s instance tick while an EVE client is running.</summary>
        public void TickPlaytime(int seconds)
        {
            RollDayIfNeeded();
            _store.AddPlaytimeSeconds(_dayKey, seconds);
            HoursPlayedToday = _store.GetPlaytimeSeconds(_dayKey) / 3600.0;
            RaiseDerived();
        }

        private void RollDayIfNeeded()
        {
            var key = EveDay.KeyFor(DateTime.UtcNow);
            if (key == _dayKey) return;
            _dayKey          = key;                 // new EVE day → everything re-baselines
            IskToday         = 0;
            LpToday          = 0;
            KillsToday       = 0;
            HoursPlayedToday = 0;
            Application.Current?.Dispatcher.Invoke(() => MinedToday.Clear());
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            var lastMining = DateTime.MinValue;
            var lastKills  = DateTime.MinValue;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    RollDayIfNeeded();
                    await RefreshWalletAndLpAsync(ct);                       // every loop (3 min)
                    if ((DateTime.UtcNow - lastKills).TotalMinutes  >= 5)  { await RefreshKillsAsync(ct);  lastKills  = DateTime.UtcNow; }
                    if ((DateTime.UtcNow - lastMining).TotalMinutes >= 30) { await RefreshMiningAsync(ct); lastMining = DateTime.UtcNow; }
                    ResetsInLabel = FormatResetsIn();
                    RaiseDerived();
                }
                catch { /* never let the tracker kill the overlay */ }

                try { await Task.Delay(TimeSpan.FromMinutes(3), ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private string FormatResetsIn()
        {
            var left = EveDay.NextResetUtc(DateTime.UtcNow) - DateTime.UtcNow;
            return $"resets in {(int)left.TotalHours}h {left.Minutes:00}m";
        }

        private async Task RefreshWalletAndLpAsync(CancellationToken ct)
        {
            var tokens = EsiClient.LoadTokens(_db);
            double isk = 0; long lp = 0;
            var needRelink = new List<string>();

            foreach (var t in tokens)
            {
                try
                {
                    var wallet = await _esi.GetWalletBalanceAsync(t, ct);
                    var loyalty = await _esi.GetLoyaltyPointsAsync(t, ct);

                    var row = _store.Get(_dayKey, t.CharacterId);
                    if (row is null || !row.HasBaseline)
                    {
                        _store.UpsertBaseline(_dayKey, t.CharacterId, wallet, loyalty);
                        continue;   // first sight today → baseline only, no delta yet
                    }
                    _store.UpsertLast(_dayKey, t.CharacterId, wallet, loyalty);
                    isk += wallet  - row.WalletBaseline;
                    lp  += loyalty - row.LpBaseline;
                }
                catch (EsiScopeException) { needRelink.Add(t.CharacterName); }
                catch { /* transient — skip this character this round */ }
            }

            IskToday      = isk;
            LpToday       = lp;
            RelinkPrompt  = needRelink.Count == 0
                ? ""
                : $"⚠️ Re-link {string.Join(", ", needRelink)} to enable session stats";
        }

        private async Task RefreshKillsAsync(CancellationToken ct)
        {
            var since = EveDay.NextResetUtc(DateTime.UtcNow).AddDays(-1); // start of the current EVE day
            int total = 0;
            foreach (var t in EsiClient.LoadTokens(_db))
                total += await _zkill.GetKillCountSinceAsync(t.CharacterId, since, ct);
            KillsToday = total;
        }

        private async Task RefreshMiningAsync(CancellationToken ct)
        {
            var totals = new Dictionary<int, long>();
            foreach (var t in EsiClient.LoadTokens(_db))
            {
                try
                {
                    foreach (var kv in await _esi.GetMiningTodayAsync(t, ct))
                        totals[kv.Key] = totals.TryGetValue(kv.Key, out var cur) ? cur + kv.Value : kv.Value;
                }
                catch (EsiScopeException) { /* already surfaced by the wallet/LP pass */ }
                catch { }
            }

            var names = await _esi.ResolveTypeNamesAsync(totals.Keys, ct);
            var lines = totals
                .Select(kv => new OreLine
                {
                    OreName = names.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString(),
                    Units   = kv.Value,
                    PerHour = HoursPlayedToday > 0.01 ? kv.Value / HoursPlayedToday : 0,
                })
                .OrderByDescending(o => o.Units)
                .ToList();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                MinedToday.Clear();
                foreach (var l in lines) MinedToday.Add(l);
            });
        }

        private void RaiseDerived()
        {
            OnPropertyChanged(nameof(IskPerHour));      OnPropertyChanged(nameof(IskTodayLabel));
            OnPropertyChanged(nameof(IskPerHourLabel)); OnPropertyChanged(nameof(LpPerHour));
            OnPropertyChanged(nameof(LpTodayLabel));    OnPropertyChanged(nameof(LpPerHourLabel));
            OnPropertyChanged(nameof(KillsPerHour));    OnPropertyChanged(nameof(KillsPerHourLabel));
            OnPropertyChanged(nameof(PlayedLabel));
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _zkill.Dispose();
        }
    }
}

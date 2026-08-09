// filename: Services/SystemInfoService.cs
// Dotlan-style system info using ESI public endpoints.
// Data: system stats, kills/jumps last hour, adjacent systems, sovereignty.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    // ── Models ────────────────────────────────────────────────────────────
    public sealed class AdjacentSystem
    {
        public int    SystemId   { get; set; }
        public string Name       { get; set; } = "";
        public double Security   { get; set; }
        public string SecColor   { get; set; } = "#FFFFFF";
        public int    Kills      { get; set; }   // ship kills last hour
        public int    NpcKills   { get; set; }
        public int    Jumps      { get; set; }
    }

    public sealed class SystemInfo
    {
        public int    SystemId      { get; set; }
        public string Name          { get; set; } = "";
        public string Region        { get; set; } = "";
        public string Constellation { get; set; } = "";
        public double Security      { get; set; }
        public string SecColor      { get; set; } = "#FFFFFF";
        public string Effect        { get; set; } = "";   // WH effect if any

        // Last-hour activity (ESI sovereignty/kills/jumps stats)
        public int ShipKills        { get; set; }
        public int NpcKills         { get; set; }
        public int PodKills         { get; set; }
        public int Jumps            { get; set; }

        // Sovereignty
        public string SovHolder     { get; set; } = "";  // alliance or corp name
        public string SovFaction    { get; set; } = "";  // for faction warfare

        public List<AdjacentSystem> Adjacent { get; set; } = new();
        public DateTime FetchedAt   { get; set; } = DateTime.UtcNow;
    }

    public sealed class RouteHop
    {
        public int    SystemId   { get; set; }
        public string Name       { get; set; } = "";
        public double Security   { get; set; }
        public string SecColor   { get; set; } = "#FFFFFF";
        public int    ShipKills  { get; set; }
        public int    Jumps      { get; set; }
        public bool   IsOrigin   { get; set; }
        public bool   IsDestination { get; set; }
    }

    public sealed class RouteResult
    {
        public string Origin      { get; set; } = "";
        public string Destination { get; set; } = "";
        public string RouteType   { get; set; } = "shortest";
        public List<RouteHop> Hops { get; set; } = new();
        public int TotalKills  => Hops.Sum(h => h.ShipKills);
        public int TotalJumps  => Hops.Count - 1;
    }

    public sealed class SovSystem
    {
        public int    SystemId      { get; set; }
        public string SystemName    { get; set; } = "";
        public string Region        { get; set; } = "";
        public string AllianceName  { get; set; } = "";
        public int    AllianceId    { get; set; }
        public double Security      { get; set; }
        public string SecColor      { get; set; } = "#FFFFFF";
    }

    // ── Service ───────────────────────────────────────────────────────────
    public sealed class SystemInfoService : IDisposable
    {
        private readonly HttpClient _http;

        // Cache system info for 5 minutes
        private readonly Dictionary<int, (SystemInfo info, DateTime at)> _cache = new();

        // Bulk stat caches refreshed together (ESI returns all systems at once)
        private Dictionary<int, (int shipKills, int npcKills, int podKills)> _killStats = new();
        private Dictionary<int, int>  _jumpStats   = new();
        private DateTime _statsRefreshedAt          = DateTime.MinValue;

        // Name caches
        private readonly Dictionary<int, string> _systemNames = new();
        private readonly Dictionary<int, string> _regionNames = new();
        private readonly Dictionary<int, string> _constNames  = new();
        private readonly Dictionary<int, string> _alliNames   = new();

        private const string ESI = EsiHttp.Base;   // unversioned; see EsiHttp

        public SystemInfoService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            EsiHttp.Configure(_http);
        }

        // ── Main entry point ──────────────────────────────────────────────
        public async Task<SystemInfo> GetSystemAsync(
            int systemId, CancellationToken ct = default,
            Action<string>? onProgress = null)
        {
            // Return cached if fresh
            if (_cache.TryGetValue(systemId, out var cached)
                && (DateTime.UtcNow - cached.at).TotalMinutes < 5)
                return cached.info;

            onProgress?.Invoke("Loading system data…");

            // Refresh bulk stats if stale (> 60 min)
            if ((DateTime.UtcNow - _statsRefreshedAt).TotalMinutes > 60)
            {
                onProgress?.Invoke("Refreshing kill & jump stats…");
                await RefreshBulkStatsAsync(ct);
            }

            // Fetch system static info
            onProgress?.Invoke("Fetching system details…");
            var sysDoc = await GetJsonAsync($"{ESI}/universe/systems/{systemId}/", ct);
            if (sysDoc is null) throw new Exception($"System {systemId} not found.");

            var r = sysDoc.RootElement;
            string name = r.TryGetProperty("name",               out var n)  ? n.GetString() ?? "" : "";
            int    constId = r.TryGetProperty("constellation_id", out var ci) ? ci.GetInt32() : 0;
            double sec  = r.TryGetProperty("security_status",    out var ss) ? ss.GetDouble() : 0;
            sec = Math.Round(sec, 1);

            // Get adjacent systems (stargate destinations)
            var adjacent = new List<int>();
            if (r.TryGetProperty("stargates", out var gates))
                foreach (var g in gates.EnumerateArray())
                    adjacent.Add(g.GetInt32());

            // Resolve constellation → region
            onProgress?.Invoke("Resolving region…");
            int regionId = 0;
            string constName = await GetConstNameAsync(constId, ct);
            var constDoc = await GetJsonAsync($"{ESI}/universe/constellations/{constId}/", ct);
            if (constDoc?.RootElement.TryGetProperty("region_id", out var rid) == true)
                regionId = rid.GetInt32();
            string regionName = await GetRegionNameAsync(regionId, ct);

            // Get stargates → destination system IDs
            onProgress?.Invoke("Loading adjacent systems…");
            var adjSystemIds = new List<int>();
            foreach (var gateId in adjacent.Take(8))
            {
                var gateDoc = await GetJsonAsync($"{ESI}/universe/stargates/{gateId}/", ct);
                if (gateDoc?.RootElement.TryGetProperty("destination", out var dest) == true
                    && dest.TryGetProperty("system_id", out var sid))
                    adjSystemIds.Add(sid.GetInt32());
                await Task.Delay(40, ct);
            }

            // Resolve adjacent system names + stats
            var adjList = new List<AdjacentSystem>();
            foreach (var adjId in adjSystemIds.Distinct())
            {
                string adjName = await GetSystemNameAsync(adjId, ct);
                double adjSec  = await GetSystemSecurityAsync(adjId, ct);
                _killStats.TryGetValue(adjId, out var adjKills);
                _jumpStats.TryGetValue(adjId, out var adjJumps);
                adjList.Add(new AdjacentSystem
                {
                    SystemId = adjId,
                    Name     = adjName,
                    Security = Math.Round(adjSec, 1),
                    SecColor = SecColor(adjSec),
                    Kills    = adjKills.shipKills,
                    NpcKills = adjKills.npcKills,
                    Jumps    = adjJumps,
                });
            }

            // Get sovereignty
            onProgress?.Invoke("Checking sovereignty…");
            string sovHolder  = await GetSovereigntyAsync(systemId, ct);

            _killStats.TryGetValue(systemId, out var ks);
            _jumpStats.TryGetValue(systemId, out var js);

            var info = new SystemInfo
            {
                SystemId      = systemId,
                Name          = name,
                Region        = regionName,
                Constellation = constName,
                Security      = sec,
                SecColor      = SecColor(sec),
                ShipKills     = ks.shipKills,
                NpcKills      = ks.npcKills,
                PodKills      = ks.podKills,
                Jumps         = js,
                SovHolder     = sovHolder,
                Adjacent      = adjList.OrderBy(a => a.Name).ToList(),
                FetchedAt     = DateTime.UtcNow,
            };

            _cache[systemId] = (info, DateTime.UtcNow);
            sysDoc.Dispose();
            return info;
        }

        // Also support lookup by name
        public async Task<SystemInfo> GetSystemByNameAsync(
            string name, CancellationToken ct = default,
            Action<string>? onProgress = null)
        {
            onProgress?.Invoke($"Resolving {name}…");
            int id = await ResolveSystemIdAsync(name, ct);
            if (id == 0) throw new Exception($"System '{name}' not found.");
            return await GetSystemAsync(id, ct, onProgress);
        }

        // ── ESI bulk stats ────────────────────────────────────────────────
        private async Task RefreshBulkStatsAsync(CancellationToken ct)
        {
            try
            {
                // Kill stats (ship/pod/npc kills last hour per system)
                var killDoc = await GetJsonAsync($"{ESI}/universe/system_kills/", ct);
                if (killDoc is not null)
                {
                    _killStats.Clear();
                    foreach (var e in killDoc.RootElement.EnumerateArray())
                    {
                        if (!e.TryGetProperty("system_id", out var sid)) continue;
                        int shipK = e.TryGetProperty("ship_kills", out var sk) ? sk.GetInt32() : 0;
                        int npcK  = e.TryGetProperty("npc_kills",  out var nk) ? nk.GetInt32() : 0;
                        int podK  = e.TryGetProperty("pod_kills",  out var pk) ? pk.GetInt32() : 0;
                        _killStats[sid.GetInt32()] = (shipK, npcK, podK);
                    }
                    killDoc.Dispose();
                }

                // Jump stats
                var jumpDoc = await GetJsonAsync($"{ESI}/universe/system_jumps/", ct);
                if (jumpDoc is not null)
                {
                    _jumpStats.Clear();
                    foreach (var e in jumpDoc.RootElement.EnumerateArray())
                    {
                        if (!e.TryGetProperty("system_id",  out var sid)) continue;
                        if (!e.TryGetProperty("ship_jumps", out var sj))  continue;
                        _jumpStats[sid.GetInt32()] = sj.GetInt32();
                    }
                    jumpDoc.Dispose();
                }

                _statsRefreshedAt = DateTime.UtcNow;
            }
            catch { /* non-fatal — stats just won't show */ }
        }

        // ── ESI sovereignty ───────────────────────────────────────────────
        private async Task<string> GetSovereigntyAsync(int systemId, CancellationToken ct)
        {
            try
            {
                var doc = await GetJsonAsync($"{ESI}/sovereignty/map/", ct);
                if (doc is null) return "";
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("system_id", out var sid)) continue;
                    if (sid.GetInt32() != systemId) continue;
                    if (e.TryGetProperty("alliance_id", out var aid))
                        return await GetAllianceNameAsync(aid.GetInt32(), ct);
                    if (e.TryGetProperty("corporation_id", out var cid))
                        return $"Corp #{cid.GetInt32()}";
                    if (e.TryGetProperty("faction_id", out var fid))
                        return $"Faction #{fid.GetInt32()}";
                }
                doc.Dispose();
            }
            catch { }
            return "";
        }

        // ── ESI name resolvers ────────────────────────────────────────────
        private async Task<int> ResolveSystemIdAsync(string name, CancellationToken ct)
        {
            var body = JsonSerializer.Serialize(new[] { name });
            var req  = new HttpRequestMessage(HttpMethod.Post,
                $"{ESI}/universe/ids/?datasource=tranquility")
            {
                Content = new System.Net.Http.StringContent(
                    body, System.Text.Encoding.UTF8, "application/json")
            };
            try
            {
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return 0;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("systems", out var sys))
                    foreach (var s in sys.EnumerateArray())
                        if (s.TryGetProperty("id", out var id)) return id.GetInt32();
            }
            catch { }
            return 0;
        }

        private async Task<string> GetSystemNameAsync(int id, CancellationToken ct)
        {
            if (_systemNames.TryGetValue(id, out var n)) return n;
            var doc = await GetJsonAsync($"{ESI}/universe/systems/{id}/", ct);
            if (doc is null) return $"#{id}";
            string name = doc.RootElement.TryGetProperty("name", out var nm)
                ? nm.GetString() ?? $"#{id}" : $"#{id}";
            _systemNames[id] = name;
            doc.Dispose();
            return name;
        }

        private async Task<double> GetSystemSecurityAsync(int id, CancellationToken ct)
        {
            var doc = await GetJsonAsync($"{ESI}/universe/systems/{id}/", ct);
            if (doc is null) return 0;
            double sec = doc.RootElement.TryGetProperty("security_status", out var ss)
                ? ss.GetDouble() : 0;
            doc.Dispose();
            return sec;
        }

        private async Task<string> GetRegionNameAsync(int id, CancellationToken ct)
        {
            if (id == 0) return "";
            if (_regionNames.TryGetValue(id, out var n)) return n;
            var doc = await GetJsonAsync($"{ESI}/universe/regions/{id}/", ct);
            if (doc is null) return $"#{id}";
            string name = doc.RootElement.TryGetProperty("name", out var nm)
                ? nm.GetString() ?? "" : "";
            _regionNames[id] = name;
            doc.Dispose();
            return name;
        }

        private async Task<string> GetConstNameAsync(int id, CancellationToken ct)
        {
            if (id == 0) return "";
            if (_constNames.TryGetValue(id, out var n)) return n;
            var doc = await GetJsonAsync($"{ESI}/universe/constellations/{id}/", ct);
            if (doc is null) return $"#{id}";
            string name = doc.RootElement.TryGetProperty("name", out var nm)
                ? nm.GetString() ?? "" : "";
            _constNames[id] = name;
            doc.Dispose();
            return name;
        }

        private async Task<string> GetAllianceNameAsync(int id, CancellationToken ct)
        {
            if (_alliNames.TryGetValue(id, out var n)) return n;
            var doc = await GetJsonAsync($"{ESI}/alliances/{id}/", ct);
            if (doc is null) return $"#{id}";
            string name = doc.RootElement.TryGetProperty("name", out var nm)
                ? nm.GetString() ?? "" : "";
            _alliNames[id] = name;
            doc.Dispose();
            return name;
        }

        // ── HTTP helper ───────────────────────────────────────────────────
        private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct)
        {
            try
            {
                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return null;
                return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            }
            catch { return null; }
        }

        // ── Security color (EVE convention) ───────────────────────────────
        public static string SecColor(double sec)
        {
            if (sec >= 0.5)  return "#FF2ECC71";  // green  — high sec
            if (sec >= 0.1)  return "#FFF39C12";  // orange — low sec
            if (sec >= 0.0)  return "#FFF39C12";  // orange — 0.0
            return                  "#FFEF5350";  // red    — null/wh
        }

        // ── Route calculation ─────────────────────────────────────────────
        public async Task<RouteResult> GetRouteAsync(
            string originName, string destName,
            string routeType = "shortest",  // shortest | secure | insecure
            CancellationToken ct = default,
            Action<string>? onProgress = null)
        {
            onProgress?.Invoke($"Resolving {originName}…");
            int originId = await ResolveSystemIdAsync(originName, ct);
            if (originId == 0) throw new Exception($"System '{originName}' not found.");

            onProgress?.Invoke($"Resolving {destName}…");
            int destId = await ResolveSystemIdAsync(destName, ct);
            if (destId == 0) throw new Exception($"System '{destName}' not found.");

            onProgress?.Invoke("Calculating route…");
            var url = $"{ESI}/route/{originId}/{destId}/?flag={routeType}";
            var doc = await GetJsonAsync(url, ct);
            if (doc is null) throw new Exception("Route calculation failed.");

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new Exception("No route found (systems may be unreachable).");

            var systemIds = doc.RootElement.EnumerateArray()
                .Select(e => e.GetInt32()).ToList();
            doc.Dispose();

            // Refresh stats if stale
            if ((DateTime.UtcNow - _statsRefreshedAt).TotalMinutes > 60)
                await RefreshBulkStatsAsync(ct);

            onProgress?.Invoke($"Resolving {systemIds.Count} system names…");
            var hops = new List<RouteHop>();
            foreach (var sysId in systemIds)
            {
                if (ct.IsCancellationRequested) break;
                string name = await GetSystemNameAsync(sysId, ct);
                double sec  = await GetSystemSecurityAsync(sysId, ct);
                _killStats.TryGetValue(sysId, out var ks);
                _jumpStats.TryGetValue(sysId, out var js);
                hops.Add(new RouteHop
                {
                    SystemId    = sysId,
                    Name        = name,
                    Security    = Math.Round(sec, 1),
                    SecColor    = SecColor(sec),
                    ShipKills   = ks.shipKills,
                    Jumps       = js,
                    IsOrigin      = sysId == originId,
                    IsDestination = sysId == destId,
                });
                await Task.Delay(40, ct);
            }

            return new RouteResult
            {
                Origin      = originName,
                Destination = destName,
                RouteType   = routeType,
                Hops        = hops,
            };
        }

        // ── Sovereignty map ────────────────────────────────────────────────
        public async Task<List<SovSystem>> GetSovereigntyMapAsync(
            CancellationToken ct = default,
            Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Fetching sovereignty data…");
            var sovDoc = await GetJsonAsync($"{ESI}/sovereignty/map/", ct);
            if (sovDoc is null) throw new Exception("Could not load sovereignty data.");

            // Build alliance → systems map
            var alliSystems = new Dictionary<int, List<int>>();
            foreach (var e in sovDoc.RootElement.EnumerateArray())
            {
                if (!e.TryGetProperty("system_id",   out var sid)) continue;
                if (!e.TryGetProperty("alliance_id", out var aid)) continue;
                int alliId = aid.GetInt32();
                if (!alliSystems.ContainsKey(alliId)) alliSystems[alliId] = new();
                alliSystems[alliId].Add(sid.GetInt32());
            }
            sovDoc.Dispose();

            // Keep top 50 alliances by system count
            var topAlliances = alliSystems
                .OrderByDescending(kv => kv.Value.Count)
                .Take(50).ToList();

            onProgress?.Invoke($"Resolving {topAlliances.Count} alliance names…");
            var result = new List<SovSystem>();
            foreach (var (alliId, sysIds) in topAlliances)
            {
                if (ct.IsCancellationRequested) break;
                string alliName = await GetAllianceNameAsync(alliId, ct);
                foreach (var sysId in sysIds.Take(5)) // sample 5 systems per alliance
                {
                    string sysName = await GetSystemNameAsync(sysId, ct);
                    double sec     = await GetSystemSecurityAsync(sysId, ct);
                    result.Add(new SovSystem
                    {
                        SystemId    = sysId,
                        SystemName  = sysName,
                        AllianceId  = alliId,
                        AllianceName = alliName,
                        Security    = Math.Round(sec, 1),
                        SecColor    = SecColor(sec),
                    });
                }
                await Task.Delay(50, ct);
            }
            return result.OrderBy(s => s.AllianceName).ToList();
        }

        public void ClearCache() => _cache.Clear();
        public void Dispose()    => _http.Dispose();
    }
}

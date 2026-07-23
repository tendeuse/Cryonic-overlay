// filename: Services/IntelSearchService.cs
// PROS-style pilot intel using zKillboard API + ESI
//
// zKillboard /api/kills/ returns:  killmail_id, killmail_time, solar_system_id, zkb{hash, totalValue...}
// Full kill details (victim/attackers) come from ESI /killmails/{id}/{hash}/

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
    public sealed class FitModule
    {
        public int    TypeId   { get; set; }          // EVE type ID — used for category lookup
        public string Name     { get; set; } = "";
        public int    Quantity { get; set; } = 1;
        public string Slot     { get; set; } = "";    // High/Mid/Low/Rig/Sub/Cargo
        public bool   IsCharge { get; set; } = false; // true for ammo, scripts, cap boosters, etc.
    }

    public sealed class IntelShipUsage
    {
        public int    TypeId          { get; set; }
        public string ShipName        { get; set; } = "";
        public int    Count           { get; set; }
        public bool   AsKiller        { get; set; }
        public int    LatestKillmailId { get; set; }   // for zkill link
        public List<FitModule> Fit    { get; set; } = new(); // victim fit if loss
    }

    public sealed class IntelAssociate
    {
        public int    CharacterId   { get; set; }
        public string CharacterName { get; set; } = "";
        public string Corporation   { get; set; } = "";
        public int    Appearances   { get; set; }
        public string MostUsedShip  { get; set; } = "";
    }

    public sealed class IntelPlayerResult
    {
        public int      CharacterId    { get; set; }
        public string   CharacterName  { get; set; } = "";
        public string   Corporation    { get; set; } = "";
        public string   Alliance       { get; set; } = "";
        public string   SecurityStatus { get; set; } = "";
        public int      KillsAnalyzed  { get; set; }
        public int      LossesAnalyzed { get; set; }
        public DateTime LastSeen       { get; set; }
        public List<IntelShipUsage>  Ships      { get; set; } = new();
        public List<IntelAssociate>  Associates { get; set; } = new();
    }

    public sealed class IntelSearchService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly HttpClient _zkb;
        private readonly Dictionary<int, IntelPlayerResult> _cache          = new();
        private readonly Dictionary<int, string>            _shipNames      = new();
        private readonly Dictionary<int, int>               _typeGroups     = new(); // typeId  → groupId
        private readonly Dictionary<int, int>               _groupCategories= new(); // groupId → categoryId
        private readonly Dictionary<int, int>               _typeCategories = new(); // typeId  → categoryId (resolved)
        private readonly Dictionary<int, string>            _corpNames      = new();
        private readonly Dictionary<int, string>            _charNames      = new();

        public IntelSearchService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CryonicOverlay", "1.0"));

            // AutomaticDecompression is required — zKillboard always returns gzip
            var zkbHandler = new System.Net.Http.HttpClientHandler
            {
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip |
                    System.Net.DecompressionMethods.Deflate
            };
            _zkb = new HttpClient(zkbHandler) { Timeout = TimeSpan.FromSeconds(30) };
            _zkb.DefaultRequestHeaders.UserAgent.Clear();
            _zkb.DefaultRequestHeaders.UserAgent.ParseAdd(
                "CryonicGamingOverlay/1.0 (EVE Online overlay; Cryonic Gaming Discord)");
            _zkb.DefaultRequestHeaders.Accept.Clear();
            _zkb.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ── Main search ───────────────────────────────────────────────────
        public async Task<IntelPlayerResult> SearchAsync(
            string characterName, CancellationToken ct = default,
            Action<string>? onProgress = null, int lookbackDays = 90)
        {
            onProgress?.Invoke($"Resolving {characterName}…");
            int charId = await ResolveCharacterIdAsync(characterName, ct);
            if (charId == 0)
                throw new Exception($"Character '{characterName}' not found in EVE.");

            if (_cache.TryGetValue(charId, out var cached)) return cached;

            var charInfo = await GetCharInfoAsync(charId, ct);

            onProgress?.Invoke("Fetching kill history from zKillboard…");
            // Fetch kill references from zKillboard (just IDs + hashes)
            var killRefs  = await GetZkbRefsAsync(charId, "kills",  ct);
            var lossRefs  = await GetZkbRefsAsync(charId, "losses", ct);

            onProgress?.Invoke($"Loading {killRefs.Count + lossRefs.Count} killmails from ESI…");
            // NOTE: zKillboard's /api/ endpoint does NOT include killmail_time —
            // that field only exists on the ESI /killmails/{id}/{hash}/ endpoint.
            // We pass the cutoff into FetchKillDetailsAsync which reads the real
            // time from ESI and stops early once kills are older than the window.
            var cutoff = DateTime.UtcNow.AddDays(-lookbackDays);
            var killDetails  = await FetchKillDetailsAsync(killRefs, ct, cutoff);
            var lossDetails  = await FetchKillDetailsAsync(lossRefs, ct, cutoff);
            onProgress?.Invoke($"Found {killDetails.Count} kills / {lossDetails.Count} losses in last {lookbackDays}d…");

            onProgress?.Invoke("Analyzing gang members…");
            var ships      = AnalyzeShips(charId, killDetails, lossDetails);
            var associates = await AnalyzeAssociatesAsync(charId, killDetails, ct);

            var allDates = killDetails.Concat(lossDetails).Select(k => k.killTime).ToList();

            var result = new IntelPlayerResult
            {
                CharacterId    = charId,
                CharacterName  = charInfo.name,
                Corporation    = await GetCorpNameAsync(charInfo.corpId, ct),
                Alliance       = charInfo.allianceId > 0
                    ? await GetAllianceNameAsync(charInfo.allianceId, ct) : "",
                SecurityStatus = charInfo.secStatus.ToString("F1"),
                KillsAnalyzed  = killDetails.Count,
                LossesAnalyzed = lossDetails.Count,
                LastSeen       = allDates.Count > 0 ? allDates.Max() : DateTime.MinValue,
                Ships          = ships,
                Associates     = associates,
            };

            _cache[charId] = result;
            return result;
        }

        // ── ESI: resolve character name → ID ─────────────────────────────
        private async Task<int> ResolveCharacterIdAsync(string name, CancellationToken ct)
        {
            var body = JsonSerializer.Serialize(new[] { name });
            var req  = new HttpRequestMessage(HttpMethod.Post,
                "https://esi.evetech.net/latest/universe/ids/?datasource=tranquility")
            {
                Content = new System.Net.Http.StringContent(
                    body, System.Text.Encoding.UTF8, "application/json")
            };
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return 0;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("characters", out var chars))
                foreach (var c in chars.EnumerateArray())
                    if (c.TryGetProperty("id", out var id)) return id.GetInt32();
            return 0;
        }

        // ── ESI: character public info ────────────────────────────────────
        private async Task<(string name, int corpId, int allianceId, double secStatus)>
            GetCharInfoAsync(int charId, CancellationToken ct)
        {
            try
            {
                var resp = await _http.GetAsync(
                    $"https://esi.evetech.net/latest/characters/{charId}/", ct);
                if (!resp.IsSuccessStatusCode) return ("Unknown", 0, 0, 0);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var r = doc.RootElement;
                return (
                    r.TryGetProperty("name",            out var n)  ? n.GetString() ?? "Unknown" : "Unknown",
                    r.TryGetProperty("corporation_id",  out var ci) ? ci.GetInt32() : 0,
                    r.TryGetProperty("alliance_id",     out var ai) ? ai.GetInt32() : 0,
                    r.TryGetProperty("security_status", out var ss) ? ss.GetDouble(): 0
                );
            }
            catch { return ("Unknown", 0, 0, 0); }
        }

        // ── zKillboard: get kill references (id + hash only) ─────────────
        private record KillRef(int Id, string Hash, DateTime Time);

        private async Task<List<KillRef>> GetZkbRefsAsync(
            int charId, string type, CancellationToken ct)
        {
            try
            {
                // zKillboard requires specific headers — without them it may return HTML or 429
                var url = $"https://zkillboard.com/api/{type}/characterID/{charId}/";
                var resp = await _zkb.GetAsync(url, ct);
                var text = await resp.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine(
                    $"[ZKB] {type} status={resp.StatusCode} len={text.Length} preview={text[..Math.Min(100, text.Length)]}");

                if (!resp.IsSuccessStatusCode) return new();
                if (string.IsNullOrWhiteSpace(text)) return new();

                text = text.Trim();
                if (!text.StartsWith("["))
                {
                    System.Diagnostics.Debug.WriteLine($"[ZKB] Non-array response: {text}");
                    return new();
                }

                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();

                var result = new List<KillRef>();
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("killmail_id", out var kmId)) continue;
                    // killmail_time is inside the main object
                    string timeStr = e.TryGetProperty("killmail_time", out var kmTime)
                        ? kmTime.GetString() ?? "" : "";
                    if (!e.TryGetProperty("zkb", out var zkb)) continue;
                    if (!zkb.TryGetProperty("hash", out var hash)) continue;
                    DateTime.TryParse(timeStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dt);
                    result.Add(new KillRef(kmId.GetInt32(), hash.GetString() ?? "", dt));
                }
                return result;
            }
            catch { return new(); }
        }

        // ── ESI: fetch full killmail details ─────────────────────────────
        private record KillDetail(
            int KillmailId, DateTime killTime, int VictimId, int VictimShipId,
            List<(int charId, int shipTypeId)> Attackers,
            List<(int typeId, int qty, int flag)> VictimItems);

        private async Task<List<KillDetail>> FetchKillDetailsAsync(
            List<KillRef> refs, CancellationToken ct,
            DateTime cutoff = default)
        {
            // Default cutoff: accept everything
            if (cutoff == default) cutoff = DateTime.MinValue;

            var result = new List<KillDetail>();
            // Limit to 20 to avoid rate limits
            foreach (var r in refs.Take(20))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var resp = await _http.GetAsync(
                        $"https://esi.evetech.net/latest/killmails/{r.Id}/{r.Hash}/", ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ESI KM] {r.Id} → {(int)resp.StatusCode}");
                        continue;
                    }
                    System.Diagnostics.Debug.WriteLine($"[ESI KM] {r.Id} → OK");
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                    var root = doc.RootElement;

                    // Parse killmail_time from ESI — this is the authoritative source.
                    // zKillboard's /api/ endpoint does NOT include this field.
                    DateTime killTime = DateTime.MinValue;
                    if (root.TryGetProperty("killmail_time", out var ktProp))
                        DateTime.TryParse(ktProp.GetString(), null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out killTime);

                    // Early-exit: zKillboard returns newest-first, so once we hit
                    // something older than the cutoff we can stop.
                    if (killTime != DateTime.MinValue && killTime < cutoff)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ESI KM] {r.Id} older than cutoff — stopping");
                        break;
                    }

                    int victimId = 0, victimShip = 0;
                    if (root.TryGetProperty("victim", out var v))
                    {
                        v.TryGetProperty("character_id", out var vc);
                        v.TryGetProperty("ship_type_id", out var vs);
                        victimId   = vc.ValueKind == JsonValueKind.Number ? vc.GetInt32() : 0;
                        victimShip = vs.ValueKind == JsonValueKind.Number ? vs.GetInt32() : 0;
                    }

                    var attackers = new List<(int, int)>();
                    if (root.TryGetProperty("attackers", out var atks))
                        foreach (var a in atks.EnumerateArray())
                        {
                            a.TryGetProperty("character_id", out var ac);
                            a.TryGetProperty("ship_type_id", out var ash);
                            int aId  = ac.ValueKind  == JsonValueKind.Number ? ac.GetInt32()  : 0;
                            int aShp = ash.ValueKind == JsonValueKind.Number ? ash.GetInt32() : 0;
                            if (aId > 0) attackers.Add((aId, aShp));
                        }

                    // Parse victim items (fit modules) — only available on losses
                    var items = new List<(int typeId, int qty, int flag)>();
                    if (root.TryGetProperty("victim", out var vv2)
                        && vv2.TryGetProperty("items", out var vitems))
                    {
                        foreach (var item in vitems.EnumerateArray())
                        {
                            int tid = item.TryGetProperty("item_type_id",      out var it) ? it.GetInt32() : 0;
                            int qty = item.TryGetProperty("quantity_destroyed", out var iq) ? iq.GetInt32()
                                    : item.TryGetProperty("quantity_dropped",   out var id2)? id2.GetInt32(): 1;
                            int flg = item.TryGetProperty("flag",              out var fl) ? fl.GetInt32() : 0;
                            if (tid > 0) items.Add((tid, qty, flg));
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[ESI KM] {r.Id} victim={victimId} attackers={attackers.Count} items={items.Count}");
                    result.Add(new KillDetail(r.Id, killTime, victimId, victimShip, attackers, items));
                    await Task.Delay(60, ct); // ~16 req/s — within ESI limit
                }
                catch (OperationCanceledException) { break; }
                catch { /* skip bad killmail */ }
            }
            return result;
        }

        // ── Analyze ships ─────────────────────────────────────────────────
        private List<IntelShipUsage> AnalyzeShips(
            int charId, List<KillDetail> kills, List<KillDetail> losses)
        {
            // counts[key] = (count, latestKillmailId, latestItems)
            var data = new Dictionary<(int typeId, bool asKiller),
                                      (int count, int killId, List<(int,int,int)> items)>();

            foreach (var k in kills.OrderByDescending(x => x.killTime))
                foreach (var (aId, shipId) in k.Attackers)
                    if (aId == charId && shipId > 0)
                    {
                        var key = (shipId, true);
                        var cur = data.GetValueOrDefault(key, (0, 0, new()));
                        data[key] = (cur.count + 1,
                            cur.killId == 0 ? k.KillmailId : cur.killId,
                            cur.items);
                    }

            foreach (var l in losses.OrderByDescending(x => x.killTime))
                if (l.VictimId == charId && l.VictimShipId > 0)
                {
                    var key = (l.VictimShipId, false);
                    var cur = data.GetValueOrDefault(key, (0, 0, new()));
                    // Keep items from the most recent loss in this ship
                    var items = cur.count == 0 ? l.VictimItems : cur.items;
                    data[key] = (cur.count + 1,
                        cur.killId == 0 ? l.KillmailId : cur.killId,
                        items);
                }

            return data
                .OrderByDescending(kv => kv.Value.count).Take(8)
                .Select(kv => new IntelShipUsage
                {
                    TypeId           = kv.Key.typeId,
                    ShipName         = _shipNames.TryGetValue(kv.Key.typeId, out var sn)
                                       ? sn : $"#{kv.Key.typeId}",
                    Count            = kv.Value.count,
                    AsKiller         = kv.Key.asKiller,
                    LatestKillmailId = kv.Value.killId,
                    // Convert flag→slot and build FitModules for losses only
                    Fit = kv.Key.asKiller ? new() : kv.Value.items
                        .Select(i => new FitModule
                        {
                            TypeId   = i.Item1,
                            Name     = $"#{i.Item1}",  // enriched later
                            Quantity = i.Item2,
                            Slot     = FlagToSlot(i.Item3),
                        })
                        .Where(m => m.Slot != "")   // skip truly unknown flags (drone bay, implants…)
                        .OrderBy(m => m.Slot)
                        .ToList(),
                }).ToList();
        }

        // EVE item flag → slot name.
        // Flag 5 = CargoHold — kept so charges/scripts/cap boosters stored in cargo
        // are included; non-charge cargo items are removed during enrichment.
        private static string FlagToSlot(int flag) => flag switch
        {
            >= 11 and <= 18   => "Low",
            >= 19 and <= 26   => "Mid",
            >= 27 and <= 34   => "High",
            >= 92 and <= 99   => "Rig",
            >= 125 and <= 132 => "Sub",
            5                 => "Cargo",  // cargo hold — filtered post-enrichment
            _ => ""   // drone bay, implants etc — skip
        };

        // ── Analyze gang associates ───────────────────────────────────────
        private async Task<List<IntelAssociate>> AnalyzeAssociatesAsync(
            int charId, List<KillDetail> kills, CancellationToken ct)
        {
            var appearances = new Dictionary<int, int>();
            var ships       = new Dictionary<int, Dictionary<int, int>>();

            System.Diagnostics.Debug.WriteLine($"[Assoc] Analyzing {kills.Count} kills for char {charId}");
            foreach (var k in kills)
            {
                System.Diagnostics.Debug.WriteLine($"[Assoc] Kill has {k.Attackers.Count} attackers");
                foreach (var (aId, shipId) in k.Attackers)
                {
                    if (aId == charId || aId == 0) continue;
                    appearances[aId] = appearances.GetValueOrDefault(aId) + 1;
                    if (!ships.ContainsKey(aId)) ships[aId] = new();
                    if (shipId > 0) ships[aId][shipId] = ships[aId].GetValueOrDefault(shipId) + 1;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Assoc] {appearances.Count} unique co-attackers found");
            foreach (var kv in appearances.OrderByDescending(x => x.Value).Take(5))
                System.Diagnostics.Debug.WriteLine($"[Assoc]   char {kv.Key} appeared {kv.Value}x");

            // Lower threshold to 1 so solo players still show associates
            var result = new List<IntelAssociate>();
            foreach (var (aId, count) in appearances
                .Where(kv => kv.Value >= 1)
                .OrderByDescending(kv => kv.Value)
                .Take(8))
            {
                if (ct.IsCancellationRequested) break;
                var info = await GetCharInfoAsync(aId, ct);
                string corp = info.corpId > 0 ? await GetCorpNameAsync(info.corpId, ct) : "";
                string ship = "";
                if (ships.TryGetValue(aId, out var sm) && sm.Count > 0)
                {
                    int topId = sm.OrderByDescending(kv => kv.Value).First().Key;
                    ship = await GetShipNameAsync(topId, ct);
                }
                result.Add(new IntelAssociate
                {
                    CharacterId   = aId,
                    CharacterName = info.name,
                    Corporation   = corp,
                    Appearances   = count,
                    MostUsedShip  = ship,
                });
                await Task.Delay(80, ct);
            }
            return result;
        }

        // ── ESI name helpers ──────────────────────────────────────────────
        private async Task<string> GetCorpNameAsync(int id, CancellationToken ct)
        {
            if (id == 0) return "";
            if (_corpNames.TryGetValue(id, out var c)) return c;
            try {
                var r = await _http.GetAsync($"https://esi.evetech.net/latest/corporations/{id}/", ct);
                if (!r.IsSuccessStatusCode) return "";
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
                var n = d.RootElement.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                return _corpNames[id] = n;
            } catch { return ""; }
        }

        private async Task<string> GetAllianceNameAsync(int id, CancellationToken ct)
        {
            try {
                var r = await _http.GetAsync($"https://esi.evetech.net/latest/alliances/{id}/", ct);
                if (!r.IsSuccessStatusCode) return "";
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
                return d.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            } catch { return ""; }
        }

        // Returns (name, categoryId) for any EVE type.
        // ESI /universe/types/{id}/ returns group_id, NOT category_id.
        // We resolve category by calling /universe/groups/{group_id}/ (cached).
        // EVE category 8 = Charge (missiles, ammo, scripts, cap boosters, nanite paste…)
        private async Task<(string name, int categoryId)> GetTypeInfoAsync(int id, CancellationToken ct)
        {
            if (id == 0) return ($"#{id}", 0);
            // Full cache hit — name and resolved category both known
            if (_shipNames.TryGetValue(id, out var cachedName) && _typeCategories.ContainsKey(id))
                return (cachedName, _typeCategories[id]);
            try
            {
                var r = await _http.GetAsync(
                    $"https://esi.evetech.net/latest/universe/types/{id}/", ct);
                if (!r.IsSuccessStatusCode) return ($"#{id}", 0);
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
                var n      = d.RootElement.TryGetProperty("name",     out var nm) ? nm.GetString() ?? $"#{id}" : $"#{id}";
                int groupId = d.RootElement.TryGetProperty("group_id", out var gi) ? gi.GetInt32() : 0;
                _shipNames[id]  = n;
                _typeGroups[id] = groupId;
                // Resolve category through group endpoint
                int cat = await GetGroupCategoryAsync(groupId, ct);
                _typeCategories[id] = cat;
                return (n, cat);
            }
            catch { return ($"#{id}", 0); }
        }

        // Fetches category_id from /universe/groups/{groupId}/ — cached per session.
        private async Task<int> GetGroupCategoryAsync(int groupId, CancellationToken ct)
        {
            if (groupId == 0) return 0;
            if (_groupCategories.TryGetValue(groupId, out var cached)) return cached;
            try
            {
                var r = await _http.GetAsync(
                    $"https://esi.evetech.net/latest/universe/groups/{groupId}/", ct);
                if (!r.IsSuccessStatusCode) return 0;
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
                int cat = d.RootElement.TryGetProperty("category_id", out var ci) ? ci.GetInt32() : 0;
                _groupCategories[groupId] = cat;
                return cat;
            }
            catch { return 0; }
        }

        private async Task<string> GetShipNameAsync(int id, CancellationToken ct)
        {
            var (name, _) = await GetTypeInfoAsync(id, ct);
            return name;
        }

        public async Task EnrichShipNamesAsync(
            List<IntelShipUsage> ships, CancellationToken ct = default)
        {
            // Enrich hull names
            foreach (var s in ships.Where(x => x.ShipName.StartsWith("#")))
            {
                s.ShipName = await GetShipNameAsync(s.TypeId, ct);
                await Task.Delay(60, ct);
            }

            // Enrich module/charge names and tag IsCharge via ESI category_id
            foreach (var s in ships)
            {
                foreach (var mod in s.Fit)
                {
                    var (name, catId) = await GetTypeInfoAsync(mod.TypeId, ct);
                    if (mod.Name.StartsWith("#")) mod.Name = name;
                    mod.IsCharge = catId == 8; // EVE category 8 = Charge
                    await Task.Delay(40, ct);
                }
                // Drop any cargo-hold items that turned out not to be charges
                // (e.g. ore, salvage, random equipment stored in cargo)
                s.Fit.RemoveAll(m => m.Slot == "Cargo" && !m.IsCharge);
            }
        }

        public void ClearCache() => _cache.Clear();
        public void Dispose() { _http.Dispose(); _zkb.Dispose(); }
    }
}

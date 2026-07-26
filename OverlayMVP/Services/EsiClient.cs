// filename: Services/EsiClient.cs
//
// EVE SSO OAuth2 — entirely client-side, no server involvement.
// Uses TcpListener (no admin rights needed) on a fixed port.
//
// SETUP (one-time):
//   1. Go to https://developers.eveonline.com → Create Application
//   2. Connection Type: Authentication & API Access
//   3. Permissions (Scopes):
//        esi-characters.read_standings.v1
//        esi-location.read_location.v1
//        esi-location.read_ship_type.v1
//   4. Callback URL: http://localhost:4914
//      (no trailing slash — must be EXACT)
//   5. Copy the Client ID → paste in EmbeddedClientId below

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    public sealed class EsiToken
    {
        public int      CharacterId   { get; set; }
        public string   CharacterName { get; set; } = "";
        public string   AccessToken   { get; set; } = "";
        public string   RefreshToken  { get; set; } = "";
        public DateTime ExpiresAt     { get; set; } = DateTime.MinValue;

        public override string ToString() => CharacterName;
    }

    public sealed class EsiCharacterInfo
    {
        public string CharacterName  { get; set; } = "";
        public string Corporation    { get; set; } = "";
        public string ShipType       { get; set; } = "";
        public string SolarSystem    { get; set; } = "";
        public double SecurityStatus { get; set; }
        public string SecurityColour { get; set; } = "#FFFFFF";
    }

    public sealed class EsiFactionStanding
    {
        public int    FactionId   { get; set; }
        public string FactionName { get; set; } = "";
        public double Standing    { get; set; }
    }

    public sealed class EsiCorpStanding
    {
        public int    CorporationId   { get; set; }
        public string CorporationName { get; set; } = "";
        public double Standing        { get; set; }
    }

    public sealed class EsiClient : IDisposable
    {
        // The overlay is a single shared app now — every install uses this one
        // EVE application registration. Native apps have no secret, so baking
        // this in is safe. A user-supplied client id would just break login,
        // so it is no longer configurable (see ClientId below).
        private const string EmbeddedClientId = "24ee90fc5f4f4c7c8284e3dfadaf2920";

        // Fixed port — register EXACTLY "http://localhost:4914" in your EVE app
        // (no trailing slash)
        private const int    CallbackPort = 4914;
        private const string RedirectUri  = "http://localhost:4914";

        private const string Scopes   = "esi-characters.read_standings.v1 " +
                                        "esi-location.read_location.v1 " +
                                        "esi-location.read_ship_type.v1 " +
                                        "esi-skills.read_skills.v1 " +
                                        "esi-wallet.read_character_wallet.v1 " +
                                        "esi-characters.read_loyalty.v1 " +
                                        "esi-industry.read_character_mining.v1";
        private const string SsoBase  = "https://login.eveonline.com";
        private const string EsiBase  = "https://esi.evetech.net/latest";

        public const  string ClientIdKey = "esi_client_id";

        private readonly AppDb      _db;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly JsonSerializerOptions _json = new()
            { PropertyNameCaseInsensitive = true };

        // Always the baked-in constant — the overlay is one shared app now, and a
        // wrong user-supplied value would just break login for that user.
        private string ClientId => EmbeddedClientId;

        // ── Public info: what URL to register ────────────────────────────
        public static string RequiredCallbackUrl => RedirectUri;

        // ── DB helpers ────────────────────────────────────────────────────
        // The client id is baked in (EmbeddedClientId) and no longer user-editable —
        // this always returns the constant regardless of what (if anything) is stored.
        public static string LoadClientId(AppDb db) => EmbeddedClientId;

        // Kept so any stray caller still compiles; no-op since the client id is fixed.
        public static void SaveClientId(AppDb db, string clientId) { }

        public static void EnsureTable(AppDb db)
        {
            using var con = db.Open();

            // Step 1 — check for old schema (had 'id' column) using separate connection scope
            bool hasOldSchema = false;
            try
            {
                using var check = con.CreateCommand();
                check.CommandText = "PRAGMA table_info(esi_token)";
                using var r = check.ExecuteReader();
                while (r.Read())
                {
                    if (r.GetString(1) == "id") { hasOldSchema = true; }
                }
                // reader is fully disposed here before any transaction
            }
            catch { /* table doesn't exist yet — that's fine */ }

            // Step 2 — migrate old schema if needed (reader is closed)
            if (hasOldSchema)
            {
                try
                {
                    using var tx = con.BeginTransaction();
                    using var a = con.CreateCommand(); a.Transaction = tx;
                    a.CommandText = "ALTER TABLE esi_token RENAME TO esi_token_old";
                    a.ExecuteNonQuery();
                    using var b = con.CreateCommand(); b.Transaction = tx;
                    b.CommandText = @"CREATE TABLE esi_token(
  character_id INTEGER PRIMARY KEY,
  character_name TEXT NOT NULL,
  access_token TEXT NOT NULL,
  refresh_token TEXT NOT NULL,
  expires_at TEXT NOT NULL)";
                    b.ExecuteNonQuery();
                    using var c = con.CreateCommand(); c.Transaction = tx;
                    c.CommandText = @"INSERT OR IGNORE INTO esi_token
                        SELECT character_id,character_name,access_token,refresh_token,expires_at
                        FROM esi_token_old";
                    c.ExecuteNonQuery();
                    using var d = con.CreateCommand(); d.Transaction = tx;
                    d.CommandText = "DROP TABLE esi_token_old";
                    d.ExecuteNonQuery();
                    tx.Commit();
                }
                catch { /* migration failed — drop and recreate cleanly */ 
                    try {
                        using var drop = con.CreateCommand();
                        drop.CommandText = "DROP TABLE IF EXISTS esi_token";
                        drop.ExecuteNonQuery();
                    } catch { }
                }
            }

            // Step 3 — create table if it doesn't exist (new install or post-migration)
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS esi_token(
  character_id   INTEGER PRIMARY KEY,
  character_name TEXT    NOT NULL,
  access_token   TEXT    NOT NULL,
  refresh_token  TEXT    NOT NULL,
  expires_at     TEXT    NOT NULL
)";
            cmd.ExecuteNonQuery();
        }

        public static List<EsiToken> LoadTokens(AppDb db)
        {
            EnsureTable(db);
            var list = new List<EsiToken>();
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "SELECT character_id,character_name,access_token,refresh_token,expires_at " +
                    "FROM esi_token ORDER BY character_name";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(ReadToken(r));
            }
            catch { }
            return list;
        }

        public static EsiToken? LoadToken(AppDb db, int characterId)
        {
            EnsureTable(db);
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "SELECT character_id,character_name,access_token,refresh_token,expires_at " +
                "FROM esi_token WHERE character_id=$cid";
            cmd.Parameters.AddWithValue("$cid", characterId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadToken(r) : null;
        }

        // Backwards-compat: returns first token (for callers that don't specify a character)
        public static EsiToken? LoadToken(AppDb db)
        {
            var list = LoadTokens(db);
            return list.Count > 0 ? list[0] : null;
        }

        private static EsiToken ReadToken(System.Data.IDataReader r) => new()
        {
            CharacterId   = r.GetInt32(0),
            CharacterName = r.GetString(1),
            AccessToken   = r.GetString(2),
            RefreshToken  = r.GetString(3),
            ExpiresAt     = DateTime.Parse(r.GetString(4), null,
                                System.Globalization.DateTimeStyles.RoundtripKind),
        };

        private static void SaveToken(AppDb db, EsiToken t)
        {
            EnsureTable(db);
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO esi_token(character_id,character_name,access_token,refresh_token,expires_at)
VALUES($cid,$cn,$at,$rt,$ea)
ON CONFLICT(character_id) DO UPDATE SET
  character_name=excluded.character_name,
  access_token=excluded.access_token,
  refresh_token=excluded.refresh_token,
  expires_at=excluded.expires_at";
            cmd.Parameters.AddWithValue("$cid", t.CharacterId);
            cmd.Parameters.AddWithValue("$cn",  t.CharacterName);
            cmd.Parameters.AddWithValue("$at",  t.AccessToken);
            cmd.Parameters.AddWithValue("$rt",  t.RefreshToken);
            cmd.Parameters.AddWithValue("$ea",  t.ExpiresAt.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void DeleteToken(AppDb db, int characterId)
        {
            EnsureTable(db);
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM esi_token WHERE character_id=$cid";
            cmd.Parameters.AddWithValue("$cid", characterId);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteAllTokens(AppDb db)
        {
            EnsureTable(db);
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM esi_token";
            cmd.ExecuteNonQuery();
        }

        // Backwards-compat
        public static void DeleteToken(AppDb db)
            => DeleteAllTokens(db);

        public EsiClient(AppDb db) { _db = db; }

        // ── OAuth2 flow ───────────────────────────────────────────────────
        public async Task<string> AuthorizeAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ClientId))
                throw new InvalidOperationException(
                    "EVE Client ID not configured.\n" +
                    "Open ⚙ Settings → enter your EVE App Client ID.\n" +
                    $"Register your app at developers.eveonline.com\n" +
                    $"Callback URL must be exactly: {RedirectUri}");

            string state   = Guid.NewGuid().ToString("N");
            string authUrl = $"{SsoBase}/v2/oauth/authorize" +
                $"?response_type=code" +
                $"&client_id={Uri.EscapeDataString(ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&scope={Uri.EscapeDataString(Scopes)}" +
                $"&state={state}";

            // Use TcpListener — works without admin rights unlike HttpListener
            var tcpListener = new TcpListener(IPAddress.Loopback, CallbackPort);
            try { tcpListener.Start(); }
            catch (SocketException ex)
            {
                throw new Exception(
                    $"Could not listen on port {CallbackPort}.\n" +
                    $"Is another app using it? ({ex.Message})");
            }

            // Open browser AFTER listener is ready
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            // Wait for EVE to redirect back (timeout 5 min)
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linked  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            TcpClient tcp;
            try   { tcp = await tcpListener.AcceptTcpClientAsync().WaitAsync(linked.Token); }
            finally { tcpListener.Stop(); }

            // Read the HTTP GET request
            using var stream = tcp.GetStream();
            var buf    = new byte[4096];
            int nRead  = await stream.ReadAsync(buf, 0, buf.Length, ct);
            var request= Encoding.UTF8.GetString(buf, 0, nRead);

            // Extract the first line: GET /?code=...&state=... HTTP/1.1
            var firstLine = request.Split('\n')[0];
            string? code = null, gotState = null;
            var pathPart = firstLine.Split(' ').Length > 1 ? firstLine.Split(' ')[1] : "";
            var qIdx = pathPart.IndexOf('?');
            if (qIdx >= 0)
            {
                var qs = pathPart[(qIdx + 1)..];
                foreach (var part in qs.Split('&'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        if (kv[0] == "code")  code     = Uri.UnescapeDataString(kv[1]);
                        if (kv[0] == "state") gotState = Uri.UnescapeDataString(kv[1]);
                    }
                }
            }

            // Send browser response
            bool success = gotState == state && code is not null;
            var body = success
                ? "<html><head><style>body{background:#0a1a2f;color:#ccd6f6;font-family:Consolas;" +
                  "display:flex;align-items:center;justify-content:center;height:100vh;margin:0}" +
                  "h2{color:#2ecc71}</style></head><body>" +
                  "<h2>✅ EVE character linked! You can close this window.</h2></body></html>"
                : "<html><head><style>body{background:#0a1a2f;color:#ef5350;font-family:Consolas;" +
                  "display:flex;align-items:center;justify-content:center;height:100vh;margin:0}" +
                  "</style></head><body>" +
                  "<h2>⚠️ Auth failed. Please close this and try again.</h2></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var response  = "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: text/html; charset=utf-8\r\n" +
                           $"Content-Length: {bodyBytes.Length}\r\n" +
                            "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response), ct);
            await stream.WriteAsync(bodyBytes, ct);
            tcp.Close();

            if (!success) throw new Exception(
                gotState != state ? "OAuth state mismatch." : "No code in callback.");

            return await ExchangeCodeAsync(code!, ct);
        }

        private async Task<string> ExchangeCodeAsync(string code, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{SsoBase}/v2/oauth/token");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]   = "authorization_code",
                ["code"]         = code,
                ["client_id"]    = ClientId,
                ["redirect_uri"] = RedirectUri,
            });

            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Token exchange failed ({(int)resp.StatusCode}): {body}");

            var tokens = JsonSerializer.Deserialize<TokenResponse>(body, _json)
                         ?? throw new Exception("Empty token response");

            var verifyReq = new HttpRequestMessage(HttpMethod.Get, $"{SsoBase}/v2/oauth/verify");
            verifyReq.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var verifyResp = await _http.SendAsync(verifyReq, ct);
            var verifyBody = await verifyResp.Content.ReadAsStringAsync(ct);
            var charInfo   = JsonSerializer.Deserialize<VerifyResponse>(verifyBody, _json)
                             ?? throw new Exception("Empty verify response");

            var token = new EsiToken
            {
                CharacterId   = charInfo.CharacterId,
                CharacterName = charInfo.CharacterName,
                AccessToken   = tokens.AccessToken,
                RefreshToken  = tokens.RefreshToken,
                ExpiresAt     = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 30),
            };
            SaveToken(_db, token);
            return charInfo.CharacterName;
        }

        // ── Token refresh ─────────────────────────────────────────────────
        private async Task<string?> RefreshAsync(EsiToken token, CancellationToken ct)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{SsoBase}/v2/oauth/token");
                req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"]    = "refresh_token",
                    ["refresh_token"] = token.RefreshToken,
                    ["client_id"]     = ClientId,
                });
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return null;
                var tr = JsonSerializer.Deserialize<TokenResponse>(
                    await resp.Content.ReadAsStringAsync(ct), _json);
                if (tr is null) return null;
                token.AccessToken = tr.AccessToken;
                if (!string.IsNullOrEmpty(tr.RefreshToken))
                    token.RefreshToken = tr.RefreshToken;
                token.ExpiresAt = DateTime.UtcNow.AddSeconds(tr.ExpiresIn - 30);
                SaveToken(_db, token);
                return token.AccessToken;
            }
            catch { return null; }
        }

        private async Task<string?> GetValidToken(CancellationToken ct, int characterId = 0)
        {
            var t = characterId > 0 ? LoadToken(_db, characterId) : LoadToken(_db);
            if (t is null) return null;
            if (DateTime.UtcNow >= t.ExpiresAt)
                return await RefreshAsync(t, ct);
            return t.AccessToken;
        }

        /// <summary>Returns a valid access token for this character, refreshing if expired.</summary>
        public async Task<string> GetValidAccessTokenAsync(EsiToken token, CancellationToken ct = default)
        {
            if (token is null) return "";
            if (token.ExpiresAt > DateTime.UtcNow.AddMinutes(1)) return token.AccessToken;
            var refreshed = await RefreshAsync(token, ct);   // existing private refresh; mutates token in place
            return refreshed ?? "";
        }

        // ── ESI calls ─────────────────────────────────────────────────────
        public async Task<EsiCharacterInfo?> GetCharacterAsync(CancellationToken ct = default, int characterId = 0)
        {
            var token = characterId > 0 ? LoadToken(_db, characterId) : LoadToken(_db);
            if (token is null) return null;
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return null;
            int charId = token.CharacterId;

            async Task<JsonDocument?> Get(string url, bool auth = false)
            {
                var r = new HttpRequestMessage(HttpMethod.Get, url);
                if (auth) r.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", access);
                try
                {
                    var resp = await _http.SendAsync(r, ct);
                    if (!resp.IsSuccessStatusCode) return null;
                    return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                }
                catch { return null; }
            }

            async Task<string> ResolveName(string endpoint)
            {
                using var d = await Get(endpoint);
                return d?.RootElement.TryGetProperty("name", out var n) == true
                    ? n.GetString() ?? "" : "";
            }

            using var loc  = await Get($"{EsiBase}/characters/{charId}/location/", auth: true);
            using var ship = await Get($"{EsiBase}/characters/{charId}/ship/",     auth: true);
            using var pub  = await Get($"{EsiBase}/characters/{charId}/");

            int sysId  = loc?. RootElement.TryGetProperty("solar_system_id", out var s)  == true ? s.GetInt32()  : 0;
            int typeId = ship?.RootElement.TryGetProperty("ship_type_id",    out var tp) == true ? tp.GetInt32() : 0;
            int corpId = pub?. RootElement.TryGetProperty("corporation_id",  out var c)  == true ? c.GetInt32()  : 0;

            // Fetch solar system info for system name AND true security status
            // (character endpoint has CONCORD standing, NOT system security)
            double sysSec = 0;
            string system = "";
            if (sysId > 0)
            {
                using var sysDoc = await Get($"{EsiBase}/universe/systems/{sysId}/");
                system = sysDoc?.RootElement.TryGetProperty("name", out var sn) == true
                    ? sn.GetString() ?? "" : "";
                sysSec = sysDoc?.RootElement.TryGetProperty("security_status", out var ss2) == true
                    ? ss2.GetDouble() : 0;
            }

            string shipName = typeId > 0 ? await ResolveName($"{EsiBase}/universe/types/{typeId}/")  : "";
            string corp     = corpId > 0 ? await ResolveName($"{EsiBase}/corporations/{corpId}/")    : "";

            // Round to 1 decimal but preserve sign (EVE convention)
            double sec = Math.Round(sysSec, 1);
            // Clamp display: EVE shows -0.0 as 0.0
            if (sec == 0.0) sec = 0.0;
            return new EsiCharacterInfo
            {
                CharacterName  = token.CharacterName,
                Corporation    = corp,
                ShipType       = shipName,
                SolarSystem    = system,
                SecurityStatus = sec,
                SecurityColour = sec >= 0.5 ? "#2ECC71" : sec >= 0.0 ? "#F39C12" : "#E74C3C",
            };
        }

        // Verified against the live ESI /universe/factions/ endpoint. The previous
        // table mislabeled 500014–500027 (e.g. 500024 is Drifters, not ORE; ORE is
        // 500014; EDENCOM/Triglavian were swapped). Do not "correct" from memory.
        private static readonly Dictionary<int, string> FactionNames = new()
        {
            // Empire factions
            [500001] = "Caldari State",             [500002] = "Minmatar Republic",
            [500003] = "Amarr Empire",              [500004] = "Gallente Federation",
            [500005] = "Jove Empire",
            // CONCORD & aligned
            [500006] = "CONCORD Assembly",          [500007] = "Ammatar Mandate",
            [500008] = "Khanid Kingdom",            [500009] = "The Syndicate",
            // Pirate factions
            [500010] = "Guristas Pirates",          [500011] = "Angel Cartel",
            [500012] = "Blood Raider Covenant",     [500019] = "Sansha's Nation",
            [500020] = "Serpentis",
            // Industrial / independent
            [500013] = "EverMore",                  [500014] = "ORE",
            [500015] = "Thukker Tribe",             [500018] = "Mordu's Legion Command",
            // Mission / lore factions
            [500016] = "Servant Sisters of EVE",    [500017] = "The Society of Conscious Thought",
            // Pochven / Triglavian-era factions
            [500024] = "Drifters",                  [500025] = "Rogue Drones",
            [500026] = "Triglavian Collective",     [500027] = "EDENCOM",
        };

        // Returns full skill dictionary: skill_id → active_skill_level
        // active_skill_level matches what EVE's character sheet displays and accounts for
        // Alpha clone caps. trained_skill_level can be higher if a char downgraded from Omega.
        public async Task<Dictionary<int, int>> GetAllSkillsAsync(
            CancellationToken ct = default, int characterId = 0)
        {
            var token  = characterId > 0 ? LoadToken(_db, characterId) : LoadToken(_db);
            if (token is null) return new();
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return new();

            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/skills/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            try
            {
                var resp  = await _http.SendAsync(req, ct);
                var body  = await resp.Content.ReadAsStringAsync(ct); // read once
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"ESI skills returned {(int)resp.StatusCode}: {body}");

                var result = new Dictionary<int, int>();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("skills", out var skills))
                {
                    foreach (var skill in skills.EnumerateArray())
                    {
                        if (!skill.TryGetProperty("skill_id", out var sid)) continue;
                        int id = sid.GetInt32();

                        // Prefer active_skill_level — matches EVE's character sheet.
                        // Fall back to trained_skill_level if active is absent (older ESI versions).
                        int level;
                        if (skill.TryGetProperty("active_skill_level",  out var act))
                            level = act.GetInt32();
                        else if (skill.TryGetProperty("trained_skill_level", out var tr))
                            level = tr.GetInt32();
                        else
                            continue; // skip malformed entry

                        result[id] = level;
                    }
                }
                return result;
            }
            catch (Exception e) when (e is not InvalidOperationException)
            {
                throw; // re-throw so callers can surface 403 scope errors
            }
        }

        // Skills that Alpha clones CAN reach to level 5
        private static readonly HashSet<int> _alphaLevel5Skills = new()
        {
            3392, // Mechanic
            3413, // Power Grid Management
            3416, // Shield Management
            3422, // Shield Operation
            3428, // CPU Management
            3436, // Drones
            3456, // Navigation
            3386, // Mining
            3355, // Social
            3359, // Connections
            3357, // Diplomacy
        };

        /// <summary>
        /// Heuristic Alpha/Omega detection: if any skill is trained to level 5
        /// that an Alpha clone cannot reach level 5, the character is (or was) Omega.
        /// </summary>
        public static bool DetectIsOmega(Dictionary<int, int> skills)
        {
            foreach (var (id, level) in skills)
                if (level >= 5 && !_alphaLevel5Skills.Contains(id))
                    return true;
            return false;
        }

        // Convenience: the three standing-related social skills.
        // Connections (3359) = +4%/lvl effective POSITIVE standing (access).
        // Diplomacy   (3357) = +4%/lvl effective NEGATIVE standing (access).
        // Social      (3355) = +5%/lvl to standing GAINS from missions.
        public async Task<(int connections, int diplomacy, int social)> GetSkillsAsync(
            CancellationToken ct = default, int characterId = 0)
        {
            var all = await GetAllSkillsAsync(ct, characterId);
            return (all.GetValueOrDefault(3359), all.GetValueOrDefault(3357), all.GetValueOrDefault(3355));
        }

        public async Task<List<EsiFactionStanding>> GetStandingsAsync(
            CancellationToken ct = default, int characterId = 0)
        {
            var token  = characterId > 0 ? LoadToken(_db, characterId) : LoadToken(_db);
            if (token is null) return new();
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return new();

            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/standings/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            try
            {
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return new();
                var list = new List<EsiFactionStanding>();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("from_type", out var ft) ||
                        ft.GetString() != "faction") continue;
                    int id = e.GetProperty("from_id").GetInt32();
                    if (!FactionNames.ContainsKey(id)) continue;
                    list.Add(new EsiFactionStanding
                    {
                        FactionId   = id,
                        FactionName = FactionNames[id],
                        Standing    = Math.Round(e.GetProperty("standing").GetDouble(), 2),
                    });
                }
                return list;
            }
            catch { return new(); }
        }

        /// <summary>
        /// NPC corporation standings (from_type == "npc_corp"). Regular missions raise
        /// AGENT + CORPORATION standing — this is what a mission-grind countdown must use.
        /// Names are resolved lazily by the caller (ESI returns ids only here).
        /// </summary>
        public async Task<List<EsiCorpStanding>> GetCorpStandingsAsync(
            CancellationToken ct = default, int characterId = 0)
        {
            var token  = characterId > 0 ? LoadToken(_db, characterId) : LoadToken(_db);
            if (token is null) return new();
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return new();

            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/standings/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            try
            {
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return new();
                var list = new List<EsiCorpStanding>();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("from_type", out var ft) ||
                        ft.GetString() != "npc_corp") continue;
                    list.Add(new EsiCorpStanding
                    {
                        CorporationId = e.GetProperty("from_id").GetInt32(),
                        Standing      = Math.Round(e.GetProperty("standing").GetDouble(), 2),
                    });
                }
                return list;
            }
            catch { return new(); }
        }

        private static readonly Dictionary<string, int> _corpIdCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Resolve an NPC corporation NAME to its id via public ESI. 0 if not found.</summary>
        public async Task<int> ResolveCorporationIdAsync(string corpName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(corpName)) return 0;
            if (_corpIdCache.TryGetValue(corpName, out var cached)) return cached;
            try
            {
                var body = JsonSerializer.Serialize(new[] { corpName });
                var req  = new HttpRequestMessage(HttpMethod.Post, $"{EsiBase}/universe/ids/")
                { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return 0;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (!doc.RootElement.TryGetProperty("corporations", out var corps)) return 0;
                foreach (var c in corps.EnumerateArray())
                {
                    int id = c.GetProperty("id").GetInt32();
                    _corpIdCache[corpName] = id;
                    return id;
                }
            }
            catch { }
            return 0;
        }

        // ── Session tracker data ──────────────────────────────────────────
        // NOTE: unlike the older methods here, these THROW on 403 so the caller
        // can tell "token lacks the new scope" (→ prompt re-link) apart from a
        // transient failure. Other errors return a neutral value.

        /// <summary>Wallet balance in ISK. Throws EsiScopeException on 403.</summary>
        public async Task<double> GetWalletBalanceAsync(EsiToken token, CancellationToken ct = default)
        {
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return 0;
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/wallet/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            var resp = await _http.SendAsync(req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new EsiScopeException("esi-wallet.read_character_wallet.v1");
            if (!resp.IsSuccessStatusCode) return 0;
            var text = await resp.Content.ReadAsStringAsync(ct);
            return double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>Total loyalty points across all corps. Throws EsiScopeException on 403.</summary>
        public async Task<long> GetLoyaltyPointsAsync(EsiToken token, CancellationToken ct = default)
        {
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return 0;
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/loyalty/points/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            var resp = await _http.SendAsync(req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new EsiScopeException("esi-characters.read_loyalty.v1");
            if (!resp.IsSuccessStatusCode) return 0;
            long total = 0;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            foreach (var e in doc.RootElement.EnumerateArray())
                if (e.TryGetProperty("loyalty_points", out var lp)) total += lp.GetInt64();
            return total;
        }

        /// <summary>Today's mining ledger: type_id → units. Throws EsiScopeException on 403.</summary>
        public async Task<Dictionary<int, long>> GetMiningTodayAsync(EsiToken token, CancellationToken ct = default)
        {
            var result = new Dictionary<int, long>();
            var access = await GetValidToken(ct, token.CharacterId);
            if (access is null) return result;
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{EsiBase}/characters/{token.CharacterId}/mining/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            var resp = await _http.SendAsync(req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new EsiScopeException("esi-industry.read_character_mining.v1");
            if (!resp.IsSuccessStatusCode) return result;

            // The ledger is aggregated per calendar date ("YYYY-MM-DD"); take the rows whose
            // date falls on/after the current EVE-day key (mining after 11:00 UTC yesterday
            // still lands on yesterday's ledger date, so match the EVE-day key exactly and
            // also accept today's calendar date).
            string dayKey  = EveDay.KeyFor(DateTime.UtcNow);
            string calDate = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var date = e.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "";
                if (date != dayKey && date != calDate) continue;
                int  typeId = e.GetProperty("type_id").GetInt32();
                long qty    = e.GetProperty("quantity").GetInt64();
                result[typeId] = result.TryGetValue(typeId, out var cur) ? cur + qty : qty;
            }
            return result;
        }

        private static readonly Dictionary<int, string> _typeNameCache = new();

        /// <summary>Resolve type_ids to names via public ESI, cached in memory.</summary>
        public async Task<Dictionary<int, string>> ResolveTypeNamesAsync(
            IEnumerable<int> typeIds, CancellationToken ct = default)
        {
            var need = new List<int>();
            var outMap = new Dictionary<int, string>();
            foreach (var id in typeIds)
            {
                if (_typeNameCache.TryGetValue(id, out var cached)) outMap[id] = cached;
                else if (!need.Contains(id)) need.Add(id);
            }
            if (need.Count == 0) return outMap;
            try
            {
                var body = JsonSerializer.Serialize(need);
                var req  = new HttpRequestMessage(HttpMethod.Post, $"{EsiBase}/universe/names/")
                { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return outMap;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    int id   = e.GetProperty("id").GetInt32();
                    var name = e.GetProperty("name").GetString() ?? id.ToString();
                    _typeNameCache[id] = name;
                    outMap[id] = name;
                }
            }
            catch { /* names are cosmetic — fall back to ids */ }
            return outMap;
        }

        public void Dispose() => _http.Dispose();

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]  public string AccessToken  { get; set; } = "";
            [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
            [JsonPropertyName("expires_in")]    public int    ExpiresIn    { get; set; } = 1200;
        }
        private sealed class VerifyResponse
        {
            [JsonPropertyName("CharacterID")]   public int    CharacterId   { get; set; }
            [JsonPropertyName("CharacterName")] public string CharacterName { get; set; } = "";
        }
    }

    /// <summary>Thrown when ESI rejects a call because the token lacks the required scope.</summary>
    public sealed class EsiScopeException : Exception
    {
        public string Scope { get; }
        public EsiScopeException(string scope) : base($"Token missing scope: {scope}") => Scope = scope;
    }
}

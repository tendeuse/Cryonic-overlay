// filename: Services/OverlayApiClient.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OverlayMVP.Models;

namespace OverlayMVP.Services
{
    /// <summary>Thrown when the backend returns a non-success status. Carries the HTTP status
    /// code so callers can special-case things like 403 (account too new to post intel).</summary>
    public sealed class OverlayApiException : Exception
    {
        public int StatusCode { get; }
        public OverlayApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;
    }

    /// <summary>
    /// Outcome of a standing claim. A refusal is not an exception — "you're at +3.42,
    /// need +5.00" is a normal, expected answer that the UI shows as-is.
    /// </summary>
    public sealed record StandingClaimResult(
        bool Ok, double? Standing, double? Current, double? Required, string Message);

    public sealed class SponsorInfo
    {
        [JsonPropertyName("enabled")]  public bool   Enabled  { get; set; }
        [JsonPropertyName("headline")] public string Headline { get; set; } = "";
        [JsonPropertyName("subtext")]  public string Subtext  { get; set; } = "";
        [JsonPropertyName("url")]      public string Url      { get; set; } = "";
    }

    public sealed class VersionInfo
    {
        [JsonPropertyName("version")]      public string Version     { get; set; } = "";
        [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("notes")]        public string Notes       { get; set; } = "";
    }

    /// <summary>
    /// Talks to the live Cloudflare Worker intel API (see BackendSession.ApiBase). Every
    /// authed call is stamped with the bearer token for whichever character is currently
    /// active in the overlay — the token provider is supplied by the view-model so intel
    /// is always attributed to whoever is flying.
    /// </summary>
    public sealed class OverlayApiClient : IDisposable
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient _http;
        private readonly string     _baseUrl = BackendSession.ApiBase;
        private readonly Func<bool, Task<string?>> _getToken;

        /// <param name="getToken">Resolves a backend JWT for the currently active character.
        /// Called with <c>forceRefresh: true</c> exactly once, to retry after a 401.</param>
        public OverlayApiClient(Func<bool, Task<string?>> getToken)
        {
            _getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        // ----------------------------------------------------------------
        // Intel
        // ----------------------------------------------------------------

        public async Task<bool> PostIntelAsync(string system, string type, string notes, CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/intel";
            var body = JsonSerializer.Serialize(new { system, type, notes }, _json);

            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);

            await EnsureSuccessAsync(resp);
            return true;
        }

        public async Task<List<IntelReport>> GetIntelAsync(string system, CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/intel?system={Uri.EscapeDataString(system)}";
            var resp = await SendAuthedAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<IntelListResponse>(text, _json);
            return data?.Intel.Select(ToIntelReport).ToList() ?? new List<IntelReport>();
        }

        // ----------------------------------------------------------------
        // Orders (missions)
        // ----------------------------------------------------------------

        public async Task<List<Mission>> GetOrdersAsync(string? system, CancellationToken ct = default)
        {
            var url = $"{_baseUrl}/missions";
            if (!string.IsNullOrEmpty(system)) url += $"?system={Uri.EscapeDataString(system)}";

            var resp = await SendAuthedAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<OrderListResponse>(text, _json);
            return data?.Missions.Select(ToMission).ToList() ?? new List<Mission>();
        }

        /// <summary>Broadcast an Order. Scope/target are authorised server-side against the caller's role.</summary>
        public async Task<bool> PostOrderAsync(string title, string description,
                                               string targetScope, long? targetId,
                                               CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/missions";
            var body = JsonSerializer.Serialize(new
            {
                title,
                description,
                target_scope = targetScope,
                target_id    = targetId,
            }, _json);

            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);

            await EnsureSuccessAsync(resp);
            return true;
        }

        /// <summary>
        /// Join an order. <paramref name="esiToken"/> is REQUIRED for standing goals —
        /// the backend reads the pilot's standing itself rather than trusting a
        /// self-reported value — and ignored for every other order type.
        /// </summary>
        public async Task<bool> JoinOrderAsync(int id, string? esiToken = null, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { esi_token = esiToken }, _json);
            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/missions/{id}/join")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);
            await EnsureSuccessAsync(resp);
            return true;
        }

        public async Task<bool> LeaveOrderAsync(int id, CancellationToken ct = default)
        {
            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/missions/{id}/join"), ct);
            await EnsureSuccessAsync(resp);
            return true;
        }

        public async Task<bool> CompleteOrderAsync(int id, string note, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { note }, _json);
            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/missions/{id}/complete")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);
            await EnsureSuccessAsync(resp);
            return true;
        }

        public async Task<bool> SubmitKillAsync(int missionId, long killmailId, string hash,
                                                CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { killmail_id = killmailId, hash }, _json);
            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/missions/{missionId}/submit-kill")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);
            await EnsureSuccessAsync(resp);
            return true;
        }

        public async Task<StandingClaimResult> ClaimStandingAsync(
            int id, string esiToken, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { esi_token = esiToken }, _json);
            var resp = await SendAuthedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/missions/{id}/claim-standing")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, ct);

            var text = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                // TryGetProperty succeeds for an explicit JSON null, and GetDouble() then
                // throws InvalidOperationException — which the JsonException catch below
                // would not stop. Check the value kind, not just the key's presence.
                static double? Num(JsonElement root, string name)
                    => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetDouble()
                        : null;

                if (resp.IsSuccessStatusCode)
                {
                    double? reached = Num(root, "standing");
                    return new StandingClaimResult(true, reached, null, null, "Standing goal claimed.");
                }
                // A below-threshold refusal carries the pilot's current value so the UI
                // can say how far off they are instead of just "rejected".
                double? cur = Num(root, "current");
                double? req = Num(root, "required");
                var msg = root.TryGetProperty("error", out var e) ? e.GetString() ?? "Claim refused." : "Claim refused.";
                return new StandingClaimResult(false, null, cur, req, msg);
            }
            catch (JsonException)
            {
                return new StandingClaimResult(false, null, null, null, $"HTTP {(int)resp.StatusCode}");
            }
        }

        // ----------------------------------------------------------------
        // Public (no auth)
        // ----------------------------------------------------------------

        public async Task<SponsorInfo?> GetSponsorAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/sponsor", ct);
                if (!resp.IsSuccessStatusCode) return null;
                var text = await resp.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<SponsorInfo>(text, _json);
            }
            catch { return null; }
        }

        /// <summary>
        /// Skin ids this character owns. Returns null when the answer is
        /// UNKNOWN -- offline, unauthenticated, server error -- which the
        /// caller must not confuse with "owns nothing". Treating a failed
        /// request as an empty list would strip a pilot of a skin they paid
        /// for the moment their connection dropped.
        /// </summary>
        public async Task<List<string>?> GetMySkinsAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await SendAuthedAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/skins/me"), ct);
                if (!resp.IsSuccessStatusCode) return null;
                var text = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("skins", out var arr) ||
                    arr.ValueKind != JsonValueKind.Array) return null;
                var outList = new List<string>();
                foreach (var e in arr.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String) outList.Add(e.GetString()!);
                return outList;
            }
            catch { return null; }
        }

        public async Task<VersionInfo?> GetVersionAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/version", ct);
                if (!resp.IsSuccessStatusCode) return null;
                var text = await resp.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<VersionInfo>(text, _json);
            }
            catch { return null; }
        }

        // ----------------------------------------------------------------
        // Auth helper — attaches the bearer token, retries once (with a
        // forced-refresh token) on 401.
        // ----------------------------------------------------------------

        private async Task<HttpResponseMessage> SendAuthedAsync(
            Func<HttpRequestMessage> buildRequest, CancellationToken ct)
        {
            var token = await _getToken(false);
            var resp  = await SendWithTokenAsync(buildRequest(), token, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                resp.Dispose();
                var freshToken = await _getToken(true);
                resp = await SendWithTokenAsync(buildRequest(), freshToken, ct);
            }

            return resp;
        }

        private async Task<HttpResponseMessage> SendWithTokenAsync(
            HttpRequestMessage req, string? token, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await _http.SendAsync(req, ct);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode) return;

            var body = await resp.Content.ReadAsStringAsync();
            var code = (int)resp.StatusCode;

            // Prefer the backend's own {"error":"..."} message when present — pilots should
            // see "you already have 7.00 with Caldari State", not the raw JSON wrapper around
            // it. Fall back to the current behaviour when the body isn't JSON or has no
            // string "error" property.
            string message = string.IsNullOrEmpty(body) ? $"HTTP {code}" : $"HTTP {code}: {body}";
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.ValueKind == JsonValueKind.String)
                {
                    var errText = err.GetString();
                    if (!string.IsNullOrEmpty(errText))
                        message = errText;
                }
            }
            catch (JsonException) { /* not JSON — keep the raw-body fallback */ }

            throw new OverlayApiException(code, message);
        }

        // ----------------------------------------------------------------
        // Wire DTOs → display models
        // ----------------------------------------------------------------

        private static IntelReport ToIntelReport(IntelDto dto) => new()
        {
            System         = dto.System,
            TypeRaw        = dto.Type,
            Notes          = dto.Notes ?? "",
            Count          = 1,
            ReportedBy     = dto.ReporterCharacterId.ToString(),
            ReportedAtUnix = ParseUnixSeconds(dto.CreatedAt),
        };

        private static Mission ToMission(OrderDto dto) => new()
        {
            Id          = dto.Id,
            Title       = dto.Title,
            Description = dto.Description ?? "",
            CreatedBy   = dto.CreatedBy.ToString(),
            CreatedAt   = dto.CreatedAt ?? "",
            TargetScope = dto.TargetScope ?? "",
            TargetId    = dto.TargetId,
            ExpiresAt   = dto.ExpiresAt.HasValue ? DateTimeOffset.FromUnixTimeSeconds((long)dto.ExpiresAt.Value).ToString("u") : "",
            RewardAmount    = dto.RewardAmount,
            RewardType      = dto.RewardType ?? "",
            MaxParticipants = dto.MaxParticipants,
            SlotsTaken      = dto.SlotsTaken,
            MyState         = dto.MyState,
            MissionType = dto.MissionType ?? "freeform",
            TargetUnits = dto.TargetUnits,
            MyKills     = dto.MyKills,
            TotalKills  = dto.TotalKills,
            KillMinIsk            = dto.KillMinIsk,
            KillFinalBlowOnly     = dto.KillFinalBlowOnly != 0,
            KillSystemIdsJson     = dto.KillSystemIds,
            KillVictimCharIdsJson = dto.KillVictimCharIds,
            KillVictimCorpIdsJson = dto.KillVictimCorpIds,
            KillVictimAllyIdsJson = dto.KillVictimAllyIds,
            StandingTargetType   = dto.StandingTargetType,
            StandingTargetId     = dto.StandingTargetId,
            StandingTargetName   = dto.StandingTargetName ?? "",
            StandingThreshold    = dto.StandingThreshold,
            StandingUseEffective = dto.StandingUseEffective != 0,
            StandingMustEarn     = dto.StandingMustEarn != 0,
            MyStandingAtJoin     = dto.MyStandingAtJoin,
        };

        private static double ParseUnixSeconds(string? iso)
        {
            if (string.IsNullOrEmpty(iso)) return 0;
            return DateTimeOffset.TryParse(
                iso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed.ToUnixTimeSeconds()
                : 0;
        }

        public void Dispose() => _http.Dispose();

        // ----------------------------------------------------------------
        // Wire shapes (exact JSON returned by the Worker)
        // ----------------------------------------------------------------

        private sealed class IntelDto
        {
            [JsonPropertyName("id")]                   public int     Id { get; set; }
            [JsonPropertyName("region_id")]             public int     RegionId { get; set; }
            [JsonPropertyName("system")]                public string  System { get; set; } = "";
            [JsonPropertyName("type")]                  public string  Type { get; set; } = "";
            [JsonPropertyName("notes")]                 public string? Notes { get; set; }
            [JsonPropertyName("reporter_character_id")] public long    ReporterCharacterId { get; set; }
            [JsonPropertyName("created_at")]            public string? CreatedAt { get; set; }
            [JsonPropertyName("expires_at")]            public double? ExpiresAt { get; set; }
        }

        private sealed class IntelListResponse
        {
            [JsonPropertyName("intel")] public List<IntelDto> Intel { get; set; } = new();
        }

        private sealed class OrderDto
        {
            [JsonPropertyName("id")]           public int     Id { get; set; }
            [JsonPropertyName("title")]        public string  Title { get; set; } = "";
            [JsonPropertyName("description")]  public string? Description { get; set; }
            [JsonPropertyName("target_scope")] public string? TargetScope { get; set; }
            [JsonPropertyName("target_id")]    public long?   TargetId { get; set; }
            [JsonPropertyName("created_by")]   public long    CreatedBy { get; set; }
            [JsonPropertyName("created_at")]   public string? CreatedAt { get; set; }
            [JsonPropertyName("expires_at")]   public double? ExpiresAt { get; set; }

            [JsonPropertyName("reward_amount")]    public double  RewardAmount { get; set; }
            [JsonPropertyName("reward_type")]      public string? RewardType { get; set; }
            [JsonPropertyName("max_participants")] public int?    MaxParticipants { get; set; }
            [JsonPropertyName("slots_taken")]      public int     SlotsTaken { get; set; }
            [JsonPropertyName("my_state")]         public string? MyState { get; set; }

            [JsonPropertyName("mission_type")] public string? MissionType { get; set; }
            [JsonPropertyName("target_units")] public int?    TargetUnits { get; set; }
            [JsonPropertyName("my_kills")]     public int     MyKills { get; set; }
            [JsonPropertyName("total_kills")]  public int     TotalKills { get; set; }

            // Bounty qualification criteria — the *_ids columns are JSON-array TEXT in D1.
            [JsonPropertyName("kill_min_isk")]              public double  KillMinIsk { get; set; }
            [JsonPropertyName("kill_final_blow_only")]      public int     KillFinalBlowOnly { get; set; }
            [JsonPropertyName("kill_system_ids")]           public string? KillSystemIds { get; set; }
            [JsonPropertyName("kill_victim_character_ids")] public string? KillVictimCharIds { get; set; }
            [JsonPropertyName("kill_victim_corp_ids")]      public string? KillVictimCorpIds { get; set; }
            [JsonPropertyName("kill_victim_alliance_ids")]  public string? KillVictimAllyIds { get; set; }

            // Standing goal. *_use_effective / *_must_earn are INTEGER 0|1 in D1.
            [JsonPropertyName("standing_target_type")]   public string? StandingTargetType { get; set; }
            [JsonPropertyName("standing_target_id")]     public long?   StandingTargetId { get; set; }
            [JsonPropertyName("standing_target_name")]   public string? StandingTargetName { get; set; }
            [JsonPropertyName("standing_threshold")]     public double? StandingThreshold { get; set; }
            [JsonPropertyName("standing_use_effective")] public int     StandingUseEffective { get; set; } = 1;
            [JsonPropertyName("standing_must_earn")]     public int     StandingMustEarn { get; set; }
            [JsonPropertyName("my_standing_at_join")]    public double? MyStandingAtJoin { get; set; }
        }

        private sealed class OrderListResponse
        {
            [JsonPropertyName("missions")] public List<OrderDto> Missions { get; set; } = new();
        }
    }
}

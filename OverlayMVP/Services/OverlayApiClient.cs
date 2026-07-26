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
            throw new OverlayApiException(code, string.IsNullOrEmpty(body) ? $"HTTP {code}" : $"HTTP {code}: {body}");
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
        }

        private sealed class OrderListResponse
        {
            [JsonPropertyName("missions")] public List<OrderDto> Missions { get; set; } = new();
        }
    }
}

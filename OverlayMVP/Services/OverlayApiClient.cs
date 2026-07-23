// filename: Services/OverlayApiClient.cs
using System;
using System.Collections.Generic;
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
    public sealed class OverlayApiClient : IDisposable
    {
        // ----------------------------------------------------------------
        // JSON options — camelCase from Python/FastAPI backend
        // ----------------------------------------------------------------
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient _http;
        private readonly string     _baseUrl;
        private readonly string     _token;

        public OverlayApiClient(string baseUrl, string token)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _token   = token;

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
        }

        // ----------------------------------------------------------------
        // Missions
        // ----------------------------------------------------------------

        public async Task<List<Mission>> GetMissionsAsync(
            string? status = null,
            CancellationToken ct = default)
        {
            var url = $"{_baseUrl}/overlay/api/v1/missions";
            if (status is not null) url += $"?status={Uri.EscapeDataString(status)}";

            var resp = await _http.GetAsync(url, ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<MissionListResponse>(text, _json);
            return data?.Missions ?? new List<Mission>();
        }

        public async Task<List<FactionStanding>> GetStandingsAsync(
            CancellationToken ct = default)
        {
            try
            {
                var url  = $"{_baseUrl}/overlay/api/v1/standings";
                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return new List<FactionStanding>();
                var text = await resp.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<List<FactionStanding>>(text, _json)
                       ?? new List<FactionStanding>();
            }
            catch { return new List<FactionStanding>(); }
        }

        public async Task<Mission?> CreateMissionAsync(
            string title,
            string description = "",
            CancellationToken ct = default)
        {
            var url     = $"{_baseUrl}/overlay/api/v1/missions";
            var payload = new { title, description };
            var json    = JsonSerializer.Serialize(payload, _json);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var resp    = await _http.PostAsync(url, content, ct);
            await EnsureSuccessAsync(resp);
            var text = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<Mission>(text, _json);
        }

        public async Task<Mission?> AssignMissionAsync(int missionId, CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/missions/{missionId}/assign";
            var resp = await _http.PostAsync(url, null, ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<Mission>(text, _json);
        }

        public async Task<Mission?> CompleteMissionAsync(int missionId, CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/missions/{missionId}/complete";
            var resp = await _http.PostAsync(url, null, ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<Mission>(text, _json);
        }

        // ----------------------------------------------------------------
        // Character / ESI (proxied via backend — token never leaves server)
        // ----------------------------------------------------------------

        public async Task<CharacterInfo?> GetCharacterAsync(CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/character";
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CharacterInfo>(text, _json);
        }

        // ----------------------------------------------------------------
        // Intel reports
        // ----------------------------------------------------------------

        public async Task<List<IntelReport>> GetIntelAsync(CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/intel";
            var resp = await _http.GetAsync(url, ct);
            await EnsureSuccessAsync(resp);

            var text = await resp.Content.ReadAsStringAsync(ct);
            var list = JsonSerializer.Deserialize<List<IntelReport>>(text, _json);
            return list ?? new List<IntelReport>();
        }

        public async Task PostIntelAsync(
            string system,
            IntelType type,
            int count,
            string notes,
            CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/intel";
            var body = JsonSerializer.Serialize(new
            {
                system,
                type   = type.ToString().ToLowerInvariant(),
                count,
                notes
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            var resp = await _http.SendAsync(req, ct);
            await EnsureSuccessAsync(resp);
        }

        // ----------------------------------------------------------------
        // Full snapshot (missions + character + intel in one call)
        // ----------------------------------------------------------------

        // ── EVE SSO link status ──────────────────────────────────────────
        public async Task<(bool linked, string? characterName)> GetEveStatusAsync(
            CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/overlay/api/v1/eve/status", ct);
                if (!resp.IsSuccessStatusCode) return (false, null);
                var text = await resp.Content.ReadAsStringAsync(ct);
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                var root = doc.RootElement;
                bool linked = root.TryGetProperty("linked", out var l) && l.GetBoolean();
                string? name = root.TryGetProperty("character_name", out var n)
                               ? n.GetString() : null;
                return (linked, name);
            }
            catch { return (false, null); }
        }

        // ── Connectivity check ───────────────────────────────────────────
        public async Task<(bool ok, string detail)> PingAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/overlay/api/v1/health", ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                return ((int)resp.StatusCode < 500, $"{(int)resp.StatusCode}: {body}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<OverlayDataResponse?> GetSnapshotAsync(CancellationToken ct = default)
        {
            var url  = $"{_baseUrl}/overlay/api/v1/snapshot";
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Backend doesn't have snapshot endpoint yet — fall back to individual calls
                var missions  = await GetMissionsAsync(ct: ct);
                var character = await GetCharacterAsync(ct);
                var intel     = await GetIntelAsync(ct);
                return new OverlayDataResponse
                {
                    Missions  = missions,
                    Character = character,
                    Intel     = intel
                };
            }

            await EnsureSuccessAsync(resp);
            var text = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OverlayDataResponse>(text, _json);
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static async Task EnsureSuccessAsync(HttpResponseMessage resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var code = (int)resp.StatusCode;
                var hint = code switch {
                    401 => " (token invalide ou expiré — re-pair requis)",
                    503 => " (python-jose manquant sur le bot — voir requirements.txt)",
                    0   => " (hôte introuvable — vérifier l'URL dans les paramètres)",
                    _   => ""
                };
                throw new Exception($"HTTP {code}{hint}: {body}");
            }
        }

        public void Dispose() => _http.Dispose();
    }

    // Reuse the same response wrapper as in MissionModels
    internal sealed class MissionListResponse
    {
        [JsonPropertyName("missions")]
        public List<Mission> Missions { get; set; } = new();
    }
}

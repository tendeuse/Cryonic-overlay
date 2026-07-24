// filename: Services/BackendSession.cs
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    public sealed record BackendClaims(int CharacterId, string Name, string Role, bool CanPostIntel);

    /// <summary>
    /// Exchanges a character's ESI access token for a backend JWT (POST /auth/verify)
    /// and caches it per character. The overlay never sends ESI refresh tokens anywhere.
    /// </summary>
    public sealed class BackendSession : IDisposable
    {
        public const string ApiBase = "https://cryonic-intel-api.tendeuse-overlay.workers.dev/api/v1";

        private static readonly JsonSerializerOptions _json = new()
        { PropertyNameCaseInsensitive = true };

        private sealed class Entry
        {
            public string Token = "";
            public DateTime ExpiresAt = DateTime.MinValue;
            public BackendClaims? Claims;
        }

        private readonly ConcurrentDictionary<int, Entry> _byChar = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private readonly EsiClient _esi;

        public BackendSession(EsiClient esi) { _esi = esi; }

        public BackendClaims? ClaimsFor(int characterId) =>
            _byChar.TryGetValue(characterId, out var e) ? e.Claims : null;

        public bool IsSuperUser(int characterId)
        {
            var r = ClaimsFor(characterId)?.Role;
            return r is "global" or "coalition" or "ceo";
        }

        public void Invalidate(int characterId) => _byChar.TryRemove(characterId, out _);

        /// <summary>Valid backend JWT for this character, minting/refreshing as needed. Null on failure.</summary>
        public async Task<string?> GetTokenAsync(EsiToken character, bool forceRefresh = false,
                                                 CancellationToken ct = default)
        {
            if (character is null) return null;
            if (!forceRefresh
                && _byChar.TryGetValue(character.CharacterId, out var cached)
                && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
                return cached.Token;

            await _gate.WaitAsync(ct);
            try
            {
                // Re-check the cache now that we hold the gate: another caller
                // may have minted a fresh token while we were waiting.
                if (!forceRefresh
                    && _byChar.TryGetValue(character.CharacterId, out var cachedInLock)
                    && cachedInLock.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
                    return cachedInLock.Token;

                // A fresh ESI access token (EsiClient refreshes it if needed).
                string accessToken = await _esi.GetValidAccessTokenAsync(character, ct);
                if (string.IsNullOrEmpty(accessToken)) return null;

                var payload = JsonSerializer.Serialize(new { eve_access_token = accessToken });
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/auth/verify")
                { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return null;

                var body = await resp.Content.ReadAsStringAsync(ct);
                var doc  = JsonSerializer.Deserialize<VerifyResponse>(body, _json);
                if (doc is null || string.IsNullOrEmpty(doc.Token)) return null;

                _byChar[character.CharacterId] = new Entry
                {
                    Token     = doc.Token,
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(doc.ExpiresAt).UtcDateTime,
                    Claims    = new BackendClaims(doc.CharacterId, doc.Name ?? "", doc.Role ?? "pilot", doc.CanPostIntel),
                };
                return doc.Token;
            }
            catch { return null; }
            finally { _gate.Release(); }
        }

        private sealed class VerifyResponse
        {
            [JsonPropertyName("token")]          public string Token { get; set; } = "";
            [JsonPropertyName("expires_at")]     public long   ExpiresAt { get; set; }
            [JsonPropertyName("character_id")]   public int    CharacterId { get; set; }
            [JsonPropertyName("name")]           public string? Name { get; set; }
            [JsonPropertyName("role")]           public string? Role { get; set; }
            [JsonPropertyName("can_post_intel")] public bool   CanPostIntel { get; set; }
        }

        public void Dispose() { _http.Dispose(); _gate.Dispose(); }
    }
}

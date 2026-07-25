// filename: Services/ZkillClient.cs
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    /// <summary>
    /// Counts a character's kills from zKillboard's PUBLIC API, so the overlay does
    /// not have to request the killmail ESI scope. Failures are non-fatal (return 0).
    /// </summary>
    public sealed class ZkillClient : IDisposable
    {
        private const string Base = "https://zkillboard.com/api";
        private readonly HttpClient _http;

        public ZkillClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            // zKillboard asks every client to identify itself.
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("CryonicOverlay/0.6 (EVE third-party tool)");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        /// <summary>Kills by this character with a killmail time at/after sinceUtc. 0 on any failure.</summary>
        public async Task<int> GetKillCountSinceAsync(int characterId, DateTime sinceUtc,
                                                      CancellationToken ct = default)
        {
            try
            {
                var url  = $"{Base}/kills/characterID/{characterId}/";
                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return 0;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;

                int count = 0;
                foreach (var km in doc.RootElement.EnumerateArray())
                {
                    if (!km.TryGetProperty("killmail_time", out var t)) continue;
                    var s = t.GetString();
                    if (string.IsNullOrEmpty(s)) continue;
                    if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when)
                        && when >= sinceUtc)
                        count++;
                }
                return count;
            }
            catch { return 0; }
        }

        public void Dispose() => _http.Dispose();
    }
}

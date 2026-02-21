// filename: Services/PairingClient.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    public sealed class PairingClient
    {
        private readonly HttpClient _http = new HttpClient();

        public async Task<(string token, string expiresAt)> ExchangeCodeAsync(string apiBaseUrl, string code)
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/overlay/api/v1/pair/exchange";

            var body = JsonSerializer.Serialize(new { code = code.Trim() });
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Pair exchange failed ({(int)resp.StatusCode}): {text}");

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var token = root.GetProperty("token").GetString() ?? "";
            var expiresAt = root.GetProperty("expires_at").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("Pair exchange returned empty token.");

            return (token, expiresAt);
        }
    }
}

// filename: Services/EsiHttp.cs
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OverlayMVP.Services
{
    /// <summary>
    /// One place for every rule ESI asks third-party clients to follow.
    ///
    /// There were four HttpClients in this app talking to CCP, each configured
    /// differently: the main authenticated one sent no User-Agent at all, and
    /// one still announced itself as version 0.6. Nobody handled HTTP 429.
    /// Rules that live in four places are rules that hold in none of them.
    /// </summary>
    public static class EsiHttp
    {
        /// <summary>
        /// Unversioned base. CCP merged v1..vN, "dev", "latest" and "legacy"
        /// into a single surface; paths are now version-free and the wanted
        /// behaviour is selected by X-Compatibility-Date instead.
        /// </summary>
        public const string Base = "https://esi.evetech.net";

        /// <summary>
        /// PINNED ON PURPOSE. CCP suggest "today's date", which for a shipped
        /// binary means the API can change shape under a build that was never
        /// tested against it -- the exact surprise the header exists to
        /// prevent. This is a date we have tested; bump it deliberately after
        /// checking the newer behaviour, never automatically.
        ///
        /// Chosen after the 2026-06-09 /characters change that renamed
        /// title_id to corporation_title_id. We read neither field, so that
        /// rename does not affect us.
        /// </summary>
        public const string CompatibilityDate = "2026-08-08";

        /// <summary>
        /// CCP strongly prefer a contact route in the User-Agent. The repo URL
        /// is used rather than a personal email: it is already public, it
        /// reaches the same person, and it does not ship someone's inbox
        /// address inside a binary strangers download.
        /// </summary>
        public static string UserAgent { get; } = BuildUserAgent();

        private static string BuildUserAgent()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            var version = v is null ? "0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            return $"CryonicOverlay/{version} (+https://github.com/tendeuse/Cryonic-overlay)";
        }

        /// <summary>Apply the standard headers to a client. Call once, at construction.</summary>
        public static void Configure(HttpClient http)
        {
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Remove("X-Compatibility-Date");
            http.DefaultRequestHeaders.Add("X-Compatibility-Date", CompatibilityDate);
        }

        // ── Rate limiting ─────────────────────────────────────────────────
        //
        // ESI runs a token bucket per rate-limit-group / application /
        // character. A 2XX costs 2 tokens, a 3XX costs 1, and a 4XX costs
        // FIVE; they come back after 15 minutes. So the dangerous case is not
        // steady polling, it is a loop of failures -- an expired token turning
        // every call into a 401 burns budget two and a half times faster than
        // success does, and retrying harder is precisely wrong.
        //
        // The pause is process-wide rather than per-client. Being told to slow
        // down on one endpoint while three other HttpClients keep hammering
        // would defeat the point.

        private static DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
        private static readonly object _gate = new();

        /// <summary>How long callers should hold off, or zero. Exposed for status/diagnostics.</summary>
        public static TimeSpan PauseRemaining
        {
            get
            {
                lock (_gate)
                {
                    var left = _pausedUntil - DateTimeOffset.UtcNow;
                    return left > TimeSpan.Zero ? left : TimeSpan.Zero;
                }
            }
        }

        private static void PauseFor(TimeSpan d)
        {
            lock (_gate)
            {
                var until = DateTimeOffset.UtcNow + d;
                if (until > _pausedUntil) _pausedUntil = until;
            }
        }

        /// <summary>
        /// Send a request, honouring 429 and Retry-After.
        ///
        /// Takes a FACTORY, not a request: an HttpRequestMessage cannot be sent
        /// twice, so a retry needs a fresh one.
        ///
        /// Returns null when the call could not be completed. Callers already
        /// treat null as "could not tell" and leave their cached state alone,
        /// which is the right behaviour when we are being throttled.
        /// </summary>
        public static async Task<HttpResponseMessage?> SendAsync(
            HttpClient http, Func<HttpRequestMessage> make,
            CancellationToken ct = default, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var wait = PauseRemaining;
                if (wait > TimeSpan.Zero)
                {
                    // Someone was throttled recently. Do not add to it.
                    if (wait > TimeSpan.FromSeconds(30)) return null;
                    try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { return null; }
                }

                HttpResponseMessage resp;
                try { resp = await http.SendAsync(make(), ct); }
                catch (OperationCanceledException) { return null; }
                catch (HttpRequestException) { return null; }

                if (resp.StatusCode != HttpStatusCode.TooManyRequests) return resp;

                // 429. Believe Retry-After; fall back to a sane default.
                var retry = resp.Headers.RetryAfter?.Delta
                            ?? (resp.Headers.RetryAfter?.Date is { } d
                                    ? d - DateTimeOffset.UtcNow
                                    : TimeSpan.FromSeconds(60));
                if (retry < TimeSpan.Zero)      retry = TimeSpan.FromSeconds(60);
                if (retry > TimeSpan.FromMinutes(15)) retry = TimeSpan.FromMinutes(15);

                PauseFor(retry);
                resp.Dispose();

                // Only wait it out if it is short and we have attempts left.
                if (attempt == maxAttempts || retry > TimeSpan.FromSeconds(30)) return null;
            }
            return null;
        }
    }
}

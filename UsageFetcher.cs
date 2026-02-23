using System.Net;
using System.Text.Json;

namespace ClaudeUsageMonitor;

/// <summary>
/// Fetcht Usage-Daten von der Anthropic OAuth API.
/// 
/// Ein einziger HTTP-Call:
///   GET https://api.anthropic.com/api/oauth/usage
///   Authorization: Bearer {accessToken}
///   anthropic-beta: oauth-2025-04-20
///   User-Agent: claude-code/2.0.32
/// 
/// Das ist der gleiche Endpoint den das Bash-Gist von omachala nutzt.
/// Keine Org-ID nötig, kein Cookie, kein Browser.
/// </summary>
public sealed class UsageFetcher : IDisposable
{
    private readonly HttpClient _http;

    public UsageFetcher()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    /// <summary>
    /// Fetcht Usage-Daten mit dem gegebenen OAuth Access Token.
    /// </summary>
    public async Task<UsageData> FetchAsync(string accessToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/oauth/usage");
        req.Headers.Add("Authorization", $"Bearer {accessToken}");
        req.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        req.Headers.Add("User-Agent", "claude-code/2.0.32");

        var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("OAuth Token abgelaufen oder ungültig. Bitte 'claude login' ausführen.");

        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        System.Diagnostics.Debug.WriteLine($"[Usage] Response: {json}");

        return Parse(json);
    }

    private static UsageData Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var data = new UsageData { FetchedAt = DateTime.Now };

        // five_hour
        if (root.TryGetProperty("five_hour", out var fh))
        {
            if (fh.TryGetProperty("utilization", out var u)) data.SessionPercent = u.GetDouble();
            if (fh.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(r.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    data.SessionResetsAt = dt.ToUniversalTime();
        }

        // seven_day
        if (root.TryGetProperty("seven_day", out var sd))
        {
            data.HasWeekly = true;
            if (sd.TryGetProperty("utilization", out var u)) data.WeeklyPercent = u.GetDouble();
            if (sd.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(r.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    data.WeeklyResetsAt = dt.ToUniversalTime();
        }

        // extra_usage (Pay-as-you-go)
        if (root.TryGetProperty("extra_usage", out var ex))
        {
            if (ex.TryGetProperty("is_enabled", out var en)) data.ExtraEnabled = en.GetBoolean();
            if (ex.TryGetProperty("utilization", out var u)) data.ExtraPercent = u.GetDouble();
            // API liefert Cents → in Dollar umrechnen
            if (ex.TryGetProperty("used_credits", out var uc)) data.ExtraUsedDollars = uc.GetDecimal() / 100m;
            if (ex.TryGetProperty("monthly_limit", out var ml)) data.ExtraLimitDollars = ml.GetDecimal() / 100m;
        }

        return data;
    }

    public void Dispose() => _http.Dispose();
}

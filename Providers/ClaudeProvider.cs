using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Gauge.Models;
using Gauge.Providers.Internal;

namespace Gauge.Providers;

/// <summary>
/// Reads Claude Code usage from Anthropic's official OAuth usage endpoint
/// (<c>GET https://api.anthropic.com/api/oauth/usage</c>) using the OAuth token the
/// CLI stores in <c>~/.claude/.credentials.json</c>. This returns the same real
/// figures Claude Code's <c>/usage</c> shows — actual 5-hour and weekly utilization
/// (0–100) and real reset times — unlike token-counting tools such as ccusage.
///
/// The plan label (Max 5x/20x, Pro, …) comes from the credentials file, so it is
/// reported even when the usage call fails. The usage call degrades gracefully: a
/// missing credential, expired token, or network error yields an empty window list
/// (the coordinator then keeps showing the last good snapshot).
/// </summary>
public sealed class ClaudeProvider : IUsageProvider
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    // Required beta header for the OAuth usage endpoint (per Claude Code's own client).
    private const string OAuthBetaHeader = "oauth-2025-04-20";

    private readonly HttpClient _http;

    public ClaudeProvider(HttpClient http) => _http = http;

    public string ToolName => "Claude Code";

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var credentials = ClaudeCredentials.Read();
        var windows = new List<UsageWindow>();

        if (credentials?.AccessToken is { Length: > 0 } token)
        {
            try
            {
                windows = await FetchWindowsAsync(token, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"[Gauge] ClaudeProvider usage fetch failed: {ex.Message}");
            }
        }

        return new UsageSnapshot
        {
            ToolName = ToolName,
            Plan = credentials?.Plan,
            Windows = windows,
            CapturedAt = DateTimeOffset.Now,
        };
    }

    private async Task<List<UsageWindow>> FetchWindowsAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        request.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBetaHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", "Gauge/1.0");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken);
        var root = document.RootElement;

        var windows = new List<UsageWindow>();
        if (ParseWindow(root, "five_hour", UsageWindowType.FiveHour, "5시간") is { } fiveHour)
        {
            windows.Add(fiveHour);
        }
        if (ParseWindow(root, "seven_day", UsageWindowType.Weekly, "주간") is { } weekly)
        {
            windows.Add(weekly);
        }

        return windows;
    }

    /// <summary>
    /// Parses one window object: <c>{ "utilization": 0–100, "resets_at": ISO8601 }</c>.
    /// A null/absent object (or null utilization) means the window has no data and is omitted.
    /// </summary>
    private static UsageWindow? ParseWindow(JsonElement root, string property, UsageWindowType type, string label)
    {
        if (root.GetObjectOrNull(property) is not { } window
            || window.GetDoubleOrNull("utilization") is not { } utilization)
        {
            return null;
        }

        return new UsageWindow
        {
            Type = type,
            UsedRatio = Math.Clamp(utilization / 100.0, 0.0, 1.0),
            Label = label,
            ResetTime = window.GetDateTimeOffsetOrNull("resets_at"),
        };
    }
}

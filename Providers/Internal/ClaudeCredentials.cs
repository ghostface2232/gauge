using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Gauge.Providers.Internal;

/// <summary>
/// Claude Code's OAuth credentials, read (read-only) from
/// <c>%USERPROFILE%\.claude\.credentials.json</c> — the same file the CLI writes and
/// refreshes. We never write it back, so we never race the CLI's own token rotation;
/// each refresh just re-reads the latest token the CLI has stored.
/// </summary>
internal sealed record ClaudeCredentials(string? AccessToken, DateTimeOffset? ExpiresAt, string? Plan)
{
    public static ClaudeCredentials? Read()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(home, ".claude", ".credentials.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetObjectOrNull("claudeAiOauth") is not { } oauth)
            {
                return null;
            }

            var token = oauth.GetStringOrNull("accessToken");
            var expiresAt = oauth.GetInt64OrNull("expiresAt") is { } ms
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : (DateTimeOffset?)null;
            var plan = MapPlan(oauth.GetStringOrNull("subscriptionType"), oauth.GetStringOrNull("rateLimitTier"));
            return new ClaudeCredentials(token, expiresAt, plan);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gauge] ClaudeCredentials.Read failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Maps the credential fields to a display label. <c>subscriptionType</c> is the
    /// tier ("max"/"pro"/"free"/…) and <c>rateLimitTier</c> distinguishes the Max
    /// variants (e.g. "default_claude_max_5x" / "…_20x").
    /// </summary>
    private static string? MapPlan(string? subscriptionType, string? rateLimitTier)
    {
        if (string.IsNullOrWhiteSpace(subscriptionType))
        {
            return null;
        }

        switch (subscriptionType.ToLowerInvariant())
        {
            case "max":
                if (rateLimitTier is not null && rateLimitTier.Contains("20x", StringComparison.OrdinalIgnoreCase))
                {
                    return "Max 20x";
                }
                if (rateLimitTier is not null && rateLimitTier.Contains("5x", StringComparison.OrdinalIgnoreCase))
                {
                    return "Max 5x";
                }
                return "Max";
            case "pro":
                return "Pro";
            case "free":
                return "Free";
            case "team":
                return "Team";
            case "enterprise":
                return "Enterprise";
            default:
                return char.ToUpperInvariant(subscriptionType[0]) + subscriptionType[1..];
        }
    }
}

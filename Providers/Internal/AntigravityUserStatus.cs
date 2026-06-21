using System.Text.Json;

namespace Gauge.Providers.Internal;

/// <summary>
/// Reads the plan label from a <c>GetUserStatus</c> response. The short plan name (e.g. "Pro")
/// lives at <c>userStatus.planStatus.planInfo.planName</c>; the human tier name
/// (e.g. "Google AI Pro") at <c>userStatus.userTier.name</c> is used as a fallback. Tolerant of
/// a response wrapper or its absence, and returns null for anything missing — the plan is a
/// best-effort label that never blocks the usage read.
/// </summary>
internal static class AntigravityUserStatus
{
    public static string? ParsePlan(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ParsePlan(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ParsePlan(JsonElement root)
    {
        if ((root.GetObjectOrNull("response") ?? root).GetObjectOrNull("userStatus") is not { } status)
        {
            return null;
        }

        var planName = status.GetObjectOrNull("planStatus")?.GetObjectOrNull("planInfo")?.GetStringOrNull("planName");
        return NonEmpty(planName) ?? NonEmpty(status.GetObjectOrNull("userTier")?.GetStringOrNull("name"));
    }

    private static string? NonEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

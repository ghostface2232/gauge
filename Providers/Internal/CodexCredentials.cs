using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Gauge.Providers.Internal;

/// <summary>
/// Codex's OAuth credentials, read (read-only) from <c>%USERPROFILE%\.codex\auth.json</c>
/// (or <c>$CODEX_HOME\auth.json</c>). The plan tier is not stored here — it comes from
/// the usage API response — so this only carries what the request needs.
/// </summary>
internal sealed record CodexCredentials(string? AccessToken, string? AccountId)
{
    public static CodexCredentials? Read()
    {
        try
        {
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                codexHome = Path.Combine(home, ".codex");
            }

            var path = Path.Combine(codexHome, "auth.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetObjectOrNull("tokens") is not { } tokens)
            {
                return null;
            }

            return new CodexCredentials(
                tokens.GetStringOrNull("access_token"),
                tokens.GetStringOrNull("account_id"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gauge] CodexCredentials.Read failed: {ex.Message}");
            return null;
        }
    }
}

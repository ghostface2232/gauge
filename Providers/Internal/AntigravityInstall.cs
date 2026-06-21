namespace Gauge.Providers.Internal;

/// <summary>
/// Locates the Antigravity installation. The IDE installs per-user under
/// <c>%LOCALAPPDATA%\Programs\Antigravity</c>; its language server lives at
/// <c>resources\bin\language_server.exe</c> beneath that root. The root is used to confirm a
/// candidate <c>language_server*.exe</c> actually belongs to Antigravity rather than an
/// unrelated process that happens to share the name.
/// </summary>
internal static class AntigravityInstall
{
    /// <summary>The install root if present on disk, else null (not installed).</summary>
    public static string? DefaultRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            return null;
        }

        var root = Path.Combine(localAppData, "Programs", "Antigravity");
        return Directory.Exists(root) ? root : null;
    }
}

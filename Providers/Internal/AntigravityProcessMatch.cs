namespace Gauge.Providers.Internal;

/// <summary>
/// Decides whether a running process is an Antigravity language server worth talking to.
/// The binary name changes across versions (<c>language_server.exe</c> now,
/// <c>language_server_windows_x64.exe</c> before), so matching is by prefix, not an exact
/// name. Two further checks reject look-alikes: the executable must sit under the Antigravity
/// install root (when known), and it must carry a <c>--csrf_token</c> — without that token the
/// server returns 401 and is useless to us anyway.
/// </summary>
internal static class AntigravityProcessMatch
{
    public static bool IsCandidate(string? executablePath, string? installRoot, AntigravityCommandLine commandLine)
    {
        if (string.IsNullOrEmpty(executablePath) || !IsLanguageServerName(Path.GetFileName(executablePath)))
        {
            return false;
        }

        // When the install root is known, require the executable to live under it. If it is
        // unknown (install not located), fall back to name + token rather than reject outright.
        if (!string.IsNullOrEmpty(installRoot) && !IsUnder(installRoot, executablePath))
        {
            return false;
        }

        return !string.IsNullOrEmpty(commandLine.GetValue("--csrf_token"));
    }

    private static bool IsLanguageServerName(string fileName)
        => fileName.StartsWith("language_server", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string root, string path)
    {
        string fullRoot, fullPath;
        try
        {
            fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            fullPath = Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }
}

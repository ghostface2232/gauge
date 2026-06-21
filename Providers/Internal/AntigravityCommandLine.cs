using System.Runtime.InteropServices;

namespace Gauge.Providers.Internal;

/// <summary>
/// Parses a Windows process command line into its arguments and reads named flags from it.
/// Antigravity's language server carries everything Gauge needs to talk to it — the CSRF
/// token and port mode — as <c>--flag value</c> pairs on its command line.
///
/// Splitting uses the OS's own <c>CommandLineToArgvW</c> so quoting matches exactly what the
/// process saw (a quoted install path with spaces must not be split). The CSRF token is a
/// secret, so <see cref="ToString"/> masks it: the value is available through
/// <see cref="GetValue"/> for immediate use but never leaks into a logged command line.
/// </summary>
internal sealed class AntigravityCommandLine
{
    // Flags whose values are secrets and must be masked in any string rendering.
    private static readonly string[] SensitiveFlags = { "--csrf_token", "--api_key" };

    private readonly IReadOnlyList<string> _arguments;

    public AntigravityCommandLine(string? commandLine)
    {
        _arguments = Split(commandLine);
    }

    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>The value following <paramref name="flag"/>, or null if the flag is absent or last.</summary>
    public string? GetValue(string flag)
    {
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (string.Equals(_arguments[i], flag, StringComparison.Ordinal))
            {
                return _arguments[i + 1];
            }
        }

        return null;
    }

    public bool HasFlag(string flag) => _arguments.Contains(flag, StringComparer.Ordinal);

    /// <summary>Diagnostic rendering with secret flag values masked. Never the raw token.</summary>
    public override string ToString()
    {
        var parts = new List<string>(_arguments.Count);
        for (var i = 0; i < _arguments.Count; i++)
        {
            var value = i > 0 && SensitiveFlags.Contains(_arguments[i - 1], StringComparer.Ordinal)
                ? "***"
                : _arguments[i];
            parts.Add(value.Contains(' ') ? $"\"{value}\"" : value);
        }

        return string.Join(' ', parts);
    }

    private static IReadOnlyList<string> Split(string? commandLine)
    {
        // CommandLineToArgvW returns the current process path for an empty string, so guard it.
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return Array.Empty<string>();
        }

        var argv = CommandLineToArgvW(commandLine, out var count);
        if (argv == IntPtr.Zero)
        {
            return Array.Empty<string>();
        }

        try
        {
            var result = new string[count];
            for (var i = 0; i < count; i++)
            {
                var element = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                result[i] = Marshal.PtrToStringUni(element) ?? string.Empty;
            }

            return result;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}

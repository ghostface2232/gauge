using Gauge.Providers.Internal;

namespace Gauge.Tests;

/// <summary>
/// Argument splitting and flag reading for <see cref="AntigravityCommandLine"/>, using the real
/// language server command line. Splitting must honor Windows quoting (a quoted install path with
/// spaces stays one argument) and the CSRF token must never appear in a rendered string.
/// </summary>
public sealed class AntigravityCommandLineTests
{
    private const string Token = "11111111-2222-3333-4444-555555555555";

    // The observed standalone-hub command line (token substituted).
    private static string LiveCommandLine =>
        "\"C:\\Users\\me\\AppData\\Local\\Programs\\Antigravity\\resources\\bin\\language_server.exe\" " +
        "--standalone --override_ide_name antigravity --subclient_type hub " +
        "--override_ide_version 2.1.4 --https_server_port 0 " +
        $"--csrf_token {Token} --app_data_dir antigravity --enable_sidecars";

    [Fact]
    public void ReadsFlagValuesFromLiveCommandLine()
    {
        var command = new AntigravityCommandLine(LiveCommandLine);

        Assert.Equal(Token, command.GetValue("--csrf_token"));
        Assert.Equal("0", command.GetValue("--https_server_port"));
        Assert.Equal("hub", command.GetValue("--subclient_type"));
        Assert.True(command.HasFlag("--standalone"));   // a valueless flag
        Assert.True(command.HasFlag("--enable_sidecars"));
    }

    [Fact]
    public void KeepsQuotedPathWithSpacesAsOneArgument()
    {
        const string commandLine =
            "\"C:\\Program Files\\Antigravity\\resources\\bin\\language_server.exe\" --csrf_token abc";
        var command = new AntigravityCommandLine(commandLine);

        Assert.Equal("C:\\Program Files\\Antigravity\\resources\\bin\\language_server.exe", command.Arguments[0]);
        Assert.Equal("abc", command.GetValue("--csrf_token"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyCommandLineYieldsNoArguments(string? commandLine)
    {
        Assert.Empty(new AntigravityCommandLine(commandLine).Arguments);
    }

    [Fact]
    public void MissingOrTrailingFlagReturnsNull()
    {
        Assert.Null(new AntigravityCommandLine("--standalone --csrf_token").GetValue("--csrf_token")); // trailing
        Assert.Null(new AntigravityCommandLine("--standalone").GetValue("--csrf_token"));               // absent
    }

    [Fact]
    public void ToStringMasksTheCsrfToken()
    {
        var rendered = new AntigravityCommandLine(LiveCommandLine).ToString();

        Assert.DoesNotContain(Token, rendered);
        Assert.Contains("--csrf_token ***", rendered);
        // Non-secret flags are preserved for diagnostics.
        Assert.Contains("--subclient_type hub", rendered);
    }
}

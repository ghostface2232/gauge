using Gauge.Providers.Internal;

namespace Gauge.Tests;

/// <summary>
/// Candidate identification for <see cref="AntigravityProcessMatch"/>: prefix name match across
/// versions, install-root containment to reject look-alikes, and a required CSRF token.
/// </summary>
public sealed class AntigravityProcessMatchTests
{
    private const string Root = @"C:\Users\me\AppData\Local\Programs\Antigravity";
    private static readonly string CurrentExe = Path.Combine(Root, @"resources\bin\language_server.exe");
    private static readonly string LegacyExe = Path.Combine(Root, @"resources\bin\language_server_windows_x64.exe");

    private static AntigravityCommandLine WithToken() => new("language_server.exe --standalone --csrf_token abc");
    private static AntigravityCommandLine WithoutToken() => new("language_server.exe --standalone");

    [Fact]
    public void AcceptsCurrentAndLegacyServerUnderInstallRoot()
    {
        Assert.True(AntigravityProcessMatch.IsCandidate(CurrentExe, Root, WithToken()));
        Assert.True(AntigravityProcessMatch.IsCandidate(LegacyExe, Root, WithToken()));
    }

    [Fact]
    public void RejectsProcessWithoutCsrfToken()
    {
        Assert.False(AntigravityProcessMatch.IsCandidate(CurrentExe, Root, WithoutToken()));
    }

    [Theory]
    [InlineData(@"C:\Tools\language_server.exe")]                 // look-alike outside the install root
    [InlineData(@"C:\Users\me\AppData\Local\Programs\Other\language_server.exe")]
    public void RejectsServerOutsideInstallRoot(string exePath)
    {
        Assert.False(AntigravityProcessMatch.IsCandidate(exePath, Root, WithToken()));
    }

    [Theory]
    [InlineData(@"C:\X\notlanguage_server.exe")] // wrong prefix
    [InlineData(@"C:\X\language_server.dll")]    // wrong extension
    [InlineData(null)]
    [InlineData("")]
    public void RejectsNonServerExecutables(string? exePath)
    {
        Assert.False(AntigravityProcessMatch.IsCandidate(exePath, installRoot: null, WithToken()));
    }

    [Fact]
    public void FallsBackToNameAndTokenWhenInstallRootUnknown()
    {
        // Install not located: name + token is enough; don't reject a real server outright.
        Assert.True(AntigravityProcessMatch.IsCandidate(@"C:\Anywhere\language_server.exe", installRoot: null, WithToken()));
    }
}

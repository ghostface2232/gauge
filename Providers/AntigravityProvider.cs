using Gauge.Models;
using Gauge.Providers.Internal;

namespace Gauge.Providers;

/// <summary>
/// Raised when Antigravity is installed but no engine could be reached or it returned no usable
/// usage (signed out, still warming, a transport error, or an availability-only response). It is
/// a provider failure, so the coordinator keeps the last good snapshot instead of clearing the
/// card — distinct from "not installed", which is a clean empty result.
/// </summary>
public sealed class AntigravityUnavailableException : Exception
{
    public AntigravityUnavailableException(string message) : base(message)
    {
    }
}

/// <summary>
/// Reads Antigravity usage from its local language server. Unlike the other tools there is no
/// public usage endpoint and no Gauge-owned credential: the data comes from the engine the IDE
/// runs (attach) or, when the IDE is closed, from an engine Gauge launches itself (delegate),
/// which authenticates with the on-disk login. All of that — process discovery, the loopback
/// Connect transport, the delegate engine lifecycle, and parsing — is isolated behind this
/// provider; callers see only a normalized <see cref="UsageSnapshot"/>.
///
/// Failure handling follows the coordinator's contract: not installed is a clean empty snapshot;
/// every other unusable outcome throws so the last good snapshot is preserved rather than
/// overwritten with nothing.
/// </summary>
public sealed class AntigravityProvider : IUsageProvider, IDisposable
{
    private readonly IReadOnlyList<IAntigravityReader> _sources;
    private readonly Func<bool> _isInstalled;
    private readonly IDisposable[] _owned;

    public AntigravityProvider()
    {
        var installRoot = AntigravityInstall.DefaultRoot();
        var client = new AntigravityLoopbackClient();
        var host = new AntigravityEngineHost(installRoot);

        // Attach to a running IDE first; fall back to launching the engine ourselves.
        _sources = new IAntigravityReader[]
        {
            new AntigravityAttachReader(new AntigravityProcessDiscovery(installRoot), client),
            new AntigravityDelegateReader(host),
        };
        _isInstalled = () => installRoot is not null
            && File.Exists(AntigravityInstall.EngineExecutablePath(installRoot));
        _owned = new IDisposable[] { host, client };
    }

    internal AntigravityProvider(IReadOnlyList<IAntigravityReader> sources, Func<bool> isInstalled)
    {
        _sources = sources;
        _isInstalled = isInstalled;
        _owned = Array.Empty<IDisposable>();
    }

    public ToolKind Tool => ToolKind.Antigravity;

    public string ToolName => ToolCatalog.For(ToolKind.Antigravity).DisplayName;

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        // Not installed: a clean "no data" state, not a failure.
        if (!_isInstalled())
        {
            return EmptySnapshot();
        }

        AntigravityReading? reading = null;
        foreach (var source in _sources)
        {
            reading = await source.ReadAsync(cancellationToken);
            if (reading is not null)
            {
                break;
            }
        }

        // Installed but no engine returned usage (signed out, warming, or a transport error):
        // fail so the coordinator keeps the last good snapshot rather than clearing the card.
        if (reading is null)
        {
            throw new AntigravityUnavailableException("No Antigravity engine returned usage.");
        }

        var windows = AntigravityQuotaParser.Parse(reading.QuotaJson);

        // An availability-only response with no known windows must not overwrite a useful cache.
        if (windows.Count == 0)
        {
            throw new AntigravityUnavailableException("Antigravity returned no usage windows.");
        }

        return new UsageSnapshot
        {
            ToolName = ToolName,
            Plan = reading.Plan,
            Windows = windows,
            CapturedAt = DateTimeOffset.Now,
        };
    }

    private UsageSnapshot EmptySnapshot() => new()
    {
        ToolName = ToolName,
        Plan = null,
        Windows = Array.Empty<UsageWindow>(),
        CapturedAt = DateTimeOffset.Now,
    };

    public void Dispose()
    {
        foreach (var disposable in _owned)
        {
            disposable.Dispose();
        }
    }
}

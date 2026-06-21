namespace Gauge.Providers.Internal;

/// <summary>
/// Reads usage from a running Antigravity IDE by attaching to its language server(s). Several
/// engines can be running (multiple IDE windows), but they share one account, so this queries
/// each and keeps the richest response — the one that parsed into the most usage windows —
/// guarding against an engine that is up but still warming and reporting a partial set.
/// </summary>
internal sealed class AntigravityAttachReader : IAntigravityReader
{
    private readonly AntigravityProcessDiscovery _discovery;
    private readonly AntigravityLoopbackClient _client;

    public AntigravityAttachReader(AntigravityProcessDiscovery discovery, AntigravityLoopbackClient client)
    {
        _discovery = discovery;
        _client = client;
    }

    public async Task<AntigravityReading?> ReadAsync(CancellationToken cancellationToken)
    {
        var readings = new List<AntigravityReading>();
        foreach (var server in _discovery.Discover())
        {
            if (server.ListeningPorts.Count == 0)
            {
                continue;
            }

            if (await _client.FetchReadingAsync(server.ListeningPorts, server.CsrfToken, cancellationToken) is { } reading)
            {
                readings.Add(reading);
            }
        }

        return PickRichest(readings);
    }

    /// <summary>The reading that parses into the most usage windows (null if none).</summary>
    internal static AntigravityReading? PickRichest(IReadOnlyList<AntigravityReading> readings)
    {
        AntigravityReading? best = null;
        var bestWindows = -1;
        foreach (var reading in readings)
        {
            var windows = AntigravityQuotaParser.Parse(reading.QuotaJson).Count;
            if (windows > bestWindows)
            {
                best = reading;
                bestWindows = windows;
            }
        }

        return best;
    }
}

/// <summary>
/// Reads usage by launching the engine ourselves (delegate mode) when no IDE is running. The
/// lifecycle — spawn, warm reuse, cleanup — lives in <see cref="AntigravityEngineHost"/>.
/// </summary>
internal sealed class AntigravityDelegateReader : IAntigravityReader
{
    private readonly AntigravityEngineHost _host;

    public AntigravityDelegateReader(AntigravityEngineHost host) => _host = host;

    public Task<AntigravityReading?> ReadAsync(CancellationToken cancellationToken)
        => _host.GetReadingAsync(cancellationToken);
}

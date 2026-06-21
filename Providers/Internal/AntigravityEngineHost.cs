using System.ComponentModel;
using System.Diagnostics;

namespace Gauge.Providers.Internal;

/// <summary>
/// Runs the Antigravity language server itself when the IDE is closed (delegate mode), so usage
/// can be refreshed without the app open — the engine authenticates with the login already on
/// disk, exactly like Claude/Codex delegated refresh, and Gauge never touches those credentials.
///
/// The engine is launched suspended, placed under a <see cref="JobObject"/>, then resumed, so it
/// and every sidecar it spawns belong to a job Gauge can tear down completely — and nothing
/// Gauge did not start is ever at risk. The engine is kept warm and reused across refreshes
/// (its quota cache lives inside that process, so re-spawning each cycle would force a slow cold
/// start), and released after an idle period. All access is serialized so a single engine is
/// shared safely across concurrent refreshes.
///
/// Returns null — never throws — for ordinary conditions: not installed, signed out, or a engine
/// that failed to become ready. The caller falls back to its last good snapshot.
/// </summary>
internal sealed class AntigravityEngineHost : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly string? _installRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AntigravityLoopbackClient? _client;
    private Engine? _engine;
    private bool _disposed;

    public AntigravityEngineHost(string? installRoot = null)
    {
        _installRoot = installRoot ?? AntigravityInstall.DefaultRoot();
    }

    /// <summary>
    /// Returns a usage reading from a Gauge-launched engine, reusing a warm one when possible,
    /// or null if the engine is unavailable (not installed / not ready).
    /// </summary>
    public async Task<AntigravityReading?> GetReadingAsync(CancellationToken cancellationToken)
    {
        if (_installRoot is null)
        {
            return null;
        }

        var enginePath = AntigravityInstall.EngineExecutablePath(_installRoot);
        if (!File.Exists(enginePath))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
            {
                return null;
            }

            // Reuse a warm engine. If it has wedged, tear it down and spawn a fresh one once.
            if (_engine is { IsAlive: true } warm)
            {
                if (await TryFetchAsync(warm, cancellationToken) is { } reused)
                {
                    warm.Touch();
                    return reused;
                }

                DisposeEngine();
            }

            var spawn = await SpawnAndWaitReadyAsync(enginePath, cancellationToken);
            if (spawn is not { } ready)
            {
                return null;
            }

            _engine = ready.Engine;
            ready.Engine.Touch();
            return ready.Reading;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Tears down the warm engine if it has been unused for longer than <paramref name="maxIdle"/>.</summary>
    public void ReleaseIfIdle(TimeSpan maxIdle)
    {
        // Don't block a refresh in progress: if the gate is held, the engine is in use, not idle.
        if (!_gate.Wait(0))
        {
            return;
        }

        try
        {
            if (_engine is { } engine && DateTime.UtcNow - engine.LastUsedUtc > maxIdle)
            {
                DisposeEngine();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ReadyEngine?> SpawnAndWaitReadyAsync(string enginePath, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString();
        var arguments = AntigravityEngineLaunch.BuildArguments(token, AntigravityInstall.ResolveIdeVersion(_installRoot!));
        var workingDirectory = Path.GetDirectoryName(enginePath);

        JobObject? job = null;
        SuspendedProcess? process = null;
        try
        {
            job = new JobObject();
            process = SuspendedProcess.Create(enginePath, arguments, workingDirectory);

            // Assign before the engine runs so its sidecars are born inside the job. If assigning
            // fails, the suspended process is terminated by its Dispose in the finally below.
            job.Assign(process.Handle);
            process.Resume();

            var client = Client();
            var deadline = DateTime.UtcNow + ReadyTimeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!process.IsAlive)
                {
                    break; // engine exited during startup (e.g. crash)
                }

                // Readiness is a real quota response, not just an open port — the server binds its
                // ports before it can actually answer.
                var ports = WindowsListeningPortTable.LoopbackListeningPorts(process.ProcessId);
                if (ports.Count > 0
                    && await client.FetchReadingAsync(ports, token, cancellationToken) is { } reading)
                {
                    var engine = new Engine(process, job, token, ports);
                    (process, job) = (null, null); // ownership transferred to the engine
                    return new ReadyEngine(engine, reading);
                }

                await Task.Delay(PollInterval, cancellationToken);
            }

            return null;
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"[Gauge] Antigravity engine spawn failed: {ex.Message}");
            return null;
        }
        finally
        {
            // Reached unless ownership was transferred to a returned Engine: close the job (which
            // kills the tree) and release/terminate the process.
            job?.Dispose();
            process?.Dispose();
        }
    }

    private async Task<AntigravityReading?> TryFetchAsync(Engine engine, CancellationToken cancellationToken)
    {
        try
        {
            return await Client().FetchReadingAsync(engine.Ports, engine.Token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[Gauge] Antigravity delegate fetch failed: {ex.GetType().Name}");
            return null;
        }
    }

    private AntigravityLoopbackClient Client() => _client ??= new AntigravityLoopbackClient();

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeEngine();
            _client?.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private readonly record struct ReadyEngine(Engine Engine, AntigravityReading Reading);

    private sealed class Engine : IDisposable
    {
        private readonly SuspendedProcess _process;
        private readonly JobObject _job;

        public Engine(SuspendedProcess process, JobObject job, string token, IReadOnlyList<int> ports)
        {
            _process = process;
            _job = job;
            Token = token;
            Ports = ports;
            LastUsedUtc = DateTime.UtcNow;
        }

        public string Token { get; }
        public IReadOnlyList<int> Ports { get; }
        public DateTime LastUsedUtc { get; private set; }
        public bool IsAlive => _process.IsAlive;

        public void Touch() => LastUsedUtc = DateTime.UtcNow;

        public void Dispose()
        {
            // Close the job first so KILL_ON_JOB_CLOSE terminates the engine and its sidecars,
            // then release our process handle.
            _job.Dispose();
            _process.Dispose();
        }
    }
}

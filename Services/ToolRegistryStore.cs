using System.Diagnostics;
using System.Text.Json;
using Gauge.Models;

namespace Gauge.Services;

/// <summary>Persists the set of tools the user has registered ("connected").</summary>
public interface IToolRegistryStore
{
    IReadOnlyCollection<ToolKind> Load();
    void Save(IReadOnlyCollection<ToolKind> enabled);
}

/// <summary>
/// Stores the registered tool set in <c>%APPDATA%\Gauge\settings.json</c>. Only the
/// registration (which tools are shown) is persisted — never tokens or credentials.
/// A missing/unreadable file falls back to the default set so first run shows the
/// established Claude Code + Codex experience.
/// </summary>
public sealed class ToolRegistryStore : IToolRegistryStore
{
    private static readonly IReadOnlyCollection<ToolKind> Default =
        new[] { ToolKind.ClaudeCode, ToolKind.Codex };

    private readonly Func<string> _directory;

    public ToolRegistryStore(Func<string>? directory = null)
        => _directory = directory ?? (() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gauge"));

    private string FilePath => Path.Combine(_directory(), "settings.json");

    public IReadOnlyCollection<ToolKind> Load()
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var dto = JsonSerializer.Deserialize<SettingsDto>(stream);
            if (dto?.EnabledTools is not { Count: > 0 } names)
            {
                return Default;
            }

            var kinds = names
                .Select(name => Enum.TryParse<ToolKind>(name, out var kind) ? (ToolKind?)kind : null)
                .OfType<ToolKind>()
                .Distinct()
                .ToList();
            return kinds.Count > 0 ? kinds : Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[Gauge] ToolRegistry load failed: {ex.GetType().Name}");
            return Default;
        }
    }

    public void Save(IReadOnlyCollection<ToolKind> enabled)
    {
        try
        {
            var directory = _directory();
            Directory.CreateDirectory(directory);

            var dto = new SettingsDto { EnabledTools = enabled.Select(kind => kind.ToString()).ToList() };
            var path = FilePath;
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(stream, dto, new JsonSerializerOptions { WriteIndented = true });
            }
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[Gauge] ToolRegistry save failed: {ex.GetType().Name}");
        }
    }

    private sealed class SettingsDto
    {
        public List<string>? EnabledTools { get; set; }
    }
}

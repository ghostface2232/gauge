namespace Gauge.Services;

/// <summary>
/// Persists whether the tray icon reacts to usage level, in
/// <c>%APPDATA%\Gauge\settings.json</c> via <see cref="AppSettingsFile"/>. The default is
/// enabled — a missing/absent key reads as on, so a settings file written before this toggle
/// existed keeps the prior dynamic behavior. When off, the tray shows only the base icon
/// (still tracking the light/dark taskbar theme). Saving leaves other keys (tool
/// registration, UI language, notifications, view mode) untouched.
/// </summary>
public sealed class DynamicTrayIconSettingsStore
{
    private readonly Func<string> _directory;

    public DynamicTrayIconSettingsStore(Func<string>? directory = null)
        => _directory = directory ?? (() => AppSettingsFile.DefaultDirectory);

    public bool Load() => AppSettingsFile.Load(_directory()).DynamicTrayIcon ?? true;

    public void Save(bool enabled)
        => AppSettingsFile.Save(_directory(), dto => dto.DynamicTrayIcon = enabled);
}

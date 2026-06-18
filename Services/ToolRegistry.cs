using Gauge.Models;

namespace Gauge.Services;

/// <summary>
/// The set of tools the user has registered. Registration is explicit (the settings
/// "+" button), not automatic on credential detection. Backed by an
/// <see cref="IToolRegistryStore"/> for persistence; raises <see cref="Changed"/> so
/// the usage pipeline and UI react to add/remove. Enabled/available lists follow
/// <see cref="ToolCatalog.All"/> declaration order.
/// </summary>
public sealed class ToolRegistry
{
    private readonly IToolRegistryStore _store;
    private readonly HashSet<ToolKind> _enabled;

    public ToolRegistry(IToolRegistryStore store)
    {
        _store = store;
        _enabled = new HashSet<ToolKind>(store.Load());
    }

    /// <summary>Raised after the registered set changes (add/remove), post-persist.</summary>
    public event EventHandler? Changed;

    public bool IsEnabled(ToolKind kind) => _enabled.Contains(kind);

    /// <summary>Registered tools, in catalog order.</summary>
    public IReadOnlyList<ToolKind> Enabled =>
        ToolCatalog.All.Select(descriptor => descriptor.Kind).Where(_enabled.Contains).ToList();

    /// <summary>Catalog tools not yet registered — the candidates for the "+" picker.</summary>
    public IReadOnlyList<ToolKind> Available =>
        ToolCatalog.All.Select(descriptor => descriptor.Kind).Where(kind => !_enabled.Contains(kind)).ToList();

    public bool Add(ToolKind kind)
    {
        if (!_enabled.Add(kind))
        {
            return false;
        }
        Persist();
        return true;
    }

    public bool Remove(ToolKind kind)
    {
        if (!_enabled.Remove(kind))
        {
            return false;
        }
        Persist();
        return true;
    }

    private void Persist()
    {
        _store.Save(_enabled);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

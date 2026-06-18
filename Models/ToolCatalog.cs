namespace Gauge.Models;

/// <summary>
/// Per-tool data that is the same wherever the tool is referenced: how its card is
/// labelled and how its official CLI is invoked for login. Keeping it here means a new
/// tool needs only a <see cref="ToolKind"/> case plus one entry in
/// <see cref="ToolCatalog"/> — the display name and login command no longer have to be
/// repeated (and kept in sync) across the providers, the auth state, and the UI.
/// </summary>
public sealed record ToolDescriptor
{
    public required ToolKind Kind { get; init; }

    /// <summary>Card label, e.g. "Claude Code", "Codex".</summary>
    public required string DisplayName { get; init; }

    /// <summary>Executable that performs an interactive login, e.g. "claude", "codex".</summary>
    public required string LoginCommand { get; init; }

    /// <summary>Arguments passed to <see cref="LoginCommand"/>, e.g. "/login", "login".</summary>
    public required string LoginArguments { get; init; }
}

/// <summary>The single source of truth for every tool Gauge knows about.</summary>
public static class ToolCatalog
{
    public static readonly ToolDescriptor ClaudeCode = new()
    {
        Kind = ToolKind.ClaudeCode,
        DisplayName = "Claude Code",
        LoginCommand = "claude",
        LoginArguments = "/login",
    };

    public static readonly ToolDescriptor Codex = new()
    {
        Kind = ToolKind.Codex,
        DisplayName = "Codex",
        LoginCommand = "codex",
        LoginArguments = "login",
    };

    /// <summary>Declaration order is the order tools are shown in the UI.</summary>
    public static readonly IReadOnlyList<ToolDescriptor> All = new[] { ClaudeCode, Codex };

    private static readonly IReadOnlyDictionary<ToolKind, ToolDescriptor> ByKind =
        All.ToDictionary(descriptor => descriptor.Kind);

    public static ToolDescriptor For(ToolKind kind) => ByKind[kind];
}

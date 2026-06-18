using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gauge.Models;
using Gauge.Services;

namespace Gauge.ViewModels;

public sealed partial class AuthenticationCardViewModel : ObservableObject
{
    private readonly IAuthenticationProvider _provider;

    private readonly ToolDescriptor _descriptor;

    public AuthenticationCardViewModel(IAuthenticationProvider provider)
    {
        _provider = provider;
        _descriptor = ToolCatalog.For(provider.Tool);
        ToolName = provider.State.ToolName;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsLoginRunning);
        RemoveCommand = new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));
        Apply(provider.State);
        provider.StateChanged += (_, state) => Apply(state);
    }

    /// <summary>The tool this card represents (used by the registry to remove it).</summary>
    public ToolKind Tool => _provider.Tool;

    public string ToolName { get; }
    public IAsyncRelayCommand LoginCommand { get; }

    /// <summary>Disconnects (removes) the tool from the registry via the settings VM.</summary>
    public IRelayCommand RemoveCommand { get; }
    public event EventHandler? RemoveRequested;

    /// <summary>
    /// True when the tool has a CLI login (Claude/Codex) so the card shows a login
    /// button. App-login tools (e.g. Cursor) hide it and rely on the status message,
    /// which already tells the user to sign in.
    /// </summary>
    public bool SupportsCliLogin => _descriptor.LoginKind == LoginKind.CliCommand;

    public event EventHandler? AuthenticationSucceeded;

    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string LoginButtonText { get; set; } = "로그인";
    [ObservableProperty] public partial bool IsLoginRunning { get; set; }

    private async Task LoginAsync()
    {
        var state = await _provider.LoginAsync();
        if (state.Status == AuthenticationStatus.Available)
        {
            AuthenticationSucceeded?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RefreshAsync() => Apply(await _provider.RefreshStateAsync());

    private void Apply(AuthenticationState state)
    {
        StatusText = state.Message;
        IsLoginRunning = state.IsLoginRunning;
        LoginButtonText = state.Status switch
        {
            AuthenticationStatus.LoginRunning => "로그인 중…",
            AuthenticationStatus.Available or AuthenticationStatus.Invalid => "계정 전환",
            _ => "로그인",
        };
        LoginCommand.NotifyCanExecuteChanged();
    }
}

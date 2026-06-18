using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gauge.Services;

namespace Gauge.ViewModels;

/// <summary>
/// Footer update control: shows the current version and a single action button
/// that checks GitHub Releases ("업데이트 확인") and, once a newer build is found,
/// switches to applying it ("업데이트"). Status text is surfaced as the button tooltip.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateService _service;
    private GitHubRelease? _available;

    public UpdateViewModel(UpdateService service)
    {
        _service = service;
        VersionText = service.CurrentVersion.ToString(3);
        ActionCommand = new AsyncRelayCommand(RunActionAsync, () => !IsBusy);
    }

    public string VersionText { get; }
    public IAsyncRelayCommand ActionCommand { get; }

    /// <summary>
    /// Raised after the installer has launched. The app should tear down and exit
    /// so the silent installer can replace its files and relaunch Gauge.
    /// </summary>
    public event EventHandler? ExitRequested;

    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }

    public string ActionButtonText => IsUpdateAvailable ? "업데이트" : "업데이트 확인";

    partial void OnIsBusyChanged(bool value) => ActionCommand.NotifyCanExecuteChanged();

    partial void OnIsUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(ActionButtonText));

    /// <summary>Quiet check at launch; leaves the status blank unless an update is found.</summary>
    public async Task CheckInBackgroundAsync()
    {
        if (IsBusy) return;
        ApplyResult(await _service.CheckAsync(), quiet: true);
    }

    private Task RunActionAsync() => IsUpdateAvailable ? UpdateAsync() : CheckAsync();

    private async Task CheckAsync()
    {
        IsBusy = true;
        StatusText = "업데이트 확인 중…";
        try
        {
            ApplyResult(await _service.CheckAsync(), quiet: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(UpdateCheckResult result, bool quiet)
    {
        switch (result.Status)
        {
            case UpdateStatus.UpdateAvailable:
                _available = result.Release;
                IsUpdateAvailable = true;
                StatusText = "업데이트 가능";
                break;
            case UpdateStatus.UpToDate:
                IsUpdateAvailable = false;
                StatusText = "최신 버전입니다";
                break;
            default:
                IsUpdateAvailable = false;
                if (!quiet) StatusText = "업데이트를 확인하지 못했습니다.";
                break;
        }
    }

    private async Task UpdateAsync()
    {
        if (_available is null) return;

        IsBusy = true;
        StatusText = "업데이트 다운로드 중…";
        if (await _service.DownloadAndLaunchAsync(_available))
        {
            StatusText = "업데이트를 설치하고 다시 시작합니다…";
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            StatusText = "업데이트 설치를 시작하지 못했습니다.";
            IsBusy = false;
        }
    }
}

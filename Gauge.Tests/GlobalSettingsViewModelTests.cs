using Gauge.Models;
using Gauge.ViewModels;

namespace Gauge.Tests;

public sealed class GlobalSettingsViewModelTests
{
    [Fact]
    public void ConstructorSetsInitialStateWithoutRaisingEvents()
    {
        var notifications = 0;
        var startup = 0;
        var viewModeChanges = 0;
        var vm = new GlobalSettingsViewModel(notificationsEnabled: true, startOnBoot: true, viewMode: UsageViewMode.Gauge);
        vm.NotificationsToggleRequested += (_, _) => notifications++;
        vm.StartOnBootToggleRequested += (_, _) => startup++;
        vm.ViewModeChangeRequested += (_, _) => viewModeChanges++;

        Assert.True(vm.NotificationsEnabled);
        Assert.True(vm.StartOnBoot);
        Assert.Equal((int)UsageViewMode.Gauge, vm.ViewModeIndex);
        Assert.Equal(0, notifications);
        Assert.Equal(0, startup);
        Assert.Equal(0, viewModeChanges);
    }

    [Fact]
    public void PickingViewModeRaisesRequestWithChosenMode()
    {
        var vm = new GlobalSettingsViewModel(notificationsEnabled: true, startOnBoot: false, viewMode: UsageViewMode.Bar);
        UsageViewMode? requested = null;
        vm.ViewModeChangeRequested += (_, mode) => requested = mode;

        vm.ViewModeIndex = (int)UsageViewMode.Gauge;

        Assert.Equal(UsageViewMode.Gauge, requested);
    }

    [Fact]
    public void TogglingNotificationsRaisesRequestWithNewValue()
    {
        var vm = new GlobalSettingsViewModel(notificationsEnabled: true, startOnBoot: false, viewMode: UsageViewMode.Bar);
        bool? requested = null;
        vm.NotificationsToggleRequested += (_, value) => requested = value;

        vm.NotificationsEnabled = false;

        Assert.False(requested);
    }

    [Fact]
    public void TogglingStartOnBootRaisesRequestWithNewValue()
    {
        var vm = new GlobalSettingsViewModel(notificationsEnabled: false, startOnBoot: false, viewMode: UsageViewMode.Bar);
        bool? requested = null;
        vm.StartOnBootToggleRequested += (_, value) => requested = value;

        vm.StartOnBoot = true;

        Assert.True(requested);
    }

    [Fact]
    public void SetStartOnBootReflectsStateWithoutRaisingEvent()
    {
        var vm = new GlobalSettingsViewModel(notificationsEnabled: true, startOnBoot: true, viewMode: UsageViewMode.Bar);
        var raised = 0;
        vm.StartOnBootToggleRequested += (_, _) => raised++;

        vm.SetStartOnBoot(false);

        Assert.False(vm.StartOnBoot);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SyncFromSystemReflectsBothTogglesWithoutRaisingEvents()
    {
        var vm = new GlobalSettingsViewModel(notificationsEnabled: true, startOnBoot: true, viewMode: UsageViewMode.Bar);
        var notifications = 0;
        var startup = 0;
        vm.NotificationsToggleRequested += (_, _) => notifications++;
        vm.StartOnBootToggleRequested += (_, _) => startup++;

        vm.SyncFromSystem(notificationsEnabled: false, startOnBoot: false);

        Assert.False(vm.NotificationsEnabled);
        Assert.False(vm.StartOnBoot);
        Assert.Equal(0, notifications);
        Assert.Equal(0, startup);
    }
}

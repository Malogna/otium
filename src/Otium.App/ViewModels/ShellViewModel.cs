using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;

namespace Otium.App.ViewModels;

/// <summary>Advanced: the detailed dashboard, invite stats and every setting.</summary>
public partial class ShellViewModel(AppHost host, Action back, Action? accountDeleted = null) : ViewModelBase
{
    public DashboardViewModel Dashboard { get; } = new(host);

    public InviteViewModel Invite { get; } = new(host);

    public SettingsViewModel Settings { get; } = new(host, accountDeleted);

    [RelayCommand]
    private void Back() => back();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;

namespace Otium.App.ViewModels;

/// <summary>First launch: one button. Everything else has a sensible default and lives in Advanced.</summary>
public partial class WelcomeViewModel(AppHost host, Action started) : ViewModelBase
{
    public bool IsSupported => host.Provider.IsSupportedOnThisPlatform;

    public string DevicesLine => host.GpuProvider is { } gpu
        ? $"Uses your processor and {gpu.Gpus[0].Name} while you're away."
        : "Uses your processor while you're away.";

    [ObservableProperty] public partial bool ShowInvite { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string InviteCode { get; set; } = host.Settings.Referral.CodeUsed ?? "";

    [ObservableProperty] public partial string? InviteError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsStarting { get; set; }

    partial void OnInviteCodeChanged(string value) =>
        InviteError = string.IsNullOrWhiteSpace(value) || ReferralCodes.IsWellFormed(value)
            ? null
            : $"Invite codes are {ReferralCodes.Length} letters and numbers.";

    private bool CanStart() => IsSupported && !IsStarting && InviteError is null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        IsStarting = true;
        started(); // show the home screen right away; it reports progress while the account connects
        await host.StartFirstTimeAsync(InviteCode);
    }

    [RelayCommand]
    private void ToggleInvite() => ShowInvite = !ShowInvite;

    [RelayCommand]
    private static void OpenHowItWorks() => Launch.Open("https://github.com/Malogna/otium#readme");
}

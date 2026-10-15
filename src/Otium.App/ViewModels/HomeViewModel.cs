using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Engine;
using Otium.Core.Settings;

namespace Otium.App.ViewModels;

/// <summary>
/// The everyday screen: balance, one switch, plain-language status, cash out, invite.
/// Anything technical (devices, algorithms, electricity) is one click away under Advanced.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private readonly Action _openAdvanced;

    public HomeViewModel(AppHost host, Action openAdvanced)
    {
        _host = host;
        _openAdvanced = openAdvanced;
        CashOut = new CashOutViewModel(host, () => IsCashingOut = false);

        foreach (var engine in host.Engines)
            engine.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Updates.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public CashOutViewModel CashOut { get; }

    [ObservableProperty] public partial string Balance { get; set; } = "$0.00";
    [ObservableProperty] public partial string BalanceCaption { get; set; } = "";
    [ObservableProperty] public partial string PendingText { get; set; } = "";
    [ObservableProperty] public partial bool IsBalanceMode { get; set; }

    [ObservableProperty] public partial string StateLabel { get; set; } = "Paused";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool IsEarning { get; set; }
    [ObservableProperty] public partial bool IsWaiting { get; set; }
    [ObservableProperty] public partial bool IsProblem { get; set; }
    [ObservableProperty] public partial string ToggleText { get; set; } = "Start";
    [ObservableProperty] public partial bool NeedsAntivirusFix { get; set; }

    [ObservableProperty] public partial bool CanCashOut { get; set; }
    [ObservableProperty] public partial string CashOutHint { get; set; } = "";
    [ObservableProperty] public partial bool IsCashingOut { get; set; }

    [ObservableProperty] public partial string InviteTitle { get; set; } = "";
    [ObservableProperty] public partial string InviteCode { get; set; } = "";
    [ObservableProperty] public partial bool HasInviteCode { get; set; }
    [ObservableProperty] public partial string CopiedText { get; set; } = "";

    [ObservableProperty] public partial bool UpdateReady { get; set; }
    [ObservableProperty] public partial string UpdateText { get; set; } = "";

    private void Refresh()
    {
        var settings = _host.Settings;
        var account = settings.Account;
        var snapshots = _host.Engines.Select(e => e.Snapshot).ToList();
        var userPerDay = snapshots.Where(s => s.State == EngineState.Earning).Sum(s => s.UserUsdPerDay);

        // Balance
        IsBalanceMode = settings.PayoutMode == PayoutMode.Balance;
        if (IsBalanceMode)
        {
            Balance = $"{Format.Money(account.AvailableUsd)}";
            BalanceCaption = "Your balance";
            PendingText = account.PendingUsd > 0 ? $"+ {Format.Money(account.PendingUsd)} on the way (confirming)" : "";
        }
        else
        {
            Balance = Format.Usd(snapshots.Sum(s => s.TotalUserUsd));
            BalanceCaption = "Earned so far (paid straight to your wallet)";
            PendingText = "";
        }

        // Status in plain words
        var states = snapshots.Select(s => s.State).ToList();
        ToggleText = settings.EarningEnabled ? "Pause" : "Start";
        NeedsAntivirusFix = snapshots.Any(s => s.EngineBlocked);
        IsEarning = states.Contains(EngineState.Earning);
        IsWaiting = !IsEarning && settings.EarningEnabled && states.Contains(EngineState.WaitingForIdle);
        IsProblem = NeedsAntivirusFix || (!IsEarning && states.Contains(EngineState.Error));
        (StateLabel, StatusText) = Describe(settings, states, userPerDay);

        // Cash out
        var minimum = Math.Min(account.MinPayPalUsd, account.MinCryptoUsd);
        CanCashOut = IsBalanceMode && account.AvailableUsd >= minimum && account.AvailableUsd > 0;
        CashOutHint = !IsBalanceMode
            ? "The pool pays your wallet automatically."
            : CanCashOut
                ? "PayPal or crypto, sent automatically."
                : $"Cash out from {Format.Money(account.MinCryptoUsd)} in crypto or {Format.Money(account.MinPayPalUsd)} with PayPal.";

        // Invite
        var referralPercent = _host.Account?.Balance.ReferralPercent ?? 0;
        InviteTitle = referralPercent > 0
            ? $"Invite friends and get {referralPercent:0.#}% of everything they earn"
            : "Invite friends: they mine fee-free for 30 days";
        InviteCode = settings.Referral.MyCode ?? "";
        HasInviteCode = InviteCode.Length > 0;

        UpdateReady = _host.Updates.ReadyVersion is not null;
        UpdateText = UpdateReady ? $"Otium {_host.Updates.ReadyVersion} is ready." : "";
    }

    private (string Label, string Text) Describe(AppSettings settings, List<EngineState> states, double userPerDay)
    {
        if (!settings.EarningEnabled)
            return ("Paused", "Press Start to earn while you're away from your computer.");
        if (NeedsAntivirusFix)
            return ("Needs your OK", "Your antivirus blocked the earning engine. Open Advanced to allow it.");
        if (_host.MiningBlocker is { } blocker)
            return ("Connecting", blocker);
        if (states.Contains(EngineState.Earning))
            return ("Earning", userPerDay > 0 ? $"Earning about {Format.Usd(userPerDay)} a day right now." : "Earning.");
        if (states.Contains(EngineState.Installing))
            return ("Setting up", "Downloading the earning engines. This only happens once.");
        if (states.Contains(EngineState.Starting))
            return ("Starting", "Getting ready. Your first earnings show up within a few minutes.");
        if (states.Contains(EngineState.WaitingForIdle))
            return ("Ready", "Otium starts earning when you step away from your computer.");
        if (states.Contains(EngineState.PausedUnprofitable))
            return ("Paused", "Electricity would cost more than you'd earn right now. Otium checks again every 30 minutes.");
        if (states.Contains(EngineState.Error))
            return ("Needs attention", "Something went wrong. Otium retries automatically; see Advanced for details.");
        return ("Starting", "Getting ready…");
    }

    [RelayCommand]
    private void Toggle() => _host.SetEarning(!_host.Settings.EarningEnabled);

    [RelayCommand]
    private void OpenAdvanced() => _openAdvanced();

    [RelayCommand]
    private void StartCashOut()
    {
        CashOut.Reset();
        IsCashingOut = true;
    }

    [RelayCommand]
    private async Task CopyInvite()
    {
        var link = _host.Settings.Referral.ShareUrl;
        if (string.IsNullOrEmpty(link))
            return;
        await ClipboardService.CopyAsync($"I earn money with my computer's spare power using Otium. Join with my code {InviteCode} and mine fee-free for 30 days: {link}");
        CopiedText = "Invite copied. Paste it to a friend!";
    }

    [RelayCommand]
    private Task RestartToUpdate() => _host.ApplyUpdateAndRestartAsync();
}

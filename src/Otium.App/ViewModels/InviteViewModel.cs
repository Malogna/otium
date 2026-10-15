using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Fees;

namespace Otium.App.ViewModels;

/// <summary>Invite friends: the code, share shortcuts, and the rewards it has earned.</summary>
public partial class InviteViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public InviteViewModel(AppHost host)
    {
        _host = host;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Load);
        Load();
    }

    public string RewardFriend => $"Your friend mines fee-free for their first {FeePolicy.Default.RefereeFeeHoliday.TotalDays:0} days.";

    public string RewardYou => _host.Settings.PayoutMode == Core.Settings.PayoutMode.Balance
        ? $"You get {(_host.Account?.Balance.ReferralPercent ?? 0):0.#}% of everything they earn for " +
          $"{FeePolicy.Default.ReferrerRewardPeriod.TotalDays / 30:0} months, added to your Otium balance. It comes out of Otium's fee, never out of your friend's earnings."
        : 
        $"You get {FeePolicy.Default.ReferrerShareOfFee * 100:0}% of Otium' fee on everything they mine for " +
        $"{FeePolicy.Default.ReferrerRewardPeriod.TotalDays / 30:0} months, paid by the pool straight to your wallet.";

    public string RewardBonus =>
        $"Each friend who actually starts mining adds {FeePolicy.Default.ReferrerFeeHolidayPerVerifiedFriend.TotalDays:0} fee-free days for you " +
        $"(up to {FeePolicy.Default.MaxFeeHoliday.TotalDays:0} days at a time).";

    [ObservableProperty] public partial bool HasCode { get; set; }
    [ObservableProperty] public partial string Code { get; set; } = "";
    [ObservableProperty] public partial string ShareUrl { get; set; } = "";
    [ObservableProperty] public partial string InvitedCount { get; set; } = "0";
    [ObservableProperty] public partial string VerifiedCount { get; set; } = "0";
    [ObservableProperty] public partial string FeeFreeText { get; set; } = "";
    [ObservableProperty] public partial string? ErrorText { get; set; }
    [ObservableProperty] public partial string? CodeErrorText { get; set; }
    [ObservableProperty] public partial string? ReferredByText { get; set; }
    [ObservableProperty] public partial string CopiedText { get; set; } = "";
    [ObservableProperty] public partial bool IsRefreshing { get; set; }

    private string ShareMessage =>
        $"I'm getting paid for my PC's spare power with Otium. Join with my code {Code} and mine fee-free for 30 days: {ShareUrl}";

    private void Load()
    {
        var r = _host.Settings.Referral;
        HasCode = r.MyCode is not null;
        Code = r.MyCode ?? "";
        ShareUrl = r.ShareUrl ?? "";
        InvitedCount = r.InvitedCount.ToString();
        VerifiedCount = r.VerifiedCount.ToString();
        FeeFreeText = r.FeeFreeUntil is { } until && until > DateTimeOffset.UtcNow
            ? $"You're fee-free until {Format.Date(until)}"
            : "";
        ReferredByText = r.ReferredAt is not null ? "You joined through a friend's invite — thanks for being part of it." : null;
        CodeErrorText = r.CodeError is null ? null : $"The invite code you entered wasn't applied: {r.CodeError}";
        ErrorText = _host.LastServerError;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        IsRefreshing = true;
        try
        {
            await _host.SyncAccountAsync(CancellationToken.None);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task CopyCode() => CopyAsync(Code, "Code copied");

    [RelayCommand]
    private Task CopyLink() => CopyAsync(ShareUrl, "Link copied");

    [RelayCommand]
    private Task CopyMessage() => CopyAsync(ShareMessage, "Message copied — paste it anywhere");

    [RelayCommand]
    private void ShareX() => Launch.Open($"https://x.com/intent/post?text={Uri.EscapeDataString(ShareMessage)}");

    [RelayCommand]
    private void ShareWhatsApp() => Launch.Open($"https://wa.me/?text={Uri.EscapeDataString(ShareMessage)}");

    [RelayCommand]
    private void ShareReddit() =>
        Launch.Open($"https://www.reddit.com/submit?url={Uri.EscapeDataString(ShareUrl)}&title={Uri.EscapeDataString("Get paid for your PC's spare power (30 days fee-free)")}");

    [RelayCommand]
    private void ShareEmail() =>
        Launch.Open($"mailto:?subject={Uri.EscapeDataString("Get paid for your PC's spare power")}&body={Uri.EscapeDataString(ShareMessage)}");

    private async Task CopyAsync(string text, string confirmation)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await ClipboardService.CopyAsync(text);
        CopiedText = confirmation;
    }
}

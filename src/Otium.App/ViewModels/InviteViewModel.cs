using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Fees;

namespace Otium.App.ViewModels;

/// <summary>One line of the invite leaderboard.</summary>
public sealed record LeaderRow(string Rank, string Name, string Friends, bool IsYou);

/// <summary>Invite friends: the code, ready-made messages for each app, the rewards it has earned, and the month's top inviters.</summary>
public partial class InviteViewModel : ViewModelBase
{
    private static readonly TimeSpan LeaderboardMaxAge = TimeSpan.FromMinutes(10);

    private readonly AppHost _host;
    private DateTimeOffset _leaderboardLoadedAt = DateTimeOffset.MinValue;

    public InviteViewModel(AppHost host)
    {
        _host = host;
        host.AccountChanged += () => Dispatcher.UIThread.Post(() =>
        {
            Load();
            if (DateTimeOffset.UtcNow - _leaderboardLoadedAt > LeaderboardMaxAge)
                _ = LoadLeaderboardAsync();
        });
        SelectedStyle = ShareStyles[0];
        Load();
        _ = LoadLeaderboardAsync();
    }

    public string RewardFriend => $"Your friend mines fee-free for their first {FriendDays} days.";

    public string RewardYou => _host.Settings.PayoutMode == Core.Settings.PayoutMode.Balance
        ? $"You get {(_host.Account?.Balance.ReferralPercent ?? 0):0.#}% of everything they earn for " +
          $"{FeePolicy.Default.ReferrerRewardPeriod.TotalDays / 30:0} months, added to your Otium balance. It comes out of Otium's fee, never out of your friend's earnings."
        :
        $"You get {FeePolicy.Default.ReferrerShareOfFee * 100:0}% of Otium's fee on everything they mine for " +
        $"{FeePolicy.Default.ReferrerRewardPeriod.TotalDays / 30:0} months ({FeePolicy.Default.FeePercent * FeePolicy.Default.ReferrerShareOfFee:0.#}% of their mining time), " +
        "mined straight to your own address when the same pool pays you both. It comes out of Otium's fee, never out of your friend's earnings.";

    public string RewardBonus =>
        $"Each friend who actually starts mining adds {FeePolicy.Default.ReferrerFeeHolidayPerVerifiedFriend.TotalDays:0} fee-free days for you " +
        $"(up to {FeePolicy.Default.MaxFeeHoliday.TotalDays:0} days at a time).";

    private static string FriendDays => FeePolicy.Default.RefereeFeeHoliday.TotalDays.ToString("0", CultureInfo.InvariantCulture);

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

    // ---------- ready-made messages ----------

    public IReadOnlyList<ShareStyle> ShareStyles => InviteMessages.Styles;

    /// <summary>The tone picked above the message; picking one replaces any edits in <see cref="Message"/>.</summary>
    [ObservableProperty] public partial ShareStyle? SelectedStyle { get; set; }

    /// <summary>The message every share button sends. Editable, so people can say it their own way.</summary>
    [ObservableProperty] public partial string Message { get; set; } = "";

    partial void OnSelectedStyleChanged(ShareStyle? value) => Message = Fill(value?.Template);

    private string Fill(string? template) => InviteMessages.Fill(template ?? "", Code, ShareUrl, FeePolicy.Default.RefereeFeeHoliday);

    private string Subject => Fill(SelectedStyle?.Subject ?? ShareStyles[1].Subject);

    // ---------- leaderboard ----------

    public ObservableCollection<LeaderRow> Leaders { get; } = [];
    [ObservableProperty] public partial bool HasLeaders { get; set; }
    [ObservableProperty] public partial string LeaderboardTitle { get; set; } = "Top inviters this month";
    [ObservableProperty] public partial string LeaderboardNote { get; set; } = "";

    private async Task LoadLeaderboardAsync()
    {
        if (_host.Settings.Account.WorkerId is null)
            return;
        try
        {
            var board = await _host.GetInviteLeaderboardAsync(CancellationToken.None);
            _leaderboardLoadedAt = DateTimeOffset.UtcNow;
            ShowLeaderboard(board);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or InvalidOperationException)
        {
            if (Leaders.Count == 0)
                LeaderboardNote = "The leaderboard couldn't be loaded right now.";
        }
    }

    private void ShowLeaderboard(InviteLeaderboard board)
    {
        LeaderboardTitle = $"Top inviters in {board.PeriodStart.ToString("MMMM", CultureInfo.InvariantCulture)}";
        Leaders.Clear();
        foreach (var e in board.Top)
            Leaders.Add(new LeaderRow($"#{e.Rank}", e.Name, Friends(e.Friends), e.IsYou));
        HasLeaders = Leaders.Count > 0;
        LeaderboardNote = board.YourRank switch
        {
            null when Leaders.Count == 0 => "Nobody is on the board yet this month. Invite a friend who starts earning to be first.",
            null => "Invite a friend who starts earning to get on the board.",
            { } rank when board.Top.Any(e => e.IsYou) => $"You're #{rank} with {Friends(board.YourFriends)} this month.",
            { } rank => $"You're #{rank} with {Friends(board.YourFriends)} this month. Keep going!",
        };
    }

    private static string Friends(int count) => count == 1 ? "1 friend" : $"{count} friends";

    // ---------- loading and commands ----------

    private void Load()
    {
        var r = _host.Settings.Referral;
        var codeChanged = Code != (r.MyCode ?? "") || ShareUrl != (r.ShareUrl ?? "");
        HasCode = r.MyCode is not null;
        Code = r.MyCode ?? "";
        ShareUrl = r.ShareUrl ?? "";
        InvitedCount = r.InvitedCount.ToString(CultureInfo.InvariantCulture);
        VerifiedCount = r.VerifiedCount.ToString(CultureInfo.InvariantCulture);
        FeeFreeText = r.FeeFreeUntil is { } until && until > DateTimeOffset.UtcNow
            ? $"You're fee-free until {Format.Date(until)}"
            : "";
        ReferredByText = r.ReferredAt is not null ? "You joined through a friend's invite — thanks for being part of it." : null;
        CodeErrorText = r.CodeError is null ? null : $"The invite code you entered wasn't applied: {r.CodeError}";
        ErrorText = _host.LastServerError;
        if (codeChanged || Message.Length == 0)
            Message = Fill(SelectedStyle?.Template);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        IsRefreshing = true;
        try
        {
            await _host.SyncAccountAsync(CancellationToken.None);
            await LoadLeaderboardAsync();
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
    private Task CopyMessage() => CopyAsync(Message, "Message copied — paste it anywhere");

    /// <summary>Discord has no share link, so copy a Discord-formatted message to paste into a chat or server.</summary>
    [RelayCommand]
    private Task ShareDiscord() => CopyAsync(InviteMessages.ForDiscord(Message, Code, ShareUrl), "Copied for Discord — paste it into a chat or server");

    [RelayCommand]
    private void ShareWhatsApp() => Launch.Open(InviteMessages.WhatsAppLink(Message));

    [RelayCommand]
    private void ShareEmail() => Launch.Open(InviteMessages.EmailLink(Subject, InviteMessages.ForEmail(Message, Code, ShareUrl)));

    [RelayCommand]
    private void ShareX() => Launch.Open($"https://x.com/intent/post?text={Uri.EscapeDataString(Message)}");

    [RelayCommand]
    private void ShareReddit() =>
        Launch.Open($"https://www.reddit.com/submit?url={Uri.EscapeDataString(ShareUrl)}&title={Uri.EscapeDataString(Subject)}");

    private async Task CopyAsync(string text, string confirmation)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await ClipboardService.CopyAsync(text);
        CopiedText = confirmation;
    }
}

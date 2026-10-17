using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;

namespace Otium.App.ViewModels;

/// <summary>
/// "Your data" in Advanced settings: download a copy of everything the Otium server stores about the account, or
/// delete the account (confirmed with a code sent to its email), after which the app starts over at the welcome screen.
/// </summary>
public partial class YourDataViewModel(AppHost host, Action accountDeleted) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(SendCodeCommand), nameof(DeleteCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string? DownloadInfo { get; set; }
    [ObservableProperty] public partial string? DownloadError { get; set; }

    [ObservableProperty] public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool CodeSent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool GiveUpBalance { get; set; }

    [ObservableProperty] public partial string? DeleteInfo { get; set; }
    [ObservableProperty] public partial string? DeleteError { get; set; }

    public string? Email => host.Settings.Account.Email;

    public bool HasEmail => Email is not null;

    public bool NeedsCode => HasEmail && !CodeSent;

    public bool CanEnterCode => HasEmail && CodeSent;

    private decimal Balance => Math.Max(0, host.Settings.Account.AvailableUsd) + Math.Max(0, host.Settings.Account.PendingUsd);

    public bool HasBalance => Balance > 0;

    public string BalanceWarning =>
        $"You still have {Format.Money(Balance)} in Otium (including earnings that are still on hold). Deleting your account gives it up. " +
        $"Cash out first if you can: the minimum is {Format.Money(host.Settings.Account.MinCryptoUsd)} by crypto or " +
        $"{Format.Money(host.Settings.Account.MinPayPalUsd)} by PayPal.";

    public bool HasOpenCashout => host.Account?.Balance.RecentCashouts.Any(c =>
        c.Status is CashoutStatuses.Queued or CashoutStatuses.Review or CashoutStatuses.Sending) ?? false;

    public string WhatIsKept =>
        "What's removed: your email, invite code, the link between you and your mining worker, every computer's sign-in, " +
        "the internet addresses recorded while sharing, and your full payout addresses. People you invited keep what they earned.\n\n" +
        "What's kept: the money records (each earning, cash-out and amount, with dates), without anything that identifies you. " +
        "Otium has to keep bookkeeping records for several years by law, and they also prove what was paid out. " +
        "Settings and earnings stats on this computer stay in the data folder until you remove them.";

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task Download()
    {
        DownloadInfo = null;
        DownloadError = await RunAsync(async () =>
        {
            var data = await host.ExportAccountDataAsync();
            var path = await SaveFileService.SaveAsync("Save your Otium data", $"otium-data-{DateTime.Now:yyyy-MM-dd}.json", "json", data);
            if (path is not null)
                DownloadInfo = $"Saved to {path}.";
        });
    }

    [RelayCommand]
    private void StartDelete()
    {
        Refresh();
        IsConfirmingDelete = true;
        DeleteError = null;
        DeleteInfo = null;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmingDelete = false;
        CodeSent = false;
        Code = "";
        GiveUpBalance = false;
        DeleteError = null;
        DeleteInfo = null;
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task SendCode()
    {
        DeleteError = await RunAsync(async () =>
        {
            await host.SendDeleteAccountCodeAsync();
            CodeSent = true;
            Code = "";
            DeleteInfo = $"We sent a 6-digit code to {Email}. It's valid for 15 minutes.";
            Refresh();
        });
    }

    private bool CanAct() => !IsBusy;

    private bool CanDelete() =>
        !IsBusy && !HasOpenCashout && (!HasBalance || GiveUpBalance) && (!HasEmail || (CodeSent && Code.Trim().Length == 6));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        DeleteError = await RunAsync(async () =>
        {
            await host.DeleteAccountAsync(HasEmail ? Code : null, GiveUpBalance);
            CancelDelete();
            accountDeleted();
        });
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(HasEmail));
        OnPropertyChanged(nameof(NeedsCode));
        OnPropertyChanged(nameof(CanEnterCode));
        OnPropertyChanged(nameof(HasBalance));
        OnPropertyChanged(nameof(BalanceWarning));
        OnPropertyChanged(nameof(HasOpenCashout));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    /// <returns>A user-facing error, or null when the action worked.</returns>
    private async Task<string?> RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
            return null;
        }
        catch (AccountRequestException e)
        {
            return e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return "Couldn't reach Otium. Check your internet connection and try again.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"Couldn't save the file: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

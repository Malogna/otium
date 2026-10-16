using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Wallets;

namespace Otium.App.ViewModels;

/// <summary>Cash out the available balance by PayPal (email) or Monero (address).</summary>
public partial class CashOutViewModel(AppHost host, Action close) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrypto), nameof(DestinationLabel), nameof(DestinationPlaceholder), nameof(MinimumText))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsPayPal { get; set; } = true;

    public bool IsCrypto
    {
        get => !IsPayPal;
        set => IsPayPal = !value;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string Destination { get; set; } = "";

    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Result { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsSending { get; set; }

    public string Amount => $"{Format.Money(host.Settings.Account.AvailableUsd)}";

    public string DestinationLabel => IsPayPal ? "Your PayPal email" : "Your Monero (XMR) address";

    public string DestinationPlaceholder => IsPayPal ? "you@example.com" : "4… or 8…";

    public string MinimumText => IsPayPal
        ? $"Minimum {Format.Money(host.Settings.Account.MinPayPalUsd)}. No PayPal account yet? PayPal emails you a link to claim it."
        : $"Minimum {Format.Money(host.Settings.Account.MinCryptoUsd)}. Sent as XMR at today's price; the network fee is covered.";

    partial void OnIsPayPalChanged(bool value)
    {
        Destination = (value ? host.Settings.Account.LastPayPalEmail : host.Settings.Account.LastCryptoAddress) ?? "";
        Error = null;
    }

    public void Reset()
    {
        IsPayPal = true;
        Destination = host.Settings.Account.LastPayPalEmail ?? "";
        Error = null;
        Result = null;
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(MinimumText));
    }

    private bool CanConfirm() => !IsSending && Result is null && !string.IsNullOrWhiteSpace(Destination);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task Confirm()
    {
        Error = Validate();
        if (Error is not null)
            return;

        IsSending = true;
        try
        {
            var cashout = await host.RequestCashoutAsync(IsPayPal ? CashoutMethods.PayPal : CashoutMethods.Monero, Destination);
            Result = cashout.Status switch
            {
                CashoutStatuses.Review => $"Got it. Your {Format.Money(cashout.AmountUsd)} cash-out gets a quick check first and is usually sent within a day. We'll email you a receipt when it's sent.",
                CashoutStatuses.Paid => $"Sent! {Format.Money(cashout.AmountUsd)} is on its way. A receipt is in your email.",
                _ => $"Done. {Format.Money(cashout.AmountUsd)} is being sent automatically, usually within a few minutes. We'll email you a receipt when it's sent.",
            };
            OnPropertyChanged(nameof(Amount));
        }
        catch (AccountRequestException e)
        {
            Error = e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Error = "Couldn't reach Otium. Check your internet connection and try again.";
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void Close() => close();

    private string? Validate()
    {
        var destination = Destination.Trim();
        if (IsPayPal)
        {
            return MailAddress.TryCreate(destination, out var mail) && mail.Address == destination && destination.Contains('.')
                ? null
                : "That doesn't look like an email address.";
        }
        return MoneroAddress.TryValidate(destination, out _, out var error) ? null : error;
    }
}

/// <summary>One cash-out in the history, in plain words: what, where to, and what happens next.</summary>
public sealed record CashoutRow(string Amount, string Where, string When, string Status, string Detail, string Tone, string? TransactionId)
{
    public bool IsPaid => Tone == "paid";
    public bool IsPending => Tone == "pending";
    public bool IsFailed => Tone == "failed";
    public bool HasTransaction => TransactionId is not null;

    public static CashoutRow From(CashoutInfo c)
    {
        var isPayPal = c.Method == CashoutMethods.PayPal;
        var (status, detail, tone) = c.Status switch
        {
            CashoutStatuses.Paid => ("Paid", PaidDetail(c), "paid"),
            CashoutStatuses.Failed => ("Returned to balance",
                c.Note is null ? "It didn't go through, so the money is back in your balance." : $"It didn't go through ({c.Note}), so the money is back in your balance.",
                "failed"),
            CashoutStatuses.Review => ("Being checked", "A quick check by a person comes first. Usually sent within a day.", "pending"),
            CashoutStatuses.Sending when c.Note == CashoutStatuses.AwaitingClaimNote =>
                ("Claim it in PayPal", $"PayPal emailed {c.Destination}. Open that email to claim your money.", "pending"),
            CashoutStatuses.Sending => ("On its way", isPayPal ? "PayPal is processing it." : "Waiting for the Monero network.", "pending"),
            _ => ("Sending", "Sent automatically, usually within a few minutes.", "pending"),
        };
        return new CashoutRow(
            Format.Money(c.AmountUsd),
            $"{(isPayPal ? "PayPal" : "Monero")} · {c.Destination}",
            Format.Date(c.CreatedAt),
            status,
            detail,
            tone,
            c.TransactionId);
    }

    private static string PaidDetail(CashoutInfo c)
    {
        var when = c.UpdatedAt is { } paid ? $"Sent {Format.Date(paid)}" : "Sent";
        return c.TransactionId is { Length: > 16 } tx ? $"{when} · transaction {tx[..8]}…{tx[^6..]}" : when;
    }
}

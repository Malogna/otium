using System.Collections.ObjectModel;
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
    public ObservableCollection<CashoutRow> Recent { get; } = [];

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
        Recent.Clear();
        foreach (var c in host.Account?.Balance.RecentCashouts ?? [])
            Recent.Add(CashoutRow.From(c));
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
                CashoutStatuses.Review => $"Got it. Your {Format.Money(cashout.AmountUsd)} cash-out gets a quick check first and is usually sent within a day.",
                CashoutStatuses.Paid => $"Sent! {Format.Money(cashout.AmountUsd)} is on its way.",
                _ => $"Done. {Format.Money(cashout.AmountUsd)} is being sent automatically, usually within a few minutes.",
            };
            Recent.Insert(0, CashoutRow.From(cashout));
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

public sealed record CashoutRow(string When, string What, string Status)
{
    public static CashoutRow From(CashoutInfo c) => new(
        Format.Date(c.CreatedAt),
        $"{Format.Money(c.AmountUsd)} via {(c.Method == CashoutMethods.PayPal ? "PayPal" : "Monero")}",
        c.Status switch
        {
            CashoutStatuses.Paid => "Paid",
            CashoutStatuses.Review => "Being checked",
            CashoutStatuses.Failed => "Returned to balance" + (c.Note is null ? "" : $" ({c.Note})"),
            _ => "Sending",
        });
}

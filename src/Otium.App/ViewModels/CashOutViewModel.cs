using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Wallets;

namespace Otium.App.ViewModels;

public static class CashOutConverters
{
    /// <summary>Paused coins stay visible but faded.</summary>
    public static readonly FuncValueConverter<bool, double> DimWhenFalse = new(available => available ? 1.0 : 0.45);
}

/// <summary>A coin the user can cash out in, as the server offers it right now.</summary>
public sealed record CashoutChoice(CashoutMethodInfo Info)
{
    public string Method => Info.Method;

    public string Title => CashoutMethods.Label(Info.Method);

    public string Subtitle => Info.Available ? $"{Info.Network} · from {Format.Money(Info.MinUsd)}" : $"Paused: {Info.UnavailableReason}";

    public bool Available => Info.Available;
}

/// <summary>Cash out the available balance in the user's choice of crypto, to their own wallet address.</summary>
public partial class CashOutViewModel(AppHost host, Action close) : ViewModelBase
{
    /// <summary>What the server offered last; a fixed list before the first sync with an older server.</summary>
    public IReadOnlyList<CashoutChoice> Choices { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DestinationLabel), nameof(DestinationPlaceholder), nameof(MinimumText), nameof(FeeText), nameof(HasFeeText), nameof(UnavailableText))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial CashoutChoice? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressHint))]
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

    public string DestinationLabel => Selected is { } s
        ? $"Your {s.Info.Name} address on the {s.Info.Network} network"
        : "Your address";

    public string DestinationPlaceholder => Selected?.Method switch
    {
        CashoutMethods.Bitcoin => "bc1… (or 1… / 3…)",
        CashoutMethods.Ethereum or CashoutMethods.Bnb => "0x…",
        CashoutMethods.Solana => "Your Solana wallet address",
        _ => "",
    };

    public string MinimumText => Selected is { } s
        ? $"Minimum {Format.Money(s.Info.MinUsd)}. Sent as {CashoutMethods.Ticker(s.Method)} at today's price." +
          (s.Info.FeeNote is null ? " Otium pays the network fee." : "")
        : "";

    public string? FeeText => Selected?.Info.FeeNote;

    public bool HasFeeText => FeeText is not null;

    public string? UnavailableText => Selected is { Available: false } s
        ? $"{s.Info.Name} payouts are {s.Info.UnavailableReason}. Pick another coin or try again later."
        : null;

    /// <summary>Live feedback while typing, so a wrong network or a typo shows before anything is sent.</summary>
    public string? AddressHint
    {
        get
        {
            var value = Destination.Trim();
            if (value.Length < 10 || Selected is null)
                return null;
            return CryptoAddresses.TryValidate(Selected.Method, value, out var error) ? null : error;
        }
    }

    partial void OnSelectedChanged(CashoutChoice? value)
    {
        Destination = value is not null && host.Settings.Account.LastAddresses.TryGetValue(value.Method, out var last) ? last : "";
        Error = null;
    }

    public void Reset()
    {
        var offered = host.Account?.Balance.Methods ?? CashoutMethods.Crypto
            .Select(m => new CashoutMethodInfo(m, CashoutMethods.Name(m), CashoutMethods.Network(m), host.Settings.Account.MinCryptoUsd, null, null))
            .ToList();
        Choices = offered.Where(m => m.Method != CashoutMethods.PayPal).Select(m => new CashoutChoice(m)).ToList();
        OnPropertyChanged(nameof(Choices));

        var last = host.Settings.Account.LastCashoutMethod;
        Selected = Choices.FirstOrDefault(c => c.Method == last && c.Available) ?? Choices.FirstOrDefault(c => c.Available) ?? Choices.FirstOrDefault();
        OnSelectedChanged(Selected);
        Error = null;
        Result = null;
        OnPropertyChanged(nameof(Amount));
    }

    private bool CanConfirm() => !IsSending && Result is null && Selected is { Available: true } s
                                 && CryptoAddresses.TryValidate(s.Method, Destination, out _);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task Confirm()
    {
        if (Selected is not { } choice)
            return;
        if (!CryptoAddresses.TryValidate(choice.Method, Destination, out var addressError))
        {
            Error = addressError;
            return;
        }

        IsSending = true;
        Error = null;
        try
        {
            var cashout = await host.RequestCashoutAsync(choice.Method, Destination);
            var coin = CashoutMethods.Ticker(choice.Method);
            Result = cashout.Status switch
            {
                CashoutStatuses.Review => $"Got it. Your {Format.Money(cashout.AmountUsd)} cash-out gets a quick check first and is usually sent within a day. We'll email you a receipt when it's sent.",
                CashoutStatuses.Paid => $"Sent! {Format.Money(cashout.AmountUsd)} in {coin} is on its way. A receipt is in your email.",
                _ when choice.Info.FeeNote is not null => $"Done. {Format.Money(cashout.AmountUsd)} is being swapped to {coin} and sent to your wallet, usually within 10–30 minutes. We'll email you a receipt when it arrives.",
                _ => $"Done. {Format.Money(cashout.AmountUsd)} in {coin} is being sent to your wallet automatically, usually within a few minutes. We'll email you a receipt when it's sent.",
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
}

/// <summary>One cash-out in the history, in plain words: what, where to, and what happens next.</summary>
public sealed record CashoutRow(string Amount, string Where, string When, string Status, string Detail, string Tone, string? TransactionId, string? ExplorerUrl)
{
    public bool IsPaid => Tone == "paid";
    public bool IsPending => Tone == "pending";
    public bool IsFailed => Tone == "failed";
    public bool HasTransaction => TransactionId is not null;
    public bool HasExplorer => ExplorerUrl is not null;

    public static CashoutRow From(CashoutInfo c)
    {
        var isSwap = c.Method is CashoutMethods.Ethereum or CashoutMethods.Bnb or CashoutMethods.Solana;
        var (status, detail, tone) = c.Status switch
        {
            CashoutStatuses.Paid => ("Paid", PaidDetail(c), "paid"),
            CashoutStatuses.Failed => ("Returned to balance",
                c.Note is null ? "It didn't go through, so the money is back in your balance." : $"It didn't go through ({c.Note}), so the money is back in your balance.",
                "failed"),
            CashoutStatuses.Review => ("Being checked", "A quick check by a person comes first. Usually sent within a day.", "pending"),
            CashoutStatuses.Sending when c.Note == CashoutStatuses.AwaitingClaimNote =>
                ("Claim it in PayPal", $"PayPal emailed {c.Destination}. Open that email to claim your money.", "pending"),
            CashoutStatuses.Sending when isSwap => ("Swapping", $"Being swapped to {CashoutMethods.Ticker(c.Method)} through THORChain, usually 10–30 minutes.", "pending"),
            CashoutStatuses.Sending => ("On its way", c.Method == CashoutMethods.PayPal ? "PayPal is processing it." : "Waiting for the network.", "pending"),
            _ => ("Sending", "Sent automatically, usually within a few minutes.", "pending"),
        };
        var where = c.Method == CashoutMethods.PayPal
            ? $"PayPal · {c.Destination}"
            : $"{CashoutMethods.Label(c.Method)} · {c.Destination}";
        return new CashoutRow(
            Format.Money(c.AmountUsd),
            where,
            Format.Date(c.CreatedAt),
            status,
            detail,
            tone,
            c.TransactionId,
            CashoutMethods.ExplorerUrl(c.Method, c.TransactionId));
    }

    private static string PaidDetail(CashoutInfo c)
    {
        var when = c.UpdatedAt is { } paid ? $"Sent {Format.Date(paid)}" : "Sent";
        return c.TransactionId is { Length: > 16 } tx ? $"{when} · transaction {tx[..8]}…{tx[^6..]}" : when;
    }
}

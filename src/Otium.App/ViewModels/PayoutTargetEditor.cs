using CommunityToolkit.Mvvm.ComponentModel;
using Otium.App.Services;
using Otium.Core.Wallets;

namespace Otium.App.ViewModels;

/// <summary>
/// Choosing where the pool pays: a coin and the user's own address for it, checked as it's typed. Used by onboarding
/// and Settings. US users only get Monero, since unMineable (which pays every other coin) doesn't serve them.
/// </summary>
public partial class PayoutTargetEditor : ObservableObject
{
    public PayoutTargetEditor(string? current)
    {
        Coins = AppHost.InUnitedStates ? [PayoutCoins.Monero] : PayoutCoins.All;
        var existing = PayoutTarget.ParseOrNull(current);
        SelectedCoin = existing is not null && Coins.Contains(existing.Coin) ? existing.Coin : Coins.Contains(PayoutCoins.Default) ? PayoutCoins.Default : Coins[0];
        Address = existing is not null && existing.Coin == SelectedCoin ? existing.Address : "";
    }

    public IReadOnlyList<PayoutCoin> Coins { get; }

    public bool IsUnitedStates => AppHost.InUnitedStates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Placeholder), nameof(Hint), nameof(PoolNote))]
    public partial PayoutCoin SelectedCoin { get; set; } = PayoutCoins.Default;

    [ObservableProperty] public partial string Address { get; set; } = "";

    /// <summary>Shown once the address isn't empty and doesn't check out.</summary>
    [ObservableProperty] public partial string? Error { get; set; }

    [ObservableProperty] public partial bool IsValid { get; set; }

    /// <summary>The checked target ("USDT:T…"), or null while the address isn't valid.</summary>
    public PayoutTarget? Target { get; private set; }

    public string Placeholder => $"Your {SelectedCoin.Ticker} address ({SelectedCoin.Placeholder})";

    public string Hint =>
        $"Copy it from your wallet's Receive screen with {SelectedCoin.Network} selected. Any wallet or exchange account that " +
        $"can receive {SelectedCoin.Ticker} on {SelectedCoin.Network} works.";

    public string PoolNote => SelectedCoin == PayoutCoins.Monero
        ? IsUnitedStates
            ? "MoneroOcean pays this address automatically once you've earned 0.003 XMR."
            : "MoneroOcean pays this address for your processor's mining once you've earned 0.003 XMR. Your graphics card mines " +
              "better-paying coins on unMineable, which pays the same address in XMR once a day from 0.03 XMR."
        : $"unMineable pays this address once a day when you've earned {SelectedCoin.ThresholdText}. " +
          "Check its Auto pay switch for your address on unMineable's site; Otium warns you if it's off.";

    /// <summary>Raised when the target changes validity or value, so a host can re-check its Next/Save buttons.</summary>
    public event Action? Changed;

    partial void OnSelectedCoinChanged(PayoutCoin value) => Validate();

    partial void OnAddressChanged(string value) => Validate();

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            Target = null;
            Error = null;
        }
        else if (PayoutTarget.TryCreate(SelectedCoin, Address, out var target, out var error))
        {
            Target = target;
            Error = null;
        }
        else
        {
            Target = null;
            Error = error;
        }
        IsValid = Target is not null;
        Changed?.Invoke();
    }
}

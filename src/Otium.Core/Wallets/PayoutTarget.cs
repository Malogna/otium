using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Otium.Core.Wallets;

/// <summary>
/// A coin the pool can pay users in, as offered in the app.
/// </summary>
/// <param name="Symbol">unMineable's coin symbol, which is also the first part of the miner login ("USDT:T…").</param>
/// <param name="Name">"Tether", "Bitcoin".</param>
/// <param name="Ticker">Shown next to amounts.</param>
/// <param name="Network">The network the address is on, as wallets name it.</param>
/// <param name="Format">Which addresses fit. The pool picks the network from the address, so each coin takes one kind.</param>
/// <param name="Threshold">unMineable's minimum payout on that network (October 2026), in <paramref name="Ticker"/>.</param>
/// <param name="Placeholder">What an address looks like, for the empty address box.</param>
public sealed record PayoutCoin(
    string Symbol,
    string Name,
    string Ticker,
    string Network,
    AddressFormat Format,
    decimal Threshold,
    string Placeholder)
{
    /// <summary>"Tether (USDT) on TRON".</summary>
    public string Label => Name == Ticker ? $"{Name} on {Network}" : $"{Name} ({Ticker}) on {Network}";

    /// <summary>"1.5 USDT".</summary>
    public string ThresholdText => $"{Threshold.ToString("0.########", CultureInfo.InvariantCulture)} {Ticker}";
}

/// <summary>The coins Otium offers for payouts, in display order. Users can pick any coin, so these are the ones whose
/// address Otium can check and whose minimum payout is small enough to reach in days, not months.</summary>
public static class PayoutCoins
{
    public static readonly PayoutCoin Usdt = new("USDT", "Tether", "USDT", "TRON (TRC-20)", AddressFormat.Tron, 1.5m, "T…");
    public static readonly PayoutCoin Bitcoin = new("BTC", "Bitcoin", "BTC", "Bitcoin", AddressFormat.Bitcoin, 0.00075m, "bc1… or 1… or 3…");
    public static readonly PayoutCoin Litecoin = new("LTC", "Litecoin", "LTC", "Litecoin", AddressFormat.Litecoin, 0.00075m, "ltc1… or L… or M…");
    public static readonly PayoutCoin Bnb = new("BNB", "BNB", "BNB", "BNB Smart Chain (BEP-20)", AddressFormat.Evm, 0.01m, "0x…");
    public static readonly PayoutCoin Solana = new("SOL", "Solana", "SOL", "Solana", AddressFormat.Solana, 0.05m, "Base58, 32–44 characters");
    public static readonly PayoutCoin Tron = new("TRX", "TRON", "TRX", "TRON", AddressFormat.Tron, 15m, "T…");
    // An 0x address gets Ether on Ethereum itself, where the minimum payout is large (about $60 in October 2026).
    public static readonly PayoutCoin Ether = new("ETH", "Ether", "ETH", "Ethereum", AddressFormat.Evm, 0.025m, "0x…");
    // Also the coin for US users, who mine on MoneroOcean (0.003 XMR minimum) instead of unMineable.
    public static readonly PayoutCoin Monero = new("XMR", "Monero", "XMR", "Monero", AddressFormat.Monero, 0.03m, "4… or 8…");

    public static readonly PayoutCoin Default = Usdt;

    public static readonly IReadOnlyList<PayoutCoin> All = [Usdt, Bitcoin, Litecoin, Bnb, Solana, Tron, Ether, Monero];

    public static PayoutCoin? Find(string? symbol) =>
        All.FirstOrDefault(c => string.Equals(c.Symbol, symbol?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Where a user's mining pays out: a coin and the user's own address for it, written "USDT:TR7NHq…". The pool pays the
/// address directly; Otium never holds the money.
/// </summary>
public sealed record PayoutTarget
{
    private PayoutTarget(PayoutCoin coin, string address)
    {
        Coin = coin;
        Address = address;
    }

    public PayoutCoin Coin { get; }

    /// <summary>The address in canonical form: lower case where case doesn't matter (0x…, bc1…, ltc1…), as typed otherwise.</summary>
    public string Address { get; }

    /// <summary>"USDT:T…", the form stored in settings, sent to the server and used in miner logins.</summary>
    public override string ToString() => $"{Coin.Symbol}:{Address}";

    public static PayoutTarget Create(PayoutCoin coin, string address) =>
        TryCreate(coin, address, out var target, out var error) ? target : throw new ArgumentException(error, nameof(address));

    public static bool TryCreate(PayoutCoin coin, string? address, [NotNullWhen(true)] out PayoutTarget? target, out string? error)
    {
        target = null;
        var value = (address ?? "").Trim();
        if (!CryptoAddresses.TryValidate(coin.Format, value, out error))
        {
            error ??= $"That isn't a valid {coin.Name} address on {coin.Network}. Copy it from your wallet's Receive screen, with {coin.Network} selected.";
            return false;
        }
        target = new PayoutTarget(coin, Canonical(coin.Format, value));
        return true;
    }

    /// <summary>
    /// Reads "COIN:address". A bare Monero address (what Otium stored before payouts went to other coins) reads as
    /// "XMR:address".
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out PayoutTarget? target, out string? error)
    {
        target = null;
        var value = (text ?? "").Trim();
        var colon = value.IndexOf(':');
        if (colon < 0)
        {
            if (value.Length > 0 && MoneroAddress.TryValidate(value, out _, out _))
                return TryCreate(PayoutCoins.Monero, value, out target, out error);
            error = value.Length == 0 ? "Enter your address." : "Choose which coin to be paid in.";
            return false;
        }

        if (PayoutCoins.Find(value[..colon]) is not { } coin)
        {
            error = $"Otium can't pay out in {value[..colon]}. Choose one of: {string.Join(", ", PayoutCoins.All.Select(c => c.Ticker))}.";
            return false;
        }
        return TryCreate(coin, value[(colon + 1)..], out target, out error);
    }

    public static PayoutTarget? ParseOrNull(string? text) => TryParse(text, out var target, out _) ? target : null;

    /// <summary>Whether two stored targets are the same coin and address, however each was typed.</summary>
    public static bool SameAs(string? a, string? b) =>
        ParseOrNull(a) is { } x && ParseOrNull(b) is { } y ? x == y : string.Equals(a?.Trim(), b?.Trim(), StringComparison.Ordinal);

    /// <summary>The canonical "COIN:address" for a stored value, or the value trimmed when it doesn't parse.</summary>
    public static string Normalize(string text) => ParseOrNull(text)?.ToString() ?? text.Trim();

    private static string Canonical(AddressFormat format, string address) => format switch
    {
        AddressFormat.Evm => address.ToLowerInvariant(),
        AddressFormat.Bitcoin when address.StartsWith("bc1", StringComparison.OrdinalIgnoreCase) => address.ToLowerInvariant(),
        AddressFormat.Litecoin when address.StartsWith("ltc1", StringComparison.OrdinalIgnoreCase) => address.ToLowerInvariant(),
        _ => address,
    };

    public bool Equals(PayoutTarget? other) =>
        other is not null && Coin.Symbol == other.Coin.Symbol && string.Equals(Address, other.Address, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Coin.Symbol, Address);
}

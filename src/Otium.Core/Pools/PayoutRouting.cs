using Otium.Core.Wallets;

namespace Otium.Core.Pools;

/// <summary>Which pool pays a payout target.</summary>
public enum PayoutPool
{
    /// <summary>Monero for US users, whom unMineable's terms exclude: MoneroOcean pays their Monero address.</summary>
    MoneroOcean,

    /// <summary>Every other coin: unMineable mines and converts, and pays the address in its own coin.</summary>
    Unmineable,

    /// <summary>Quantus, mined by the graphics card to the user's own Quantus wallet (Kryptex Pool's address login).</summary>
    Kryptex,
}

/// <summary>
/// Turns a payout target ("USDT:T…", "XMR:4…", or a bare Monero address from older settings) into the pool that pays
/// it and the login the miner uses there. Fee and referral slices mine to other targets, so a slice can only go to a
/// target on the same pool as the user: the miner keeps its pool and algorithm and only the login changes.
///
/// Everyone outside the US mines on unMineable, Monero users included (paid in XMR there), so processor, graphics
/// card and every fee slice share one pool and Otium needs a single fee address (USDT). unMineable's terms exclude US
/// persons, so US users' Monero mines on MoneroOcean instead.
/// </summary>
public static class PayoutRouting
{
    /// <summary>unMineable's RandomX stratum. Only its TLS port 443 works for RandomX (checked October 2026).</summary>
    public const string UnmineableCpuHost = "rx.unmineable.com";
    public const int UnmineableTlsPort = 443;

    /// <summary>The pool that mines and pays a target, or null for an invalid one.</summary>
    /// <param name="unitedStates">The user is in the US, where Monero mines on MoneroOcean.</param>
    public static PayoutPool? PoolFor(string? target, bool unitedStates) => PayoutTarget.ParseAnyOrNull(target) switch
    {
        null => null,
        { Coin.Symbol: "XMR" } => unitedStates ? PayoutPool.MoneroOcean : PayoutPool.Unmineable,
        { Coin.Symbol: "QTC" } => PayoutPool.Kryptex,
        _ => PayoutPool.Unmineable,
    };

    /// <summary>Whether <paramref name="other"/> can be mined on the same pool as <paramref name="user"/>.</summary>
    public static bool SamePool(string? user, string? other, bool unitedStates) =>
        PoolFor(user, unitedStates) is { } pool && PoolFor(other, unitedStates) == pool;

    /// <summary>The first fee target that fits the user's pool, or null (the fee slice then stays with the user).</summary>
    public static string? FeeTargetFor(string? user, IEnumerable<string> feeTargets, bool unitedStates) =>
        feeTargets.FirstOrDefault(t => SamePool(user, t, unitedStates)) is { } fee ? PayoutTarget.Normalize(fee) : null;

    /// <summary>
    /// The user's Quantus target ("QTC:qz…") in canonical form, or null when the setting is empty or not a Quantus
    /// address. Only the graphics card mines to it.
    /// </summary>
    public static string? QuantusTarget(string? setting) =>
        PayoutTarget.ParseAnyOrNull(setting) is { Coin.Symbol: "QTC" } quantus ? quantus.ToString() : null;

    /// <summary>The wallet MoneroOcean expects: the bare Monero address.</summary>
    public static string MoneroOceanWallet(string target) => PayoutTarget.ParseOrNull(target)?.Address ?? target.Trim();

    /// <summary>
    /// unMineable's login: "COIN:address.worker#referral". The coin's default network is picked from the address, so a
    /// TRON address gets USDT on TRC-20. The referral code lowers the pool fee from 1% to 0.75% and credits Otium.
    /// </summary>
    public static string UnmineableLogin(string target, string worker, string? referralCode)
    {
        var parsed = PayoutTarget.ParseOrNull(target) ?? throw new ArgumentException($"Not a payout target: {target}", nameof(target));
        var login = $"{parsed}.{SanitizeWorker(worker)}";
        var code = SanitizeWorker(referralCode ?? "");
        return string.IsNullOrEmpty(referralCode) || code.Length == 0 ? login : $"{login}#{code}";
    }

    /// <summary>Letters, digits, "-" and "_" only (what both pools accept in a worker name), at most 32 characters.</summary>
    public static string SanitizeWorker(string worker)
    {
        var cleaned = new string(worker.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return cleaned.Length == 0 ? "otium" : cleaned[..Math.Min(cleaned.Length, 32)];
    }
}

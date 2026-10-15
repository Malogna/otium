namespace Otium.Server;

public sealed class ReferralOptions
{
    public const string Section = "Referral";

    /// <summary>Public base URL used in share links, e.g. https://otium.example. Defaults to the request host.</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Where the landing page sends people to download the app.</summary>
    public string DownloadUrl { get; set; } = "https://github.com/";

    /// <summary>Own-wallet accounts: hashes a referred friend must have mined (per the pool) to count as verified.</summary>
    public double VerificationHashes { get; set; } = 500_000_000;

    /// <summary>Balance accounts: credited earnings a referred friend needs to count as verified.</summary>
    public decimal VerificationUsd { get; set; } = 0.25m;

    /// <summary>A referral code can only be redeemed this soon after a device first registers.</summary>
    public TimeSpan RedeemWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Minimum time between pool checks for the same referred device.</summary>
    public TimeSpan VerificationRecheck { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>How mining by balance accounts is turned into dollars.</summary>
public sealed class EarningsOptions
{
    public const string Section = "Earnings";

    /// <summary>
    /// Operator XMR addresses (ideally subaddresses of one wallet) that balance accounts mine to. Balance mode is
    /// unavailable while this is empty and no wallet RPC is configured to create addresses.
    /// </summary>
    public string[] PoolWallets { get; set; } = [];

    /// <summary>Accounts per pool address; MoneroOcean separates only ~50 concurrently active workers per address.</summary>
    public int MaxAccountsPerPoolWallet { get; set; } = 40;

    /// <summary>Otium's share of balance-account earnings (covers payout fees, exchange costs and operations).</summary>
    public double FeePercent { get; set; } = 10;

    /// <summary>What a referrer earns, as a percent of an invited friend's gross earnings, paid out of the fee.</summary>
    public double ReferralPercent { get; set; } = 5;

    /// <summary>How long credited earnings stay "on the way" before they can be cashed out.</summary>
    public TimeSpan HoldPeriod { get; set; } = TimeSpan.FromHours(48);

    public TimeSpan CreditInterval { get; set; } = TimeSpan.FromMinutes(10);
}

public sealed class CashoutOptions
{
    public const string Section = "Cashouts";

    public decimal MinPayPalUsd { get; set; } = 5m;

    public decimal MinCryptoUsd { get; set; } = 2m;

    /// <summary>Cash-outs up to this amount are sent automatically when the other checks pass; larger ones are reviewed.</summary>
    public decimal AutoApproveMaxUsd { get; set; } = 50m;

    /// <summary>Total automatic payouts per 24 hours across all users; beyond it requests wait for review.</summary>
    public decimal DailyAutoBudgetUsd { get; set; } = 1000m;

    /// <summary>Accounts younger than this are reviewed on their first cash-out.</summary>
    public TimeSpan MinAccountAgeForAuto { get; set; } = TimeSpan.FromDays(2);

    public TimeSpan ProcessInterval { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class PayPalOptions
{
    public const string Section = "PayPal";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    /// <summary>Use PayPal's sandbox. Keep true until the live account is approved for Payouts.</summary>
    public bool UseSandbox { get; set; } = true;

    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0;
}

public sealed class MoneroWalletOptions
{
    public const string Section = "MoneroWallet";

    /// <summary>monero-wallet-rpc endpoint, e.g. http://127.0.0.1:18083/json_rpc. Keep it on localhost/private network.</summary>
    public string RpcUrl { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public bool IsConfigured => RpcUrl.Length > 0;
}

public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>Shared secret for the /admin endpoints (send as X-Admin-Token). Admin endpoints are disabled while empty.</summary>
    public string Token { get; set; } = "";
}

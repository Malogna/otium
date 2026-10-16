namespace Otium.Server;

public sealed class ReferralOptions
{
    public const string Section = "Referral";

    /// <summary>Public base URL used in share links, e.g. https://otium.example. Defaults to the request host.</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Where the landing page sends people to download the app.</summary>
    public string DownloadUrl { get; set; } = "https://github.com/Malogna/otium/releases/latest";

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

/// <summary>
/// Running behind a reverse proxy that terminates HTTPS (see docs/deploying.md). When enabled, the client's address and
/// scheme are taken from X-Forwarded-For and X-Forwarded-Proto, but only on connections from the proxy; anyone else's
/// headers are ignored. Per-IP rate limits and "one paid sharer per connection" depend on getting this right.
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string Section = "ReverseProxy";

    public bool Enabled { get; set; }

    /// <summary>The proxy's addresses, e.g. 172.30.0.10. With neither list set, only a proxy on localhost is trusted.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Networks the proxy connects from, in CIDR notation, e.g. 10.0.0.0/24.</summary>
    public string[] KnownNetworks { get; set; } = [];
}

/// <summary>
/// Paying balance accounts for sharing unused internet bandwidth through Otium's bandwidth partner. The partner pays
/// Otium as the publisher; these rates set what users are credited from their computers' usage reports, with limits
/// that keep self-reported usage honest. Off until a partner agreement is in place.
/// </summary>
public sealed class BandwidthOptions
{
    public const string Section = "Bandwidth";

    public bool Enabled { get; set; }

    /// <summary>Shown to users on the consent screen as the company whose customers' traffic is routed.</summary>
    public string? PartnerName { get; set; }

    /// <summary>Gross USD per GB shared (before Otium's fee), for partners that pay by volume and report it to the app.</summary>
    public double UsdPerGb { get; set; }

    /// <summary>
    /// Gross USD per hour a computer is online and available. Honeygain pays publishers per daily active device,
    /// weighted by uptime, and reports no volume to the app, so this is the rate that matters with it. The default
    /// (about $0.43 a month for a computer sharing around the clock) is a placeholder: set it from the partner agreement.
    /// </summary>
    public double UsdPerSharingHour { get; set; } = 0.0006;

    /// <summary>Most a computer is credited for per UTC day.</summary>
    public long DailyLimitBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    /// <summary>Faster average rates than this are implausible for proxy traffic and are capped.</summary>
    public long MaxBytesPerSecond { get; set; } = 2 * 1024 * 1024;

    /// <summary>Longest period one report may cover; longer reports are cut to this.</summary>
    public TimeSpan MaxReportPeriod { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Only one computer per internet connection (public IP) is paid at a time, as partners require. Another computer
    /// on the same connection can take over after this much time without credited reports from the first.
    /// </summary>
    public TimeSpan SharedConnectionWindow { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Which GPU algorithms apps may mine. The server re-scores them from live feeds; these settings override that.
/// </summary>
public sealed class GpuPolicyOptions
{
    public const string Section = "GpuPolicy";

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Kill switch: algorithms listed here are disabled for every app at the next policy refresh.</summary>
    public string[] Disabled { get; set; } = [];

    /// <summary>Per-algorithm tier overrides ("primary", "free-power-only", "disabled").</summary>
    public Dictionary<string, string> Tiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Kryptex Pool for GPU coins MoneroOcean doesn't pay for (Pearl, Quantus). Work is mined to the operator's Kryptex
/// account with auto-exchange, under each user's worker id, and credited from Kryptex's per-worker hashrate history.
/// </summary>
public sealed class KryptexOptions
{
    public const string Section = "Kryptex";

    public bool Enabled { get; set; }

    /// <summary>The operator's Kryptex account name (miners log in as "Account.worker"); auto-exchange is set up there.</summary>
    public string? Account { get; set; }

    public string ApiBase { get; set; } = "https://pool.kryptex.com/";

    /// <summary>WhatToMine, the live profitability feed for these coins.</summary>
    public string WhatToMineBase { get; set; } = "https://whattomine.com/";

    public TimeSpan CreditInterval { get; set; } = TimeSpan.FromMinutes(10);

    public Dictionary<string, KryptexCoin> Coins { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pearlhash"] = new() { Coin = "prl", WhatToMineId = 469, Host = "prl.kryptex.network", Port = 7048 },
        ["quantus"] = new() { Coin = "qtc", WhatToMineId = 473, Host = "qtc.kryptex.network", Port = 7049 },
    };
}

public sealed class KryptexCoin
{
    /// <summary>Kryptex's coin slug in API paths, e.g. "prl".</summary>
    public string Coin { get; set; } = "";

    public int WhatToMineId { get; set; }

    public string Host { get; set; } = "";

    public int Port { get; set; }
}

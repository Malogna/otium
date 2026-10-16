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

    public static readonly string[] DefaultMethods = ["btc", "eth", "bnb", "sol"];

    /// <summary>
    /// The payout methods offered, in display order: "btc", "eth", "bnb", "sol" (paid from the Bitcoin treasury, the
    /// others swapped through THORChain), plus the older "paypal" and "xmr" if re-enabled. Empty means the default.
    /// (No default value here: the configuration binder appends to arrays instead of replacing them.)
    /// </summary>
    public string[] Methods { get; set; } = [];

    /// <summary><see cref="Methods"/>, or the default when none are configured, without duplicates.</summary>
    public IReadOnlyList<string> OfferedMethods => (Methods.Length > 0 ? Methods : DefaultMethods).Distinct().ToList();

    /// <summary>Minimum cash-out per method (USD). Methods not listed use <see cref="MinCryptoUsd"/>.</summary>
    public Dictionary<string, decimal> MinUsd { get; set; } = new()
    {
        ["btc"] = 5m,
        ["bnb"] = 5m,
        ["eth"] = 8m,
        ["sol"] = 8m,
    };

    public decimal MinPayPalUsd { get; set; } = 5m;

    public decimal MinCryptoUsd { get; set; } = 5m;

    public decimal MinimumFor(string method) =>
        method == "paypal" ? MinPayPalUsd : MinUsd.TryGetValue(method, out var min) ? min : MinCryptoUsd;

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

/// <summary>The operator's Bitcoin treasury: a bitcoind wallet that pays BTC cash-outs and funds THORChain swaps.</summary>
public sealed class BitcoinWalletOptions
{
    public const string Section = "BitcoinWallet";

    /// <summary>bitcoind JSON-RPC, e.g. http://127.0.0.1:8332. Keep it on localhost or a private network.</summary>
    public string RpcUrl { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>The wallet to use (bitcoind's -rpcwallet), e.g. "otium-payouts".</summary>
    public string Wallet { get; set; } = "";

    /// <summary>Fee target in blocks for payouts; 6 is about an hour and cheap.</summary>
    public int ConfTarget { get; set; } = 6;

    public bool IsConfigured => RpcUrl.Length > 0;
}

/// <summary>THORChain, the swap network that turns treasury BTC into ETH, BNB or SOL at the user's address.</summary>
public sealed class ThorchainOptions
{
    public const string Section = "Thorchain";

    /// <summary>A public THORNode API (no key or account needed). Any node or gateway with the same API works.</summary>
    public string ApiBase { get; set; } = "https://gateway.liquify.com/chain/thorchain_api/";
}

/// <summary>Screening payout addresses against OFAC's sanctioned crypto addresses.</summary>
public sealed class SanctionsOptions
{
    public const string Section = "Sanctions";

    /// <summary>
    /// Plain-text lists (one address per line) per chain, from github.com/0xB10C/ofac-sanctioned-digital-currency-addresses,
    /// which rebuilds them from the OFAC SDN list every night.
    /// </summary>
    public Dictionary<string, string> ListUrls { get; set; } = new()
    {
        ["btc"] = "https://raw.githubusercontent.com/0xB10C/ofac-sanctioned-digital-currency-addresses/lists/sanctioned_addresses_XBT.txt",
        ["evm"] = "https://raw.githubusercontent.com/0xB10C/ofac-sanctioned-digital-currency-addresses/lists/sanctioned_addresses_ETH.txt",
        ["bsc"] = "https://raw.githubusercontent.com/0xB10C/ofac-sanctioned-digital-currency-addresses/lists/sanctioned_addresses_BSC.txt",
        ["sol"] = "https://raw.githubusercontent.com/0xB10C/ofac-sanctioned-digital-currency-addresses/lists/sanctioned_addresses_SOL.txt",
    };

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(12);
}

public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>Shared secret for the /admin endpoints (send as X-Admin-Token). Admin endpoints are disabled while empty.</summary>
    public string Token { get; set; } = "";
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

    /// <summary>
    /// Kryptex's Monero (RandomX) pool, for balance accounts' CPUs routed there by <see cref="CpuMiningOptions"/>.
    /// Port 8029 is its TLS port (7029 plain); regional hosts are xmr-eu and xmr-us.
    /// </summary>
    public KryptexCoin Cpu { get; set; } = new() { Coin = "xmr", Host = "xmr.kryptex.network", Port = 8029, Tls = true };
}

/// <summary>Where balance accounts' CPUs mine. Own-wallet accounts always mine to their own address on MoneroOcean.</summary>
public sealed class CpuMiningOptions
{
    public const string Section = "CpuMining";

    /// <summary>
    /// Share of balance accounts (0–100) whose CPU mines Monero on Kryptex instead of MoneroOcean, chosen by a stable
    /// hash of the worker id. 0 keeps everyone on MoneroOcean; raise it gradually (canary), 100 moves everyone.
    /// Needs Kryptex:Enabled and Kryptex:Account.
    /// </summary>
    public int KryptexPercent { get; set; }
}

public sealed class KryptexCoin
{
    /// <summary>Kryptex's coin slug in API paths, e.g. "prl".</summary>
    public string Coin { get; set; } = "";

    public int WhatToMineId { get; set; }

    public string Host { get; set; } = "";

    public int Port { get; set; }

    /// <summary>Whether <see cref="Port"/> is a TLS port.</summary>
    public bool Tls { get; set; }
}

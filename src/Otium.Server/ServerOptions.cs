namespace Otium.Server;

/// <summary>
/// Whether this server holds users' earnings. Off (the default), Otium is non-custodial: pools pay users' own
/// addresses, so the server keeps accounts and referrals only, and runs no crediting, cash-out, treasury or bandwidth
/// jobs (their routes return 404). On, balance accounts work as before: mining to the operator's pools is credited as
/// a USD balance and cashed out from the treasury.
/// </summary>
public sealed class CustodyOptions
{
    public const string Section = "Custody";

    public bool Enabled { get; set; }
}

/// <summary>
/// The disclosed fee and invite terms sent to apps with every account summary (see FeeTerms). Changing them here takes
/// effect at each app's next sync, without a release. Apps cap the fee at 10%.
/// </summary>
public sealed class FeeOptions
{
    public const string Section = "Fees";

    public double FeePercent { get; set; } = Core.Fees.FeePolicy.Default.FeePercent;

    public double ReferrerShareOfFee { get; set; } = Core.Fees.FeePolicy.Default.ReferrerShareOfFee;

    public double ReferrerRewardDays { get; set; } = Core.Fees.FeePolicy.Default.ReferrerRewardPeriod.TotalDays;

    public double RefereeFeeFreeDays { get; set; } = Core.Fees.FeePolicy.Default.RefereeFeeHoliday.TotalDays;

    public double ReferrerFeeFreeDaysPerFriend { get; set; } = Core.Fees.FeePolicy.Default.ReferrerFeeHolidayPerVerifiedFriend.TotalDays;

    public double MaxFeeFreeDays { get; set; } = Core.Fees.FeePolicy.Default.MaxFeeHoliday.TotalDays;

    public Core.Accounts.FeeTerms ToTerms() =>
        new(FeePercent, ReferrerShareOfFee, ReferrerRewardDays, RefereeFeeFreeDays, ReferrerFeeFreeDaysPerFriend, MaxFeeFreeDays);
}

public sealed class ReferralOptions
{
    public const string Section = "Referral";

    /// <summary>Public base URL used in share links, e.g. https://otium.example. Defaults to the request host.</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Where the landing page sends people to download the app.</summary>
    public string DownloadUrl { get; set; } = "https://github.com/Malogna/otium/releases/latest";

    /// <summary>Own-wallet accounts: hashes a referred friend must have mined (per the pool) to count as verified.</summary>
    public double VerificationHashes { get; set; } = 500_000_000;

    /// <summary>
    /// Accounts paid by unMineable: verified once unMineable has rewarded their address in a daily accounting (or paid
    /// it), which takes at least a day of real mining. unMineable shows no prices, so this can't be a dollar amount.
    /// </summary>
    public bool VerifyUnmineableByRewards { get; set; } = true;

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

    public PearlForkOptions PearlFork { get; set; } = new();
}

/// <summary>
/// Pearl's planned FP8 hard fork (PIP-3, a draft with no activation height in October 2026) whitelists only NVIDIA's
/// Hopper and Blackwell cards: from its activation height, RTX 20/30/40 and AMD cards can't produce valid work. Once the
/// fork is active the policy limits pearlhash to <see cref="SupportedGpus"/>, so every other card moves to its next best
/// coin at the next policy refresh (apps also drop a coin whose shares the pool rejects).
/// </summary>
public sealed class PearlForkOptions
{
    /// <summary>
    /// "auto": active once Pearl's chain (as Kryptex Pool reports it) reaches the activation height, taken from
    /// <see cref="ActivationHeight"/> or else read from PIP-3 as soon as it names one. "active": active now (use when
    /// the fork is announced without a height, or to test). "off": never.
    /// </summary>
    public string Mode { get; set; } = "auto";

    /// <summary>The fork's block height, once announced. Overrides the height read from PIP-3.</summary>
    public long? ActivationHeight { get; set; }

    /// <summary>PIP-3's source; the watcher looks for an activation height in it.</summary>
    public string PipUrl { get; set; } = "https://raw.githubusercontent.com/pearl-research-labs/pips/main/PIPS/pip-0003.md";

    /// <summary>How often PIP-3 and Pearl's chain height are checked.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Card names (case-insensitive regular expressions) that keep mining Pearl after the fork: Blackwell (RTX 50xx,
    /// RTX PRO Blackwell, B100/B200/B300, GB10/GB200) and Hopper (H100, H200, H800, GH200). The founders confirmed the
    /// RTX 5090 and RTX PRO 6000 (Kryptex, 7 October 2026).
    /// </summary>
    public string[] SupportedGpus { get; set; } =
    [
        @"\bRTX 50\d0\b",
        @"\bRTX PRO \d{4}\b.*Blackwell",
        @"\bRTX PRO 6000\b",
        @"\bG?H(100|200|800)\b",
        @"\bG?B(100|200|300)\b",
        @"\bGB10\b",
    ];
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
    /// Coins apps may mine on Kryptex without an account, to the user's own wallet for the coin (Kryptex pays it
    /// directly). Off by default: everything is paid to the user's one payout address, and Quantus would need a
    /// separate Quantus wallet, so the app no longer mines it.
    /// </summary>
    public bool AddressMining { get; set; }

    /// <summary>Quantus: 0.1 QTC minimum, hourly payouts, 3% fee (October 2026). Port 8049 is the TLS port (7049 plain).</summary>
    public Dictionary<string, KryptexCoin> AddressCoins { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quantus"] = new() { Coin = "qtc", WhatToMineId = 473, Host = "qtc.kryptex.network", Port = 8049, Tls = true },
    };

    /// <summary>
    /// Kryptex's Monero (RandomX) pool, for balance accounts' CPUs routed there by <see cref="CpuMiningOptions"/>.
    /// Port 8029 is its TLS port (7029 plain); regional hosts are xmr-eu and xmr-us.
    /// </summary>
    public KryptexCoin Cpu { get; set; } = new() { Coin = "xmr", Host = "xmr.kryptex.network", Port = 8029, Tls = true };
}

/// <summary>
/// unMineable, which mines GPU coins MoneroOcean doesn't pay for and pays users' own addresses in the coin they chose.
/// Apps log in with the user's payout target and Otium's referral code, so nothing here is an account.
/// </summary>
public sealed class UnmineableOptions
{
    public const string Section = "Unmineable";

    public bool Enabled { get; set; } = true;

    /// <summary>unMineable's fee with a referral code on the worker (1% without one).</summary>
    public double FeePercent { get; set; } = 0.75;

    /// <summary>Otium's own fee target on unMineable ("USDT:T…"), for the admin dashboard's fee and referral earnings.</summary>
    public string? OtiumTarget { get; set; }

    /// <summary>GPU algorithms mined there (SRBMiner names), with their WhatToMine id and stratum endpoint.</summary>
    public Dictionary<string, UnmineableAlgorithm> Algorithms { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Offered to AMD cards too: SRBMiner 3.7.6 mines pearlhash on AMD (WhatToMine benchmarks the RX 9070 XT and
        // 7900 XTX on it), although unMineable's support article still says NVIDIA only. If unMineable rejects an AMD
        // card's shares, the app's share-rejection failover sets Pearl aside for that card and it mines the next coin.
        ["pearlhash"] = new() { WhatToMineId = 469, Host = "pearlpow.unmineable.com" },
        // Loses money at $0.15/kWh on every card checked (October 2026).
        ["xelishash"] = new() { WhatToMineId = 459, Host = "xelishash.unmineable.com", Tier = "free-power-only" },
        ["fishhash"] = new() { WhatToMineId = 411, Host = "fishhash.unmineable.com", Tier = "free-power-only" },
    };
}

public sealed class UnmineableAlgorithm
{
    public int WhatToMineId { get; set; }

    public string Host { get; set; } = "";

    /// <summary>unMineable's TLS port (443 and 4444 both work; 443 passes more firewalls).</summary>
    public int Port { get; set; } = 443;

    /// <summary>"primary" or "free-power-only" while the feed is fresh.</summary>
    public string Tier { get; set; } = "primary";

    /// <summary>Card names (regular expressions) that can mine it there; empty for every card.</summary>
    public string[] OnlyGpus { get; set; } = [];
}

/// <summary>
/// Mysterium internet sharing: Otium pays each new node's one-time registration fee (about 0.13 MYST in October 2026)
/// from a small wallet of its own on Polygon, so users' nodes register automatically and settle to their own address.
/// </summary>
/// <summary>
/// Internet sharing through Proxybase: the app runs Proxybase's client and has its earnings paid to the user's own 0x
/// address, so the server has nothing to do but say whether it's offered. On unless the operator turns it off.
/// </summary>
public sealed class ProxybaseOptions
{
    public const string Section = "Proxybase";

    public bool Enabled { get; set; } = true;
}

public sealed class MysteriumOptions
{
    public const string Section = "Mysterium";

    public bool Enabled { get; set; }

    /// <summary>A Polygon JSON-RPC endpoint.</summary>
    public string PolygonRpcUrl { get; set; } = "https://polygon-rpc.com";

    public long ChainId { get; set; } = 137;

    /// <summary>
    /// Private key (hex) of Otium's sponsor wallet. Keep only a few days' worth of MYST (and a little POL for gas) in it.
    /// Set it as a secret (environment variable), never in a file in the repository.
    /// </summary>
    public string? SponsorPrivateKey { get; set; }

    /// <summary>MYST on Polygon.</summary>
    public string MystTokenAddress { get; set; } = "0x1379E8886A944d2D9d440b3d88DF536Aea08d9F3";

    /// <summary>Sent per node: the fee (0.1297–0.1321 MYST when checked) plus a margin, since quotes move.</summary>
    public decimal AmountMyst { get; set; } = 0.15m;

    /// <summary>Most MYST spent on registrations in any 24 hours.</summary>
    public decimal DailyBudgetMyst { get; set; } = 15m;

    public int MaxPerAccount { get; set; } = 5;

    public int MaxPerIpPerDay { get; set; } = 3;
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

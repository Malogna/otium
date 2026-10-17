using Otium.Core.Power;

namespace Otium.Core.Settings;

/// <summary>How a user gets paid.</summary>
public enum PayoutMode
{
    /// <summary>
    /// Only with a custodial server. Mining goes to the operator's pool account under the user's worker id; the Otium
    /// server credits a USD balance the user cashes out in BTC, ETH, BNB or SOL to their own wallet.
    /// </summary>
    Balance,

    /// <summary>
    /// Default. The pool pays the user's own address directly, in the coin they chose (unMineable) or in Monero
    /// (MoneroOcean); nothing passes through Otium.
    /// </summary>
    OwnWallet,
}

public sealed class AppSettings
{
    public bool OnboardingComplete { get; set; }

    public PayoutMode PayoutMode { get; set; } = PayoutMode.OwnWallet;

    /// <summary>
    /// The user's own payout target, "COIN:address" (e.g. "USDT:T…"), used in <see cref="PayoutMode.OwnWallet"/>.
    /// Older settings hold a bare Monero address, which reads as "XMR:…".
    /// </summary>
    public string PayoutAddress { get; set; } = "";

    /// <summary>Price per kWh in USD. Starts as an estimate for the user's country.</summary>
    public double ElectricityPricePerKwh { get; set; } = ElectricityPrices.ForCurrentRegion();

    /// <summary>True until the user enters their own price.</summary>
    public bool ElectricityPriceIsEstimate { get; set; } = true;

    /// <summary>
    /// Whether to subtract electricity at all. False for users who don't pay for power (included in rent, solar,
    /// employer's machine): the price is treated as 0, so mining runs whenever it earns anything.
    /// </summary>
    public bool PayForElectricity { get; set; } = true;

    /// <summary>Day-ahead bidding zone for live hourly prices (e.g. "NO1"); null until resolved for the region.</summary>
    public string? ElectricityZone { get; set; } = SpotPriceZone.DefaultForCurrentRegion()?.Code;

    public int CpuUsagePercent { get; set; } = 50;

    public bool CpuEnabled { get; set; } = true;

    /// <summary>Mine with the graphics card too, when a supported one is present.</summary>
    public bool GpuEnabled { get; set; } = true;

    /// <summary>User-supplied extra GPU watts while mining, used only when power can't be measured.</summary>
    public double? GpuWatts { get; set; }

    /// <summary>On by default so Otium never slows down someone who is using their computer.</summary>
    public bool OnlyWhenIdle { get; set; } = true;

    public int IdleMinutes { get; set; } = 3;

    /// <summary>Stop automatically when estimated earnings don't cover the electricity.</summary>
    public bool NeverRunAtLoss { get; set; } = true;

    /// <summary>User-supplied CPU power at full load. Null means estimate from the CPU.</summary>
    public double? CpuFullLoadWatts { get; set; }

    /// <summary>Whether earning was switched on when the app last closed, so it resumes after a restart.</summary>
    public bool EarningEnabled { get; set; }

    public bool AcceptedTerms { get; set; }

    /// <summary>Share unused internet bandwidth through Otium's partner. Off until the user opts in.</summary>
    public bool BandwidthEnabled { get; set; }

    /// <summary>When the user agreed to the bandwidth-sharing explanation; sharing never runs without it.</summary>
    public DateTimeOffset? BandwidthConsentAt { get; set; }

    /// <summary>Share only while the user is away, like mining. Off by default because sharing barely uses the computer.</summary>
    public bool BandwidthOnlyWhenIdle { get; set; }

    /// <summary>Most data to share per day, in GB.</summary>
    public double BandwidthDailyLimitGb { get; set; } = 2;

    /// <summary>The user answered "Not now" to the internet sharing suggestion on the overview.</summary>
    public bool BandwidthPromptDismissed { get; set; }

    public string ServerUrl { get; set; } = AppIdentity.DefaultServerUrl;

    public ReferralState Referral { get; set; } = new();

    public AccountState Account { get; set; } = new();

    /// <summary>Random per-install secret that authenticates this device to the Otium server.</summary>
    public string DeviceSecret { get; set; } = "";

    /// <summary>Random seed used for the device ID when the OS exposes no machine ID.</summary>
    public string DeviceSeed { get; set; } = "";
}

/// <summary>The server-side account as last synced. Cached so the balance shows instantly at startup.</summary>
public sealed class AccountState
{
    /// <summary>Pool worker name the server assigned to this account; mining is credited by it.</summary>
    public string? WorkerId { get; set; }

    /// <summary>Verified email that secures the account; null until the user adds one.</summary>
    public string? Email { get; set; }

    public int DeviceCount { get; set; } = 1;

    /// <summary>Operator wallet to mine to in balance mode, as announced by the server.</summary>
    public string? PoolWallet { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public decimal AvailableUsd { get; set; }

    public decimal PendingUsd { get; set; }

    public decimal LifetimeUsd { get; set; }

    public decimal ReferralUsd { get; set; }

    public decimal MinPayPalUsd { get; set; } = 5m;

    public decimal MinCryptoUsd { get; set; } = 5m;

    public double FeePercent { get; set; }

    /// <summary>Bandwidth sharing terms as last announced by the server.</summary>
    public bool BandwidthAvailable { get; set; }

    public double BandwidthUserUsdPerGb { get; set; }

    public double BandwidthUserUsdPerSharingHour { get; set; }

    public long BandwidthServerDailyLimitBytes { get; set; }

    public string? BandwidthPartnerName { get; set; }

    /// <summary>Last PayPal email used, to prefill the cash-out form.</summary>
    public string? LastPayPalEmail { get; set; }

    /// <summary>Last crypto address used, to prefill the cash-out form.</summary>
    public string? LastCryptoAddress { get; set; }

    /// <summary>Last address used per payout coin ("btc", "eth", ...), to prefill the cash-out form.</summary>
    public Dictionary<string, string> LastAddresses { get; set; } = [];

    /// <summary>The coin last used to cash out.</summary>
    public string? LastCashoutMethod { get; set; }

    /// <summary>Where the server last said this account's CPU should mine (kept so it survives a restart offline).</summary>
    public Accounts.CpuPoolRoute? CpuPool { get; set; }
}

public sealed class ReferralState
{
    /// <summary>Code the user typed during setup (the friend who invited them).</summary>
    public string? CodeUsed { get; set; }

    /// <summary>This user's own code to share.</summary>
    public string? MyCode { get; set; }

    public string? ShareUrl { get; set; }

    public string? ReferrerWallet { get; set; }

    public DateTimeOffset? ReferredAt { get; set; }

    public DateTimeOffset? FeeFreeUntil { get; set; }

    public int InvitedCount { get; set; }

    public int VerifiedCount { get; set; }

    /// <summary>Payout address last sent to the server, so a wallet change triggers re-registration.</summary>
    public string? RegisteredPayoutAddress { get; set; }

    /// <summary>Why the server rejected the invite code the user entered, if it did.</summary>
    public string? CodeError { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }
}

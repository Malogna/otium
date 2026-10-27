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

    /// <summary>Pause the graphics-card miner while Salad (installed by the user) runs a job on the card.</summary>
    public bool YieldGpuToSalad { get; set; } = true;

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

    /// <summary>
    /// Keep the computer from sleeping while it earns on mains power (the screen still turns off). On by default:
    /// without it Windows' usual power plan sleeps 15–30 minutes after the user leaves and the miners stop for the night.
    /// </summary>
    public bool KeepAwakeWhileEarning { get; set; } = true;

    /// <summary>
    /// NVIDIA cards mine with their power limit lowered (see <see cref="Gpu.GpuEfficiency"/>). Off until the user turns it
    /// on in Settings, which asks Windows for administrator permission once.
    /// </summary>
    public bool GpuEfficiencyMode { get; set; }

    /// <summary>
    /// "Earn while I browse": the graphics card also mines while the user is at the computer doing ordinary things
    /// (never during full-screen apps or games, or on battery). Off unless the user turns it on.
    /// </summary>
    public bool EarnWhileBrowsing { get; set; }

    /// <summary>Desktop notifications for earnings, payouts and problems. On by default; one switch in Settings.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>The pool's "paid" total last seen, per pool and coin, so a new payout is noticed once.</summary>
    public Dictionary<string, double> NotifiedPaid { get; set; } = [];

    /// <summary>How many internet sharing payouts were already announced.</summary>
    public int NotifiedSharingPayouts { get; set; }

    /// <summary>The month ("2026-10") whose report was already announced.</summary>
    public string? MonthlyNotified { get; set; }

    public DateTimeOffset? ProblemNotifiedAt { get; set; }

    public int IdleMinutes { get; set; } = 3;

    /// <summary>Stop automatically when estimated earnings don't cover the electricity.</summary>
    public bool NeverRunAtLoss { get; set; } = true;

    /// <summary>User-supplied CPU power at full load. Null means estimate from the CPU.</summary>
    public double? CpuFullLoadWatts { get; set; }

    /// <summary>Whether earning was switched on when the app last closed, so it resumes after a restart.</summary>
    public bool EarningEnabled { get; set; }

    public bool AcceptedTerms { get; set; }

    /// <summary>
    /// The user said they're a US citizen or resident: unMineable doesn't serve US persons anywhere, so they're paid in
    /// Monero by MoneroOcean, like computers in the US region.
    /// </summary>
    public bool UsPerson { get; set; }

    /// <summary>
    /// The user answered the US question themselves (ticked or unticked it), so <see cref="UsPerson"/> stands even when
    /// Windows' region says otherwise. Until then the region decides: fresh English installs often say "United States".
    /// </summary>
    public bool UsPersonAnswered { get; set; }

    /// <summary>Why the Otium server says Otium isn't available where this computer is (sanctions, mining bans), or null.</summary>
    public string? UnavailableReason { get; set; }

    /// <summary>Share unused internet bandwidth through Otium's partner. Off until the user opts in.</summary>
    public bool BandwidthEnabled { get; set; }

    /// <summary>When the user agreed to the bandwidth-sharing explanation; sharing never runs without it.</summary>
    public DateTimeOffset? BandwidthConsentAt { get; set; }

    /// <summary>Share only while the user is away, like mining. Off by default because sharing barely uses the computer.</summary>
    public bool BandwidthOnlyWhenIdle { get; set; }

    /// <summary>Most data to share per day, in GB.</summary>
    public double BandwidthDailyLimitGb { get; set; } = 2;

    /// <summary>
    /// The user's own 0x address that internet sharing earnings are paid to (Proxybase: USDC on Tempo), when their
    /// payout address isn't already an 0x address (the same key works on every EVM network).
    /// </summary>
    public string? BandwidthPayoutAddress { get; set; }

    /// <summary>
    /// Optional "QTC:qz…": the user's own Quantus wallet. With it, the graphics card may also mine Quantus on Kryptex
    /// Pool (paid there, in QTC) whenever Quantus pays more than the coins paid to <see cref="PayoutAddress"/>.
    /// </summary>
    public string? QuantusAddress { get; set; }

    /// <summary>The user answered "Not now" to the internet sharing suggestion on the overview.</summary>
    public bool BandwidthPromptDismissed { get; set; }

    /// <summary>
    /// Also share internet through Mysterium (next to Proxybase above): its node pays MYST straight to the user's own
    /// Polygon address. Off until the user opts in on its own explanation screen.
    /// </summary>
    public bool MysteriumEnabled { get; set; }

    /// <summary>When the user agreed to Mysterium's explanation; its node never runs without it.</summary>
    public DateTimeOffset? MysteriumConsentAt { get; set; }

    /// <summary>Share through Mysterium only while the user is away.</summary>
    public bool MysteriumOnlyWhenIdle { get; set; }

    /// <summary>Most data to share through Mysterium per day, in GB.</summary>
    public double MysteriumDailyLimitGb { get; set; } = 5;

    /// <summary>The user's own Polygon (0x…) address Mysterium pays, when their payout address isn't an 0x address.</summary>
    public string? MysteriumPayoutAddress { get; set; }

    /// <summary>The user answered "Not now" to the overview's suggestion to also share through Mysterium.</summary>
    public bool MysteriumPromptDismissed { get; set; }

    /// <summary>
    /// Also share storage and upload bandwidth through an AIOZ DePIN node: it pays AIOZ, which Otium withdraws to the
    /// user's own 0x address. Off until the user opts in on its own explanation screen.
    /// </summary>
    public bool AiozEnabled { get; set; }

    /// <summary>When the user agreed to AIOZ's explanation; its node never runs without it.</summary>
    public DateTimeOffset? AiozConsentAt { get; set; }

    /// <summary>Share through AIOZ only while the user is away.</summary>
    public bool AiozOnlyWhenIdle { get; set; }

    /// <summary>Most disk space AIOZ's files may use, in GB.</summary>
    public double AiozStorageLimitGb { get; set; } = 20;

    /// <summary>The user's own 0x address AIOZ is withdrawn to, when their payout address isn't an 0x address.</summary>
    public string? AiozPayoutAddress { get; set; }

    /// <summary>The user answered "Not now" to the overview's suggestion to also share through AIOZ.</summary>
    public bool AiozPromptDismissed { get; set; }

    /// <summary>The last "measure my PC" test, or null until one finished.</summary>
    public Projections.PcMeasurement? Measurement { get; set; }

    /// <summary>The user skipped the first-run test, so it isn't offered again by itself (it stays in Home's card).</summary>
    public bool MeasurementSkipped { get; set; }

    /// <summary>The month ("2026-10") whose report the user closed, so it doesn't come back.</summary>
    public string? MonthlyReportDismissed { get; set; }

    /// <summary>
    /// Send what this computer earns (graphics card model, processor name, hours, amounts; never an address) for the
    /// real-earnings table on otium.lol. Asked as a plain yes or no on the welcome screen (nothing preselected: it's
    /// consent); Settings turns it on or off.
    /// </summary>
    public bool ShareEarningsStats { get; set; }

    /// <summary>When the user last answered "Share analytics", as a record of their choice.</summary>
    public DateTimeOffset? ShareEarningsAnsweredAt { get; set; }

    /// <summary>The user stopped sharing earnings and the server hasn't yet deleted what this computer shared.</summary>
    public bool DeleteSharedEarnings { get; set; }

    /// <summary>
    /// When the user comes back, pause the processor miner instead of stopping it, keeping its memory (about 2.5 GB) for
    /// up to 10 minutes so earning resumes at once when they leave again. Off unless the user agrees.
    /// </summary>
    public bool KeepMinerReadyWhenBack { get; set; }

    /// <summary>
    /// Install a downloaded update by itself while the user is away and the window is closed, instead of waiting for
    /// "Restart to update". Mining pauses for the few seconds the restart takes.
    /// </summary>
    public bool InstallUpdatesWhileAway { get; set; } = true;

    public EarningsReportState EarningsReports { get; set; } = new();

    public ReferralState Referral { get; set; } = new();

    public AccountState Account { get; set; } = new();

    /// <summary>Random per-install secret that authenticates this device to the Otium server.</summary>
    public string DeviceSecret { get; set; } = "";

    /// <summary>Random seed used for the device ID when the OS exposes no machine ID.</summary>
    public string DeviceSeed { get; set; } = "";

    /// <summary>
    /// The device ID comes from this install's seed, not the machine ID: set when the server already knew this
    /// computer from an earlier install (whose secret is gone), so this install registers as a new computer.
    /// </summary>
    public bool InstallDeviceId { get; set; }
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

    /// <summary>The server pays new Mysterium nodes' registration fee, so internet sharing can be offered.</summary>
    public bool MysteriumSponsored { get; set; }

    /// <summary>The operator switched internet sharing through Proxybase off.</summary>
    public bool ProxybaseDisabled { get; set; }

    /// <summary>Today's MYST price in US dollars as the server last sent it, for Mysterium sharing earnings; null until then.</summary>
    public double? MystUsd { get; set; }

    /// <summary>Today's AIOZ price in US dollars as the server last sent it, for AIOZ sharing earnings; null until then.</summary>
    public double? AiozUsd { get; set; }

    /// <summary>The fee and invite terms the server last announced; null until it has.</summary>
    public Accounts.FeeTerms? Fees { get; set; }

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
    /// <summary>Code the user typed during setup (the friend who invited them), or that their invite link filled in.</summary>
    public string? CodeUsed { get; set; }

    /// <summary>The server has been asked once whether a friend's invite link was opened on this connection.</summary>
    public bool InviteLinkChecked { get; set; }

    /// <summary>Estimated, from the friends' apps: what invited friends' mining has paid to this user's address.</summary>
    public double EarnedFromFriendsUsd { get; set; }

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

    /// <summary>The server's signed fee grant for this computer (see Fees.FeeGrant): editing it only voids it.</summary>
    public string? FeeGrant { get; set; }

    public string? FeeGrantSignature { get; set; }
}

/// <summary>
/// Where the opt-in earnings reports left off: each device's running totals at the last report, so the next one sends
/// only what was earned since. Reset when the user turns sharing on, so nothing from before is sent.
/// </summary>
public sealed class EarningsReportState
{
    public long Sequence { get; set; }

    public DateTimeOffset? LastAt { get; set; }

    /// <summary>Per device ("cpu", "gpu"): mining seconds, user USD and electricity USD at the last report.</summary>
    public Dictionary<string, double[]> Baselines { get; set; } = [];
}

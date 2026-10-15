using Otium.Core.Power;

namespace Otium.Core.Settings;

/// <summary>How a user gets paid.</summary>
public enum PayoutMode
{
    /// <summary>
    /// Default. Mining goes to the operator's pool account under the user's worker id; the Otium server credits
    /// a USD balance the user cashes out by PayPal or crypto. No wallet needed.
    /// </summary>
    Balance,

    /// <summary>Advanced. The pool pays the user's own Monero wallet directly; nothing passes through Otium.</summary>
    OwnWallet,
}

public sealed class AppSettings
{
    public bool OnboardingComplete { get; set; }

    public PayoutMode PayoutMode { get; set; } = PayoutMode.Balance;

    /// <summary>User's own XMR address, used only in <see cref="PayoutMode.OwnWallet"/>.</summary>
    public string PayoutAddress { get; set; } = "";

    /// <summary>Price per kWh in USD. Starts as an estimate for the user's country.</summary>
    public double ElectricityPricePerKwh { get; set; } = ElectricityPrices.ForCurrentRegion();

    /// <summary>True until the user enters their own price.</summary>
    public bool ElectricityPriceIsEstimate { get; set; } = true;

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

    public decimal MinCryptoUsd { get; set; } = 2m;

    public double FeePercent { get; set; }

    /// <summary>Last PayPal email used, to prefill the cash-out form.</summary>
    public string? LastPayPalEmail { get; set; }

    /// <summary>Last crypto address used, to prefill the cash-out form.</summary>
    public string? LastCryptoAddress { get; set; }
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

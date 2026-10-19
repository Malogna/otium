using System.Reflection;

namespace Otium.Core;

/// <summary>
/// Operator-level settings. Whoever publishes Otium sets these before shipping a build, either here or at build time
/// (<c>dotnet publish -p:OtiumServerUrl=... -p:OtiumFeeWallet=... -p:OtiumUnmineableReferral=...</c>, which is what
/// build/package.ps1 and the release workflow do). Build-time values win over the defaults below; environment
/// variables win over both.
/// </summary>
public static class AppIdentity
{
    /// <summary>Where users ask for help: the project's public issue tracker.</summary>
    public const string HelpUrl = "https://github.com/Malogna/otium/issues";

    /// <summary>Otium's public email address (Cloudflare Email Routing forwards it), named in the terms and privacy policy.</summary>
    public const string ContactEmail = "contact@otium.lol";

    /// <summary>Otium's community Discord server (a permanent invite link).</summary>
    public const string DiscordUrl = "https://discord.gg/4FtFDrz97t";

    public const string Name = "Otium";

    /// <summary>
    /// Payout targets that receive the disclosed app fee, separated by ";": one for unMineable (e.g. "USDT:T…") and
    /// optionally one for MoneroOcean ("XMR:4…"), since a fee slice is mined on the user's own pool. While this is
    /// empty the fee slice is mined for the user instead, so development builds never send hashes to an address
    /// nobody controls. Can be overridden at runtime with the OTIUM_FEE_WALLET environment variable.
    /// </summary>
    public static readonly string FeeWalletAddress = BuildSetting("Otium.FeeWallet") ?? "";

    /// <summary>
    /// Otium's unMineable referral code, added to every unMineable login ("…#code"). Users pay unMineable 0.75%
    /// instead of 1%, and Otium receives 0.25–0.50% of their rewards from unMineable. Overridable with
    /// OTIUM_UNMINEABLE_REFERRAL ("none" turns it off).
    /// </summary>
    public static readonly string UnmineableReferralCode = BuildSetting("Otium.UnmineableReferral") ?? "ztjq-jr7s";

    /// <summary>
    /// The Otium server (accounts, balances, cash-outs, referrals). Release builds set the production URL at build time;
    /// local builds talk to a server on this computer. Can be overridden at runtime with OTIUM_SERVER_URL.
    /// </summary>
    public static readonly string DefaultServerUrl = BuildSetting("Otium.ServerUrl") ?? "http://localhost:5080";

    /// <summary>
    /// Where installed copies look for updates: a GitHub repository URL (releases created by the release workflow)
    /// or any static URL hosting a Velopack release feed. Empty disables update checks.
    /// Can be overridden at runtime with the OTIUM_UPDATE_URL environment variable.
    /// </summary>
    public const string UpdateUrl = "https://github.com/Malogna/otium";

    public static string ResolveServerUrl(string? configured)
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(overridden))
            return overridden.Trim();
        return string.IsNullOrWhiteSpace(configured) ? DefaultServerUrl : configured.Trim();
    }

    public static string? ResolveUpdateUrl()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_UPDATE_URL");
        var url = string.IsNullOrWhiteSpace(overridden) ? UpdateUrl : overridden.Trim();
        return url.Length == 0 ? null : url;
    }

    /// <summary>
    /// The configured fee targets that parse ("COIN:address", a bare Monero address, or "QTC:qz…" for the graphics
    /// card's Quantus), canonical form.
    /// </summary>
    public static IReadOnlyList<string> ResolveFeeWallets()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_FEE_WALLET");
        var value = string.IsNullOrWhiteSpace(overridden) ? FeeWalletAddress : overridden;
        return value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Wallets.PayoutTarget.ParseAnyOrNull)
            .OfType<Wallets.PayoutTarget>()
            .Select(t => t.ToString())
            .Distinct()
            .ToList();
    }

    public static string? ResolveUnmineableReferral()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_UNMINEABLE_REFERRAL")?.Trim();
        var code = string.IsNullOrEmpty(overridden) ? UnmineableReferralCode : overridden;
        return code.Length == 0 || code.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : code;
    }

    /// <summary>
    /// Honeygain SDK publisher key for internet sharing, from the Honeygain developer dashboard. Empty means internet
    /// sharing is unavailable in this build. Release builds can set it here or via OTIUM_HONEYGAIN_KEY.
    /// </summary>
    public static readonly string HoneygainApiKey = BuildSetting("Otium.HoneygainKey") ?? "";

    /// <summary>A value baked in at build time with <c>[AssemblyMetadata]</c> (see Otium.Core.csproj), or null.</summary>
    private static string? BuildSetting(string key)
    {
        var value = typeof(AppIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? ResolveHoneygainApiKey()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_HONEYGAIN_KEY");
        var key = string.IsNullOrWhiteSpace(overridden) ? HoneygainApiKey : overridden.Trim();
        return key.Length == 0 ? null : key;
    }
}

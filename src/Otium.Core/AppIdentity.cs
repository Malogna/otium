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
    /// The Otium server (accounts, balances, cash-outs, referrals). Release builds set the production URL at build time
    /// and always use it; local builds talk to a server on this computer, or OTIUM_SERVER_URL.
    /// </summary>
    public static readonly string DefaultServerUrl = BuildSetting("Otium.ServerUrl") ?? "http://localhost:5080";

    /// <summary>
    /// A build made for users (the release workflow bakes in the production server). Such a build ignores every
    /// developer override (OTIUM_SERVER_URL, OTIUM_FEE_WALLET, OTIUM_SIMULATE…): what comes from otium.lol mines with
    /// Otium's fee, and only a build someone makes from the source themselves can change that.
    /// </summary>
    public static bool IsReleaseBuild { get; } = BuildSetting("Otium.ServerUrl") is not null;

    /// <summary>A developer override from the environment; always null in a release build.</summary>
    public static string? DevelopmentOverride(string name) =>
        IsReleaseBuild ? null : Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    /// <summary>
    /// The keys the Otium server signs fee grants with (base64 SubjectPublicKeyInfo, ECDSA P-256; see Fees.FeeGrant).
    /// The private half lives only on the server and the developer's computer.
    /// </summary>
    public static readonly IReadOnlyList<string> FeeGrantKeys =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKInWm5ld+GoqHDuUMTh6z2Un7u5wY8oqOGoT6ANeFM9P+sKUpmj05cUW5av4SddKYsDVuc9vRxmcgoSDyZ4iTQ==", // the server's, since 0.1.1
    ];

    /// <summary>
    /// Where installed copies look for updates: a GitHub repository URL (releases created by the release workflow)
    /// or any static URL hosting a Velopack release feed. Empty disables update checks.
    /// Can be overridden at runtime with the OTIUM_UPDATE_URL environment variable.
    /// </summary>
    public const string UpdateUrl = "https://github.com/Malogna/otium";

    /// <summary>
    /// The keys update feeds must be signed with (base64 SubjectPublicKeyInfo, ECDSA P-256; see Updates.UpdateSignature):
    /// the developer's signing key, then a backup key kept offline in case that one is lost. Their private halves never
    /// go to GitHub or Otium's server, so neither can make an installed Otium run code the developer didn't sign.
    /// </summary>
    public static readonly IReadOnlyList<string> UpdateSigningKeys =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE7DHZKuimCgYHszCOTw3NvTcWOaup2Krt0cQdzvDiIpeLynmq+Ze5cOzr4sdsclk/grOgbwFWQkKLkIYESYSykw==", // f963b01b1bd06365, on the developer's computer
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEcWNwSWpREWQ/Murj8d33MIg7HGtSDHQwCnI6We0CoZQW+bSGhByrZEtULNvTb9HdgwYPfC9U4oiTF57Mr/oBNQ==", // 1a3770bcd22c7715, the offline backup
    ];

    /// <summary>The server this build talks to: always the built-in one in release builds.</summary>
    public static string ResolveServerUrl() => DevelopmentOverride("OTIUM_SERVER_URL")?.Trim() ?? DefaultServerUrl;

    public static string? ResolveUpdateUrl()
    {
        var overridden = DevelopmentOverride("OTIUM_UPDATE_URL");
        var url = string.IsNullOrWhiteSpace(overridden) ? UpdateUrl : overridden.Trim();
        return url.Length == 0 ? null : url;
    }

    /// <summary>
    /// The configured fee targets that parse ("COIN:address", a bare Monero address, or "QTC:qz…" for the graphics
    /// card's Quantus), canonical form.
    /// </summary>
    public static IReadOnlyList<string> ResolveFeeWallets()
    {
        var overridden = DevelopmentOverride("OTIUM_FEE_WALLET");
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
        var overridden = DevelopmentOverride("OTIUM_UNMINEABLE_REFERRAL")?.Trim();
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

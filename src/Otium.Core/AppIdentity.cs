using System.Reflection;

namespace Otium.Core;

/// <summary>
/// Operator-level settings. Whoever publishes Otium sets these before shipping a build, either here or at build time
/// (<c>dotnet publish -p:OtiumServerUrl=... -p:OtiumFeeWallet=... -p:OtiumHoneygainKey=...</c>, which is what
/// build/package.ps1 and the release workflow do). Build-time values win over the defaults below; environment
/// variables win over both.
/// </summary>
public static class AppIdentity
{
    public const string Name = "Otium";

    /// <summary>
    /// XMR address that receives the disclosed app fee. While this is empty the fee slice is mined for
    /// the user instead, so development builds never send hashes to an address nobody controls.
    /// Can be overridden at runtime with the OTIUM_FEE_WALLET environment variable.
    /// </summary>
    public static readonly string FeeWalletAddress = BuildSetting("Otium.FeeWallet") ?? "";

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

    public static string? ResolveFeeWallet()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_FEE_WALLET");
        var wallet = string.IsNullOrWhiteSpace(overridden) ? FeeWalletAddress : overridden.Trim();
        return wallet.Length == 0 ? null : wallet;
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

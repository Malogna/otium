namespace Otium.Core;

/// <summary>
/// Operator-level constants. Whoever publishes Otium sets these before shipping a build.
/// </summary>
public static class AppIdentity
{
    public const string Name = "Otium";

    /// <summary>
    /// XMR address that receives the disclosed app fee. While this is empty the fee slice is mined for
    /// the user instead, so development builds never send hashes to an address nobody controls.
    /// Can be overridden at runtime with the OTIUM_FEE_WALLET environment variable.
    /// </summary>
    public const string FeeWalletAddress = "";

    /// <summary>
    /// The Otium server (accounts, balances, cash-outs, referrals). Replace with the production URL when deployed.
    /// Can be overridden at runtime with the OTIUM_SERVER_URL environment variable.
    /// </summary>
    public const string DefaultServerUrl = "http://localhost:5080";

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
    public const string HoneygainApiKey = "";

    public static string? ResolveHoneygainApiKey()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_HONEYGAIN_KEY");
        var key = string.IsNullOrWhiteSpace(overridden) ? HoneygainApiKey : overridden.Trim();
        return key.Length == 0 ? null : key;
    }
}

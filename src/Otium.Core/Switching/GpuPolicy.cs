using System.Text.RegularExpressions;

namespace Otium.Core.Switching;

/// <summary>
/// What the Otium server tells apps about GPU mining: which algorithms may be mined, where, and what each pays right
/// now. The server builds it from live profitability feeds, so a coin that collapses or a pool that changes its terms
/// can be dropped for every user at once without an app update (the "kill switch").
/// </summary>
public sealed record GpuPolicy(DateTimeOffset GeneratedAt, IReadOnlyList<GpuAlgorithmPolicy> Algorithms, IReadOnlyList<GpuPoolRoute> Routes)
{
    /// <summary>Stands in until the server's policy arrives for coins only it prices (unMineable's): nothing to mine yet.</summary>
    public static GpuPolicy NotYetFetched { get; } = new(DateTimeOffset.MinValue, [], []);

    /// <summary>This is <see cref="NotYetFetched"/>, or a filtered copy of it: the server hasn't answered yet.</summary>
    public bool IsNotYetFetched => GeneratedAt == DateTimeOffset.MinValue;

    public GpuAlgorithmPolicy? For(string algorithm) =>
        Algorithms.FirstOrDefault(a => string.Equals(a.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase));

    public GpuPoolRoute? Route(string name) =>
        Routes.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The policy without operator pools: Kryptex account coins are mined to Otium's own Kryptex account, so they don't
    /// apply to users the pool pays directly. Kryptex's address login (Quantus to the user's own wallet) stays.
    /// </summary>
    public GpuPolicy WithoutOperatorPools() => OnlyRoutes(GpuRoutes.MoneroOcean, GpuRoutes.Unmineable, GpuRoutes.KryptexAddress);

    /// <summary>
    /// The pool each kind of route may send a graphics card to, whatever the server says. A compromised or mistaken
    /// server could otherwise point own-wallet users' cards at a proxy that keeps the work while the login still shows
    /// the user's own address.
    /// </summary>
    private static readonly Dictionary<string, string> TrustedPoolDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        [GpuRoutes.Unmineable] = "unmineable.com",
        [GpuRoutes.KryptexAddress] = "kryptex.network",
        [GpuRoutes.MoneroOcean] = "moneroocean.stream",
    };

    /// <summary>The route's host belongs to the pool its kind names (e.g. "pearlpow.unmineable.com" for unMineable).</summary>
    public static bool IsTrustedHost(GpuPoolRoute route) =>
        TrustedPoolDomains.TryGetValue(route.Kind, out var domain)
        && (string.Equals(route.Host, domain, StringComparison.OrdinalIgnoreCase)
            || route.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

    /// <summary>The policy without routes whose host isn't on their pool's own domain (see <see cref="IsTrustedHost"/>).</summary>
    public GpuPolicy OnlyTrustedHosts()
    {
        var routes = Routes.Where(IsTrustedHost).ToList();
        return this with
        {
            Routes = routes,
            Algorithms = Algorithms.Where(a => routes.Any(r => string.Equals(r.Name, a.Route, StringComparison.OrdinalIgnoreCase))).ToList(),
        };
    }

    /// <summary>The policy limited to routes of the given kinds, e.g. only MoneroOcean for a Monero payout.</summary>
    public GpuPolicy OnlyRoutes(params string[] kinds)
    {
        var routes = Routes.Where(r => kinds.Contains(r.Kind, StringComparer.OrdinalIgnoreCase)).ToList();
        return this with
        {
            Routes = routes,
            Algorithms = Algorithms.Where(a => routes.Any(r => string.Equals(r.Name, a.Route, StringComparison.OrdinalIgnoreCase))).ToList(),
        };
    }
}

/// <param name="Algorithm">SRBMiner-side identity of the algorithm, e.g. "pearlhash".</param>
/// <param name="Tier">One of <see cref="GpuTiers"/>.</param>
/// <param name="Route">Name of the <see cref="GpuPoolRoute"/> to mine it on.</param>
/// <param name="UsdPerHashPerSecondDay">Gross revenue per day for 1 H/s, before pool and miner fees.</param>
/// <param name="PoolFeePercent">The pool's fee for this algorithm.</param>
/// <param name="Note">Why it is in this tier, for Advanced settings.</param>
/// <param name="OnlyGpus">
/// When set, only graphics cards whose name matches one of these patterns (case-insensitive regular expressions, e.g.
/// "RTX 50\d\d") may mine it; other cards skip it. Pearl's FP8 fork (PIP-3) whitelists only Hopper and Blackwell
/// cards, so the server sets this for pearlhash once the fork activates.
/// </param>
public sealed record GpuAlgorithmPolicy(
    string Algorithm,
    string Tier,
    string Route,
    double UsdPerHashPerSecondDay,
    double PoolFeePercent,
    string? Note = null,
    IReadOnlyList<string>? OnlyGpus = null)
{
    /// <summary>Whether every one of these cards may mine it (always, without <see cref="OnlyGpus"/>).</summary>
    public bool Allows(IEnumerable<string> gpuNames) => gpuNames.All(AllowsCard);

    /// <summary>Whether this card may mine it: on a PC with several cards, the others can mine it without this one.</summary>
    public bool AllowsCard(string gpuName) => OnlyGpus is not { Count: > 0 } patterns || patterns.Any(p => Matches(p, gpuName));

    private static bool Matches(string pattern, string name)
    {
        try
        {
            return Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        }
        catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException)
        {
            return false; // a broken pattern from the server allows nothing rather than everything
        }
    }
}

public static class GpuTiers
{
    /// <summary>Benchmarked and mined whenever it pays more than electricity.</summary>
    public const string Primary = "primary";

    /// <summary>
    /// Usually loses money on electricity: not benchmarked or mined unless the user's electricity is free (price 0),
    /// which keeps first-run testing short and never burns power on a losing coin.
    /// </summary>
    public const string FreePowerOnly = "free-power-only";

    /// <summary>Switched off by the operator or by a failed/stale profitability feed.</summary>
    public const string Disabled = "disabled";
}

/// <param name="Name">Unique route name that algorithms refer to, e.g. "moneroocean" or "kryptex-prl".</param>
/// <param name="Kind">One of <see cref="GpuRoutes"/>: decides how the miner logs in.</param>
/// <param name="User">
/// Pool account the work is credited to (the operator's Kryptex account); the miner logs in as "User.worker".
/// Null for MoneroOcean, which uses the account's pool wallet and pins the algorithm in the password, for unMineable,
/// which logs in with the payout target ("COIN:address.worker#referral"), and for Kryptex's address login, which logs
/// in with the user's own coin address.
/// </param>
/// <param name="Tls">Connect with TLS (unMineable's 443 and 4444 ports).</param>
public sealed record GpuPoolRoute(string Name, string Kind, string Host, int Port, string? User, bool Tls = false);

public static class GpuRoutes
{
    public const string MoneroOcean = "moneroocean";
    public const string Kryptex = "kryptex";

    /// <summary>unMineable: mines the algorithm's coin and pays the user's own address in the coin they chose.</summary>
    public const string Unmineable = "unmineable";

    /// <summary>
    /// Kryptex Pool without an account: the miner logs in with the user's own wallet for the coin (Quantus, "qz…"),
    /// and Kryptex pays that wallet in the coin itself. Only for users who added an address for that coin.
    /// </summary>
    public const string KryptexAddress = "kryptex-address";
}

/// <summary>
/// The last GPU policy the server sent, kept on disk: Otium starts its miners before its first call to the server, so
/// without it the graphics card had nothing to mine for its first minute after every start (and every update).
/// </summary>
public sealed class CachedGpuPolicy
{
    /// <summary>
    /// How old a kept policy may be and still be used at startup. The server's own arrives within a minute, so a coin it
    /// has since dropped is mined for a minute at most.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    public GpuPolicy? Policy { get; set; }

    public DateTimeOffset? FetchedAt { get; set; }

    /// <summary>The kept policy, or null when there is none or it's older than <see cref="MaxAge"/>.</summary>
    public GpuPolicy? UsableAt(DateTimeOffset now) =>
        Policy is not null && FetchedAt is { } at && now - at <= MaxAge && now >= at ? Policy : null;
}

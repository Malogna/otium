namespace Otium.Core.Switching;

/// <summary>
/// What the Otium server tells apps about GPU mining: which algorithms may be mined, where, and what each pays right
/// now. The server builds it from live profitability feeds, so a coin that collapses or a pool that changes its terms
/// can be dropped for every user at once without an app update (the "kill switch").
/// </summary>
public sealed record GpuPolicy(DateTimeOffset GeneratedAt, IReadOnlyList<GpuAlgorithmPolicy> Algorithms, IReadOnlyList<GpuPoolRoute> Routes)
{
    public GpuAlgorithmPolicy? For(string algorithm) =>
        Algorithms.FirstOrDefault(a => string.Equals(a.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase));

    public GpuPoolRoute? Route(string name) =>
        Routes.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The policy without operator pools. Own-wallet users are paid by MoneroOcean directly, so Kryptex coins (mined
    /// to Otium's Kryptex account) don't apply to them.
    /// </summary>
    public GpuPolicy WithoutOperatorPools()
    {
        var routes = Routes.Where(r => r.Kind == GpuRoutes.MoneroOcean).ToList();
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
public sealed record GpuAlgorithmPolicy(
    string Algorithm,
    string Tier,
    string Route,
    double UsdPerHashPerSecondDay,
    double PoolFeePercent,
    string? Note = null);

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
/// Null for MoneroOcean, which uses the account's pool wallet and pins the algorithm in the password.
/// </param>
public sealed record GpuPoolRoute(string Name, string Kind, string Host, int Port, string? User);

public static class GpuRoutes
{
    public const string MoneroOcean = "moneroocean";
    public const string Kryptex = "kryptex";
}

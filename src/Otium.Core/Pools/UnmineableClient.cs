using System.Globalization;
using System.Text.Json;
using Otium.Core.Providers;
using Otium.Core.Wallets;

namespace Otium.Core.Pools;

/// <summary>
/// unMineable's view of one payout address. unMineable creates an account the first time a miner logs in with an
/// address, so an address nobody has mined to yet has no <see cref="Uuid"/>.
/// </summary>
/// <param name="Balance">Mined and not yet paid, in the coin.</param>
/// <param name="Payable">The part of <paramref name="Balance"/> that can be paid now.</param>
/// <param name="Threshold">Minimum payout for this address's network.</param>
/// <param name="AutoPay">Whether unMineable pays automatically once the threshold is reached. When false the user must
/// request each payout on unMineable's site.</param>
/// <param name="Network">"TRX", "BSC", "BTC"…: the network unMineable will pay on.</param>
/// <param name="Problems">Plain-language problems unMineable flags on the address (failed payment, missing memo…).</param>
/// <param name="Maintenance">unMineable's own notice when payouts of this coin are paused.</param>
public sealed record UnmineableAddress(
    string? Uuid,
    decimal Balance,
    decimal Payable,
    decimal Threshold,
    bool AutoPay,
    bool Enabled,
    string Network,
    IReadOnlyList<string> Problems,
    string? Maintenance);

/// <param name="MiningBalance">From the address's own workers.</param>
/// <param name="ReferralBalance">From miners who used this address's referral code.</param>
/// <param name="LastPayment">Null when nothing has been paid yet.</param>
public sealed record UnmineableStats(
    string Coin,
    decimal MiningBalance,
    decimal ReferralBalance,
    decimal Balance,
    decimal Rewarded24h,
    decimal Rewarded7d,
    decimal Rewarded30d,
    decimal Paid,
    DateTimeOffset? LastPayment,
    decimal Threshold);

public sealed record UnmineablePayment(DateTimeOffset Time, decimal Amount, string Network, string TransactionId, string Status, string? ExplorerUrl);

/// <summary>One worker on one algorithm.</summary>
/// <param name="CalculatedHps">Hashrate unMineable computes from accepted shares (what it pays for), averaged over the
/// last hour: a worker that started 10 minutes ago shows a sixth of its real rate.</param>
/// <param name="ReportedHps">Hashrate the miner reports, when it does.</param>
/// <param name="Referral">The referral code the worker logged in with ("…#code"), or null.</param>
/// <param name="LastSeen">When the worker last sent a share, or null.</param>
public sealed record UnmineableWorker(
    string Algorithm,
    string Name,
    bool Online,
    double CalculatedHps,
    double ReportedHps,
    string? Referral = null,
    DateTimeOffset? LastSeen = null);

/// <summary>An address's referral code and how many miners use it ("o***f"-style names only, so just the count).</summary>
public sealed record UnmineableReferrals(string Code, int Miners);

public interface IUnmineableApi
{
    Task<UnmineableAddress> GetAddressAsync(PayoutTarget target, CancellationToken ct);

    Task<UnmineableStats> GetStatsAsync(string uuid, CancellationToken ct);

    Task<IReadOnlyList<UnmineableWorker>> GetWorkersAsync(string uuid, CancellationToken ct);

    Task<IReadOnlyList<UnmineablePayment>> GetPaymentsAsync(string uuid, CancellationToken ct);

    Task<UnmineableReferrals> GetReferralsAsync(string uuid, CancellationToken ct);
}

/// <summary>
/// unMineable's public API (v5, no key needed). Amounts arrive as strings and timestamps in Unix milliseconds.
/// </summary>
public sealed class UnmineableClient(HttpClient http) : IUnmineableApi
{
    public static readonly Uri UnmineableApi = new("https://api.unminable.com/");

    /// <summary>The API in use: unMineable, or a simulated pool when OTIUM_UNMINEABLE_API is set (dev mode).</summary>
    public static Uri ApiBase { get; } = AppIdentity.DevelopmentOverride("OTIUM_UNMINEABLE_API") is { Length: > 0 } custom
        ? new Uri(custom.TrimEnd('/') + "/")
        : UnmineableApi;

    /// <summary>The address's page on unMineable, where users can check workers and request a payout.</summary>
    public static Uri DashboardUrl(PayoutTarget target) =>
        new($"https://unmineable.com/address/{Uri.EscapeDataString(target.Address)}?coin={Uri.EscapeDataString(target.Coin.Symbol)}");

    public async Task<UnmineableAddress> GetAddressAsync(PayoutTarget target, CancellationToken ct) =>
        ParseAddress(await GetDataAsync($"v5/address/{Uri.EscapeDataString(target.Address)}?coin={Uri.EscapeDataString(target.Coin.Symbol)}", ct));

    public async Task<UnmineableStats> GetStatsAsync(string uuid, CancellationToken ct) =>
        ParseStats(await GetDataAsync($"v5/account/{Uri.EscapeDataString(uuid)}/stats", ct));

    public async Task<IReadOnlyList<UnmineableWorker>> GetWorkersAsync(string uuid, CancellationToken ct) =>
        ParseWorkers(await GetDataAsync($"v5/account/{Uri.EscapeDataString(uuid)}/workers", ct));

    public async Task<IReadOnlyList<UnmineablePayment>> GetPaymentsAsync(string uuid, CancellationToken ct) =>
        ParsePayments(await GetDataAsync($"v5/account/{Uri.EscapeDataString(uuid)}/payments?page=1", ct));

    public async Task<UnmineableReferrals> GetReferralsAsync(string uuid, CancellationToken ct) =>
        ParseReferrals(await GetDataAsync($"v5/account/{Uri.EscapeDataString(uuid)}/referrals", ct));

    private async Task<JsonElement> GetDataAsync(string path, CancellationToken ct)
    {
        using var stream = await http.GetStreamAsync(new Uri(ApiBase, path), ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
        {
            var message = root.TryGetProperty("msg", out var m) ? m.ToString() : root.TryGetProperty("message", out var m2) ? m2.ToString() : "";
            throw new InvalidOperationException($"unMineable refused {path.Split('?')[0]}: {message}");
        }
        return root.TryGetProperty("data", out var data) ? data.Clone() : throw new InvalidOperationException($"unMineable sent no data for {path}.");
    }

    internal static UnmineableAddress ParseAddress(JsonElement data)
    {
        var problems = new List<string>();
        if (data.TryGetProperty("err_flags", out var flags) && flags.ValueKind == JsonValueKind.Object)
        {
            if (Flag(flags, "payment_error"))
                problems.Add("unMineable's last payment to this address failed.");
            if (Flag(flags, "missing_memo"))
                problems.Add("This address needs a memo that hasn't been set on unMineable.");
            if (Flag(flags, "not_activated"))
                problems.Add("This address isn't activated on its network yet, so payments can't arrive.");
            if (Flag(flags, "restricted"))
                problems.Add("unMineable has restricted this address.");
        }
        if (Flag(data, "delisted_chain"))
            problems.Add("unMineable no longer pays on this address's network.");

        return new UnmineableAddress(
            Text(data, "uuid") is { Length: > 0 } uuid ? uuid : null,
            Amount(data, "balance"),
            Amount(data, "balance_payable"),
            Amount(data, "payment_threshold"),
            Flag(data, "auto"),
            !data.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False,
            Text(data, "network") ?? "",
            problems,
            Text(data, "maintenance_message") is { Length: > 0 } notice ? notice : null);
    }

    internal static UnmineableReferrals ParseReferrals(JsonElement data)
    {
        var count = Number(data, "count");
        if (count <= 0 && data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
            count = list.GetArrayLength();
        return new UnmineableReferrals(Text(data, "custom_code") is { Length: > 0 } custom ? custom : Text(data, "code") ?? "", (int)count);
    }

    internal static UnmineableStats ParseStats(JsonElement data)
    {
        var rewarded = data.TryGetProperty("rewarded", out var r) && r.ValueKind == JsonValueKind.Object ? r : default;
        decimal Rewarded(string name) => rewarded.ValueKind == JsonValueKind.Object ? Amount(rewarded, name) : 0;
        var last = Number(data, "last_payment");
        return new UnmineableStats(
            Text(data, "coin") ?? "",
            Amount(data, "balance_mining"),
            Amount(data, "balance_referral"),
            Amount(data, "balance"),
            Rewarded("past_24h"),
            Rewarded("past_7d"),
            Rewarded("past_30d"),
            Amount(data, "paid"),
            last > 0 ? DateTimeOffset.FromUnixTimeMilliseconds((long)last) : null,
            Amount(data, "payment_threshold"));
    }

    internal static IReadOnlyList<UnmineablePayment> ParsePayments(JsonElement data)
    {
        if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            return [];
        return list.EnumerateArray()
            .Where(p => p.ValueKind == JsonValueKind.Object)
            .Select(p => new UnmineablePayment(
                DateTimeOffset.FromUnixTimeMilliseconds((long)Number(p, "timestamp")),
                Amount(p, "amount"),
                Text(p, "network") ?? "",
                Text(p, "tx") ?? "",
                Text(p, "status") ?? "",
                Text(p, "explorer_link") is { Length: > 0 } link && link.StartsWith("https://", StringComparison.Ordinal) ? link : null))
            .OrderByDescending(p => p.Time)
            .ToList();
    }

    /// <summary>
    /// Workers are grouped by algorithm ("randomx", "pearlpow"…), each with name, online, chr and rhr (hashrates as
    /// strings), referral and last (Unix milliseconds), as captured from a live address in October 2026.
    /// </summary>
    internal static IReadOnlyList<UnmineableWorker> ParseWorkers(JsonElement data)
    {
        var result = new List<UnmineableWorker>();
        if (data.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var algo in data.EnumerateObject())
        {
            if (algo.Value.ValueKind != JsonValueKind.Object
                || !algo.Value.TryGetProperty("workers", out var workers) || workers.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var w in workers.EnumerateArray().Where(w => w.ValueKind == JsonValueKind.Object))
            {
                result.Add(new UnmineableWorker(
                    algo.Name,
                    Text(w, "name") ?? Text(w, "worker") ?? "",
                    Flag(w, "online"),
                    Number(w, "chr"),
                    Number(w, "rhr"),
                    Text(w, "referral") is { Length: > 0 } code ? code : null,
                    Number(w, "last") is > 0 and var last ? DateTimeOffset.FromUnixTimeMilliseconds((long)last) : null));
            }
        }
        return result;
    }

    /// <summary>
    /// What unMineable holds for a target, as the app shows it. An address nobody has mined to yet reads as an empty
    /// balance with the coin's threshold.
    /// </summary>
    public static async Task<PayoutBalance> GetPayoutBalanceAsync(IUnmineableApi api, PayoutTarget target, CancellationToken ct)
    {
        var address = await api.GetAddressAsync(target, ct);
        var stats = address.Uuid is { } uuid ? await api.GetStatsAsync(uuid, ct) : null;
        var threshold = address.Threshold > 0 ? address.Threshold : target.Coin.Threshold;
        var problems = address.Problems.ToList();
        if (address.Maintenance is { } maintenance)
            problems.Add($"unMineable: {maintenance}");
        else if (!address.Enabled)
            problems.Add($"unMineable has paused payouts of {target.Coin.Ticker} for now.");
        return new PayoutBalance((double)address.Balance, (double)(stats?.Paid ?? 0), target.Coin.Ticker, (double)threshold,
            DashboardUrl(target), address.AutoPay, problems, stats?.LastPayment, "unMineable");
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && v.GetString() is "true" or "1"));

    /// <summary>A coin amount, sent as "0.138709" (or occasionally as a number).</summary>
    private static decimal Amount(JsonElement e, string name) =>
        Text(e, name) is { } s && decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static double Number(JsonElement e, string name) =>
        Text(e, name) is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}

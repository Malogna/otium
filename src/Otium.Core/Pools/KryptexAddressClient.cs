using System.Globalization;
using System.Text.Json;
using Otium.Core.Providers;
using Otium.Core.Wallets;

namespace Otium.Core.Pools;

/// <summary>Kryptex Pool's view of a coin address mined to without an account.</summary>
/// <param name="Unpaid">Mined and not yet paid (confirmed and still maturing), in the coin.</param>
/// <param name="Threshold">The address's minimum payout; 0 until anything has been mined to it.</param>
/// <param name="LastActive">When a worker last sent a share, or null if none ever has.</param>
public sealed record KryptexAddressBalance(decimal Unpaid, decimal Confirmed, decimal Threshold, DateTimeOffset? LastActive);

public interface IKryptexAddressApi
{
    Task<KryptexAddressBalance> GetBalanceAsync(string coin, string address, CancellationToken ct);

    /// <summary>Everything Kryptex has paid the address so far, in the coin.</summary>
    Task<decimal> GetPaidAsync(string coin, string address, CancellationToken ct);
}

/// <summary>
/// Kryptex Pool's public miner API (https://pool.kryptex.com/openapi.yaml, no key): balances and payouts of an address
/// mined to directly, e.g. a Quantus wallet. Kryptex pays such addresses by itself (hourly for Quantus, October 2026).
/// </summary>
public sealed class KryptexAddressClient(HttpClient http) : IKryptexAddressApi
{
    public static readonly Uri KryptexPool = new("https://pool.kryptex.com/");

    /// <summary>The pool in use: Kryptex, or a simulated pool when OTIUM_KRYPTEX_API is set (dev mode).</summary>
    public static Uri ApiBase { get; } = Environment.GetEnvironmentVariable("OTIUM_KRYPTEX_API") is { Length: > 0 } custom
        ? new Uri(custom.TrimEnd('/') + "/")
        : KryptexPool;

    /// <summary>Kryptex's coin slug for a payout coin ("qtc").</summary>
    public static string Slug(PayoutCoin coin) => coin.Symbol.ToLowerInvariant();

    /// <summary>The address's page on Kryptex Pool: workers, balance and payouts.</summary>
    public static Uri DashboardUrl(PayoutTarget target) =>
        new($"https://pool.kryptex.com/{Slug(target.Coin)}/miner/stats/{Uri.EscapeDataString(target.Address)}");

    public async Task<KryptexAddressBalance> GetBalanceAsync(string coin, string address, CancellationToken ct) =>
        ParseBalance(await GetAsync($"{Uri.EscapeDataString(coin)}/api/v1/miner/balance/{Uri.EscapeDataString(address)}", ct));

    public async Task<decimal> GetPaidAsync(string coin, string address, CancellationToken ct) =>
        Amount(await GetAsync($"{Uri.EscapeDataString(coin)}/api/v1/miner/payouts/{Uri.EscapeDataString(address)}/stats", ct), "paid");

    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        using var stream = await http.GetStreamAsync(new Uri(ApiBase, path), ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    /// <summary>{"total":2.54,"unconfirmed":0.71,"confirmed":1.83,"threshold":0.1,"last_active":1792321558000,…}</summary>
    internal static KryptexAddressBalance ParseBalance(JsonElement e)
    {
        var lastActive = e.TryGetProperty("last_active", out var la) && la.ValueKind == JsonValueKind.Number && la.GetInt64() > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(la.GetInt64())
            : (DateTimeOffset?)null;
        return new KryptexAddressBalance(Amount(e, "total"), Amount(e, "confirmed"), Amount(e, "threshold"), lastActive);
    }

    /// <summary>What Kryptex holds for a Quantus (or other directly mined) address, as the app shows pool balances.</summary>
    public static async Task<PayoutBalance> GetPayoutBalanceAsync(IKryptexAddressApi api, PayoutTarget target, CancellationToken ct)
    {
        var slug = Slug(target.Coin);
        var balance = await api.GetBalanceAsync(slug, target.Address, ct);
        var paid = await api.GetPaidAsync(slug, target.Address, ct);
        var threshold = balance.Threshold > 0 ? balance.Threshold : target.Coin.Threshold;
        return new PayoutBalance((double)balance.Unpaid, (double)paid, target.Coin.Ticker, (double)threshold, DashboardUrl(target),
            PoolName: "Kryptex");
    }

    private static decimal Amount(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.Number or JsonValueKind.String
        && decimal.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0;
}

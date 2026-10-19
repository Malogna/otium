using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Wallets;
using Otium.Server.Accounts;

namespace Otium.Server.Sharing;

/// <summary>
/// Turning internet sharing earnings (Proxybase pays only USDC on the Tempo network) into the coin each user chose, at
/// their own payout address, without Otium ever holding the money. For each payout the server opens a one-time
/// conversion and the app has Proxybase pay straight into it:
/// <list type="bullet">
/// <item>USDT (TRON), TRX, BTC, SOL, BNB, ETH: one Relay deposit address on Tempo that delivers to the user's address.</item>
/// <item>LTC: Relay to USDC on Base, into a SideShift shift that settles in LTC to the user.</item>
/// <item>XMR: Relay to USDC on Base, into a ChangeNOW exchange that pays XMR to the user.</item>
/// </list>
/// Otium's cut is not part of the conversion: of each payout the app keeps back Otium's share (<see cref="FeeBps"/>, the
/// mining fee by default) and pays it from the user's Proxybase earnings straight to <see cref="FeeRecipient"/> on Tempo,
/// the way mining pays Otium a slice of mining time. The conversion carries no Otium fee, so Otium earns nothing from
/// exchanging anyone's coins.
/// </summary>
public sealed class SharingOptions
{
    public const string Section = "Sharing";

    /// <summary>Relay API key (a secret; Relay requires one for quotes from October 2026).</summary>
    public string? RelayApiKey { get; set; }

    public string RelayApi { get; set; } = "https://api.relay.link/";

    /// <summary>Otium's own 0x address, which the app pays Otium's sharing fee to on Tempo. No fee without one.</summary>
    public string? FeeRecipient { get; set; }

    /// <summary>Otium's share of each sharing payout, in basis points (100 = 1%). Defaults to the mining fee.</summary>
    public int? FeeBps { get; set; }

    /// <summary>Wait for more to build up while the conversion's own network costs are above this share.</summary>
    public double MaxNetworkCostPercent { get; set; } = 5;

    /// <summary>From this much waiting, pay whatever the network costs (so nobody waits forever).</summary>
    public double PayAnywayUsd { get; set; } = 25;

    /// <summary>SideShift account secret and id (for LTC). Secrets; without them LTC can't be reached.</summary>
    public string? SideShiftSecret { get; set; }

    public string? SideShiftAffiliateId { get; set; }

    public string SideShiftApi { get; set; } = "https://sideshift.ai/api/v2/";

    /// <summary>ChangeNOW partner API key (for XMR). A secret; without it XMR can't be reached.</summary>
    public string? ChangeNowApiKey { get; set; }

    public string ChangeNowApi { get; set; } = "https://api.changenow.io/v2/";
}

/// <summary>A Relay deposit-address quote: where to pay and what comes out.</summary>
public sealed record RelayQuote(string DepositAddress, string? RequestId, string OutAmount, double InUsd, double OutUsd, double AppFeeUsd);

/// <summary>Where Relay delivers: its chain id and currency for an asset, and how to name it to the user.</summary>
public sealed record RelayDestination(long ChainId, string Currency, string Label);

public interface IRelayApi
{
    Task<RelayQuote> QuoteDepositAddressAsync(RelayDestination destination, string recipient, long amountMicro, string refundTo,
        string? feeRecipient, int feeBps, CancellationToken ct);
}

public interface ISwapToCoinApi
{
    /// <summary>Opens a conversion from USDC on Base to the coin, paid to <paramref name="settleAddress"/>; returns its deposit address on Base.</summary>
    Task<(string DepositAddress, decimal MinimumUsdc)> CreateAsync(string settleAddress, decimal usdcAmount, string refundAddress, string userIp, CancellationToken ct);
}

/// <summary>Relay's public API (api.relay.link): deposit-address quotes from Tempo USDC.</summary>
public sealed class RelayClient(HttpClient http, IOptions<SharingOptions> options) : IRelayApi
{
    public const long TempoChainId = 4217;

    /// <summary>USDC on Tempo (Stargate-bridged), 6 decimals: one unit is one Proxybase microcredit.</summary>
    public const string TempoUsdc = "0x20c000000000000000000000b9537d11c60e8b50";

    public static readonly RelayDestination BaseUsdc = new(8453, "0x833589fcd6edb6e08f4c7c32d4f71b54bda02913", "USDC (Base)");

    /// <summary>The payout coins Relay delivers to directly.</summary>
    public static RelayDestination? DirectFor(PayoutCoin coin) => coin.Symbol switch
    {
        "USDT" => new(728126428, "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", "USDT (TRON)"),
        "TRX" => new(728126428, "T9yD14Nj9j7xAB4dbGeiX9h8unkKHxuWwb", "TRX"),
        "BTC" => new(8253038, "bc1qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqmql8k8", "BTC"),
        "SOL" => new(792703809, "11111111111111111111111111111111", "SOL"),
        "BNB" => new(56, "0x0000000000000000000000000000000000000000", "BNB"),
        "ETH" => new(1, "0x0000000000000000000000000000000000000000", "ETH"),
        _ => null,
    };

    public async Task<RelayQuote> QuoteDepositAddressAsync(RelayDestination destination, string recipient, long amountMicro, string refundTo,
        string? feeRecipient, int feeBps, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["user"] = refundTo,
            ["originChainId"] = TempoChainId,
            ["destinationChainId"] = destination.ChainId,
            ["originCurrency"] = TempoUsdc,
            ["destinationCurrency"] = destination.Currency,
            ["amount"] = amountMicro.ToString(CultureInfo.InvariantCulture),
            ["tradeType"] = "EXACT_INPUT",
            ["recipient"] = recipient,
            // An open deposit address: a payout that arrives slightly smaller (a Proxybase fee) is re-quoted rather than
            // refunded; one that can't be filled at all is refunded to the user's own wallet on their computer.
            ["useDepositAddress"] = true,
            ["refundTo"] = refundTo,
        };
        if (feeRecipient is not null && feeBps > 0)
            body["appFees"] = new JsonArray(new JsonObject { ["recipient"] = feeRecipient, ["fee"] = feeBps.ToString(CultureInfo.InvariantCulture) });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.RelayApi), "quote/v2"))
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        if (options.Value.RelayApiKey is { Length: > 0 } key)
            request.Headers.Add("x-api-key", key);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Relay refused the quote ({(int)response.StatusCode}): {Trim(json)}");
        return ParseQuote(json);
    }

    internal static RelayQuote ParseQuote(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var deposit = root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array
            ? steps.EnumerateArray().Select(s => s.TryGetProperty("depositAddress", out var d) ? d.GetString() : null).FirstOrDefault(d => d is { Length: > 0 })
            : null;
        if (deposit is null)
            throw new InvalidOperationException("Relay's quote had no deposit address.");
        var details = root.GetProperty("details");
        return new RelayQuote(
            deposit,
            root.TryGetProperty("requestId", out var id) ? id.GetString() : null,
            Text(details.GetProperty("currencyOut"), "amountFormatted") ?? "?",
            Number(details.GetProperty("currencyIn"), "amountUsd"),
            Number(details.GetProperty("currencyOut"), "amountUsd"),
            root.TryGetProperty("fees", out var fees) && fees.TryGetProperty("app", out var app) ? Number(app, "amountUsd") : 0);
    }

    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : null;

    private static double Number(JsonElement e, string name) =>
        Text(e, name) is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    internal static string Trim(string s) => s.Length > 200 ? s[..200] : s;
}

/// <summary>SideShift (sideshift.ai) variable shifts from USDC on Base to LTC. Needs Otium's account secret.</summary>
public sealed class SideShiftLitecoin(HttpClient http, IOptions<SharingOptions> options) : ISwapToCoinApi
{
    public bool IsConfigured => options.Value is { SideShiftSecret.Length: > 0, SideShiftAffiliateId.Length: > 0 };

    public async Task<(string DepositAddress, decimal MinimumUsdc)> CreateAsync(string settleAddress, decimal usdcAmount, string refundAddress, string userIp, CancellationToken ct)
    {
        var o = options.Value;
        var body = new JsonObject
        {
            ["settleAddress"] = settleAddress,
            ["depositCoin"] = "USDC",
            ["depositNetwork"] = "base",
            ["settleCoin"] = "LTC",
            ["settleNetwork"] = "litecoin",
            ["affiliateId"] = o.SideShiftAffiliateId,
            // Otium's cut is taken once, by Relay; SideShift's own commission is set to nothing.
            ["commissionRate"] = "0",
            ["refundAddress"] = refundAddress,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.SideShiftApi), "shifts/variable"))
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-sideshift-secret", o.SideShiftSecret);
        request.Headers.Add("x-user-ip", userIp);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"SideShift refused the shift ({(int)response.StatusCode}): {RelayClient.Trim(json)}");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var min = root.TryGetProperty("depositMin", out var m) && decimal.TryParse(m.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 3m;
        return (root.GetProperty("depositAddress").GetString()!, min);
    }
}

/// <summary>ChangeNOW (changenow.io) exchanges from USDC on Base to XMR. Needs Otium's partner API key.</summary>
public sealed class ChangeNowMonero(HttpClient http, IOptions<SharingOptions> options) : ISwapToCoinApi
{
    /// <summary>ChangeNOW's minimum from USDC on Base to XMR (11.39 USDC in October 2026), with some room.</summary>
    public const decimal MinimumUsdc = 12m;

    public bool IsConfigured => options.Value is { ChangeNowApiKey.Length: > 0 };

    public async Task<(string DepositAddress, decimal MinimumUsdc)> CreateAsync(string settleAddress, decimal usdcAmount, string refundAddress, string userIp, CancellationToken ct)
    {
        var o = options.Value;
        var body = new JsonObject
        {
            ["fromCurrency"] = "usdc",
            ["fromNetwork"] = "base",
            ["toCurrency"] = "xmr",
            ["toNetwork"] = "xmr",
            ["fromAmount"] = usdcAmount.ToString("0.######", CultureInfo.InvariantCulture),
            ["address"] = settleAddress,
            ["flow"] = "standard",
            ["refundAddress"] = refundAddress,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.ChangeNowApi), "exchange"))
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-changenow-api-key", o.ChangeNowApiKey);
        request.Headers.Add("x-forwarded-for", userIp);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ChangeNOW refused the exchange ({(int)response.StatusCode}): {RelayClient.Trim(json)}");
        using var doc = JsonDocument.Parse(json);
        return (doc.RootElement.GetProperty("payinAddress").GetString()!, MinimumUsdc);
    }
}

/// <summary>Decides, for one account's waiting sharing earnings, whether and where to pay them.</summary>
public sealed class SharingRouteService(
    IRelayApi relay, SideShiftLitecoin litecoin, ChangeNowMonero monero, IOptions<SharingOptions> options, IOptions<FeeOptions> fees,
    IOptions<ProxybaseOptions> proxybase, ILogger<SharingRouteService> logger)
{
    /// <summary>Otium's cut in basis points: the configured one, or the mining fee.</summary>
    public int FeeBps => options.Value.FeeBps ?? (int)Math.Round(fees.Value.FeePercent * 100);

    public async Task<SharingRoute> RouteAsync(Session session, SharingRouteRequest request, string userIp, CancellationToken ct)
    {
        if (!proxybase.Value.Enabled)
            return new SharingRoute(SharingRouteDecisions.Unsupported, Message: "Internet sharing is switched off on this Otium server for now.");
        if (request.AvailableMicrocredits < 1_000_000)
            return new SharingRoute(SharingRouteDecisions.Wait, Message: "Waiting until $1 has built up.");
        if (!CryptoAddresses.TryValidate(AddressFormat.Evm, request.RefundAddress, out _))
            throw new OtiumException(StatusCodes.Status400BadRequest, "The sharing client's wallet address is missing.");

        // Always the account's own registered payout address: the app can't send earnings anywhere else.
        var stored = session.Account.OwnWallet;
        var target = PayoutTarget.ParseOrNull(stored) ?? PayoutTarget.ParseOrNull($"XMR:{stored}");
        if (target is null)
            return new SharingRoute(SharingRouteDecisions.Unsupported, Message: "Add your payout address in Settings first.");

        var refund = request.RefundAddress!.Trim();
        var o = options.Value;
        var feeRecipient = CryptoAddresses.TryValidate(AddressFormat.Evm, o.FeeRecipient, out _) ? o.FeeRecipient!.Trim().ToLowerInvariant() : null;
        // Otium's share stays at Proxybase for the app to pay Otium directly; only the user's part is converted, fee-free.
        var fee = feeRecipient is null ? 0 : (long)Math.Round(request.AvailableMicrocredits * Math.Clamp(FeeBps, 0, 1000) / 10_000d);
        var amount = request.AvailableMicrocredits - fee;
        var usd = amount / 1_000_000d;
        try
        {
            var route = await ConvertAsync(target, amount, usd, refund, userIp, ct);
            return route.Decision == SharingRouteDecisions.Pay && fee > 0 ? route with { FeeAddress = feeRecipient, FeeMicrocredits = fee } : route;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or KeyNotFoundException
                                    && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Sharing payout route failed for {Coin}", target.Coin.Symbol);
            return new SharingRoute(SharingRouteDecisions.Wait, Message: "The conversion service didn't answer. Otium tries again in a few minutes.");
        }
    }

    /// <summary>The one-time conversion of <paramref name="amount"/> to the user's coin and address, with no Otium fee in it.</summary>
    private async Task<SharingRoute> ConvertAsync(PayoutTarget target, long amount, double usd, string refund, string userIp, CancellationToken ct)
    {
        if (RelayClient.DirectFor(target.Coin) is { } destination)
        {
            var quote = await relay.QuoteDepositAddressAsync(destination, target.Address, amount, refund, null, 0, ct);
            return Decide(quote, usd, $"{quote.OutAmount} {destination.Label}", amount);
        }

        ISwapToCoinApi? hop = target.Coin == PayoutCoins.Litecoin && litecoin.IsConfigured ? litecoin
            : target.Coin == PayoutCoins.Monero && monero.IsConfigured ? monero
            : null;
        if (hop is null)
            return new SharingRoute(SharingRouteDecisions.Unsupported,
                Message: $"Internet sharing can't pay {target.Coin.Ticker} yet. Choose another payout coin in Settings to get its earnings.");

        // First what Relay would deliver as USDC on Base, then a conversion of that to the user's coin, then the Relay
        // address that delivers into it.
        var estimate = await relay.QuoteDepositAddressAsync(RelayClient.BaseUsdc, refund, amount, refund, null, 0, ct);
        var usdcOut = (decimal)estimate.OutUsd;
        var minimum = hop == monero ? ChangeNowMonero.MinimumUsdc : 3.5m;
        if (usdcOut < minimum)
            return new SharingRoute(SharingRouteDecisions.Wait,
                Message: FormattableString.Invariant($"Waiting until about ${minimum + 1:0} has built up: converting to {target.Coin.Ticker} needs at least that."));
        var (swapDeposit, _) = await hop.CreateAsync(target.Address, Math.Round(usdcOut, 2), refund, userIp, ct);
        var quote2 = await relay.QuoteDepositAddressAsync(RelayClient.BaseUsdc, swapDeposit, amount, refund, null, 0, ct);
        return Decide(quote2, usd, $"about {usdcOut:0.00} USDC worth of {target.Coin.Ticker}", amount);
    }

    /// <summary>Pays when the conversion's own network costs are small for this amount, or once enough has waited.</summary>
    internal SharingRoute Decide(RelayQuote quote, double usd, string expected, long amountMicro)
    {
        var o = options.Value;
        var inUsd = quote.InUsd > 0 ? quote.InUsd : usd;
        var networkCost = Math.Max(0, inUsd - quote.OutUsd - quote.AppFeeUsd);
        var networkPercent = inUsd > 0 ? networkCost / inUsd * 100 : 100;
        var totalPercent = inUsd > 0 ? Math.Max(0, inUsd - quote.OutUsd) / inUsd * 100 : 100;
        if (networkPercent > o.MaxNetworkCostPercent && usd < o.PayAnywayUsd)
        {
            // The network part is mostly fixed: enough waiting makes it the configured share.
            var needed = Math.Min(o.PayAnywayUsd, networkCost / (o.MaxNetworkCostPercent / 100));
            return new SharingRoute(SharingRouteDecisions.Wait, CostPercent: totalPercent,
                Message: FormattableString.Invariant($"Waiting until about ${Math.Ceiling(needed):0} has built up, so the network fee (${networkCost:0.00} a payout) stays small."));
        }
        return new SharingRoute(SharingRouteDecisions.Pay, quote.DepositAddress, amountMicro, expected, totalPercent, RequestId: quote.RequestId);
    }
}

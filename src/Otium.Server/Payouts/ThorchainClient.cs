using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server.Payouts;

/// <summary>A THORChain swap quote: send <c>Sats</c> to <see cref="InboundAddress"/> with <see cref="Memo"/> before <see cref="Expiry"/>.</summary>
/// <param name="ExpectedAmountOut">In THORChain's 1e8 units of the target asset, after all fees.</param>
/// <param name="RecommendedMinSats">Below this the swap may be refunded; THORChain's own recommendation.</param>
public sealed record ThorchainQuote(
    string InboundAddress,
    string Memo,
    long ExpectedAmountOut,
    long RecommendedMinSats,
    DateTimeOffset Expiry,
    long TotalFee,
    int TotalSwapSeconds);

/// <param name="Done">THORChain has signed the outbound transaction.</param>
/// <param name="Refunded">The swap failed and THORChain sent the BTC back to the sending wallet.</param>
/// <param name="OutTxId">The transaction that delivered the coins to the user (or the refund).</param>
public sealed record ThorchainSwapStatus(bool Done, bool Refunded, string? OutTxId);

/// <summary>The parts of THORNode's public API that Otium uses. No key or account is needed.</summary>
public interface IThorchainApi
{
    /// <summary>BTC price in USD from THORChain's BTC pool.</summary>
    Task<decimal> GetBtcPriceUsdAsync(CancellationToken ct);

    /// <summary>Why swaps out to <paramref name="chain"/> (e.g. "SOL") are paused right now, or null when they run.</summary>
    Task<string?> GetChainPauseReasonAsync(string chain, CancellationToken ct);

    /// <summary>A quote for swapping <paramref name="sats"/> of BTC to <paramref name="toAsset"/> at <paramref name="destination"/>.</summary>
    /// <exception cref="ThorchainException">THORChain refused (halted, too small, bad address, ...).</exception>
    Task<ThorchainQuote> QuoteFromBtcAsync(string toAsset, long sats, string destination, CancellationToken ct);

    Task<ThorchainSwapStatus> GetSwapStatusAsync(string inboundTxId, CancellationToken ct);
}

public sealed class ThorchainClient(HttpClient http, IOptions<ThorchainOptions> options) : IThorchainApi
{
    private readonly Uri _base = new(options.Value.ApiBase.TrimEnd('/') + "/");

    public async Task<decimal> GetBtcPriceUsdAsync(CancellationToken ct)
    {
        using var doc = await GetAsync("thorchain/pool/BTC.BTC", ct);
        // asset_tor_price is the pool's USD price in 1e8 units.
        var tor = decimal.Parse(doc.RootElement.GetProperty("asset_tor_price").GetString()!, CultureInfo.InvariantCulture);
        return tor / 100_000_000m;
    }

    public async Task<string?> GetChainPauseReasonAsync(string chain, CancellationToken ct)
    {
        using var doc = await GetAsync("thorchain/inbound_addresses", ct);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!string.Equals(entry.GetProperty("chain").GetString(), chain, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Flag(entry, "halted") || Flag(entry, "chain_trading_paused") || Flag(entry, "global_trading_paused"))
                return "paused by the swap network right now";
            return null;
        }
        return "not supported by the swap network";
    }

    public async Task<ThorchainQuote> QuoteFromBtcAsync(string toAsset, long sats, string destination, CancellationToken ct)
    {
        // No refund_address: it makes the memo too long for Bitcoin's 80-byte OP_RETURN, and refunds go back to the
        // sending address anyway, which is Otium's own wallet.
        var path = $"thorchain/quote/swap?from_asset=BTC.BTC&to_asset={Uri.EscapeDataString(toAsset)}" +
                   $"&amount={sats.ToString(CultureInfo.InvariantCulture)}&destination={Uri.EscapeDataString(destination)}";
        using var doc = await GetAsync(path, ct);
        var root = doc.RootElement;
        return new ThorchainQuote(
            root.GetProperty("inbound_address").GetString()!,
            root.GetProperty("memo").GetString()!,
            Long(root, "expected_amount_out"),
            Long(root, "recommended_min_amount_in"),
            DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("expiry").GetInt64()),
            root.TryGetProperty("fees", out var fees) ? Long(fees, "total") : 0,
            root.TryGetProperty("total_swap_seconds", out var seconds) ? seconds.GetInt32() : 0);
    }

    public async Task<ThorchainSwapStatus> GetSwapStatusAsync(string inboundTxId, CancellationToken ct)
    {
        using var doc = await GetAsync($"thorchain/tx/status/{Uri.EscapeDataString(inboundTxId)}", ct);
        var root = doc.RootElement;
        var signed = root.TryGetProperty("stages", out var stages)
                     && stages.TryGetProperty("outbound_signed", out var outbound)
                     && outbound.TryGetProperty("completed", out var completed) && completed.GetBoolean();

        string? outTx = null;
        var refunded = false;
        if (root.TryGetProperty("out_txs", out var outs) && outs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tx in outs.EnumerateArray())
            {
                var chain = tx.TryGetProperty("chain", out var c) ? c.GetString() : null;
                var id = tx.TryGetProperty("id", out var i) ? i.GetString() : null;
                var memo = tx.TryGetProperty("memo", out var m) ? m.GetString() ?? "" : "";
                if (chain == "THOR" || string.IsNullOrEmpty(id) || id.All(ch => ch == '0'))
                    continue; // internal RUNE legs and not-yet-broadcast outbounds
                refunded |= memo.StartsWith("REFUND", StringComparison.OrdinalIgnoreCase);
                outTx = id;
            }
        }
        return new ThorchainSwapStatus(signed && outTx is not null, refunded, outTx);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri(_base, path), ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // THORNode explains refusals in a JSON "message"; anything else is a transient network problem.
            string? message = null;
            try
            {
                using var error = JsonDocument.Parse(text);
                message = error.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch (JsonException)
            {
            }
            if (message is not null && (int)response.StatusCode is >= 400 and < 500)
                throw new ThorchainException(message);
            throw new HttpRequestException($"THORNode returned {(int)response.StatusCode} for {path}");
        }
        return JsonDocument.Parse(text);
    }

    private static bool Flag(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static long Long(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetInt64()
            : 0;
}

/// <summary>THORChain refused a request for a reason that retrying won't fix soon (halted asset, too small, bad address).</summary>
public sealed class ThorchainException(string message) : Exception(message);

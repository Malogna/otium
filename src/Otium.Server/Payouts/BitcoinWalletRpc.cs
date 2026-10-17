using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server.Payouts;

/// <summary>The operator's Bitcoin treasury wallet.</summary>
public interface IBitcoinWallet
{
    bool IsConfigured { get; }

    /// <summary>Confirmed, spendable balance in BTC.</summary>
    Task<decimal> GetBalanceAsync(CancellationToken ct);

    /// <summary>One transaction paying every address; Otium pays the fee. Returns the transaction id.</summary>
    Task<string> SendManyAsync(IReadOnlyDictionary<string, decimal> amountsBtc, CancellationToken ct);

    /// <summary>
    /// Pays <paramref name="amountBtc"/> to <paramref name="address"/> with <paramref name="memo"/> in an OP_RETURN output,
    /// laid out as THORChain requires: output 0 to the vault, output 1 change, output 2 the memo. Returns the transaction id.
    /// </summary>
    Task<string> SendWithMemoAsync(string address, decimal amountBtc, string memo, CancellationToken ct);
}

/// <summary>
/// Client for bitcoind's JSON-RPC wallet (the operator's hot wallet for payouts). Run bitcoind (pruned is fine) on
/// localhost or a private network with rpcauth, and keep only a few weeks of payouts in this wallet.
/// </summary>
public sealed class BitcoinWalletRpc : IBitcoinWallet, IDisposable
{
    private readonly BitcoinWalletOptions _options;
    private readonly HttpClient? _http;

    public BitcoinWalletRpc(IOptions<BitcoinWalletOptions> options) : this(options.Value, null)
    {
    }

    internal BitcoinWalletRpc(BitcoinWalletOptions options, HttpMessageHandler? handler)
    {
        _options = options;
        if (!_options.IsConfigured)
            return;

        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromMinutes(2);
        if (_options.Username.Length > 0)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}")));
        }
    }

    public bool IsConfigured => _http is not null;

    public async Task<decimal> GetBalanceAsync(CancellationToken ct)
    {
        var result = await CallAsync("getbalances", [], ct);
        return result.GetProperty("mine").GetProperty("trusted").GetDecimal();
    }

    public async Task<string> SendManyAsync(IReadOnlyDictionary<string, decimal> amountsBtc, CancellationToken ct)
    {
        var amounts = amountsBtc.ToDictionary(a => a.Key, a => Round(a.Value));
        // sendmany "" {amounts} minconf comment subtractfeefrom replaceable conf_target
        var result = await CallAsync("sendmany", ["", amounts, 1, "otium payouts", Array.Empty<string>(), true, _options.ConfTarget], ct);
        return result.GetString() ?? throw new BitcoinWalletException("bitcoind returned no transaction id");
    }

    public async Task<string> SendWithMemoAsync(string address, decimal amountBtc, string memo, CancellationToken ct)
    {
        var memoBytes = Encoding.UTF8.GetBytes(memo);
        if (memoBytes.Length > 80)
            throw new BitcoinWalletException("memo longer than 80 bytes");

        var outputs = new object[]
        {
            new Dictionary<string, decimal> { [address] = Round(amountBtc) },
            new Dictionary<string, string> { ["data"] = Convert.ToHexStringLower(memoBytes) },
        };
        var raw = (await CallAsync("createrawtransaction", [Array.Empty<object>(), outputs], ct)).GetString()!;
        var funded = await CallAsync("fundrawtransaction", [raw, new Dictionary<string, object>
        {
            ["changePosition"] = 1,     // THORChain: vault first, change second, memo last
            ["change_type"] = "bech32", // no exotic scripts
            ["conf_target"] = _options.ConfTarget,
            ["replaceable"] = false,
        }], ct);
        var signed = await CallAsync("signrawtransactionwithwallet", [funded.GetProperty("hex").GetString()!], ct);
        if (!signed.GetProperty("complete").GetBoolean())
            throw new BitcoinWalletException("the wallet couldn't sign the swap transaction");
        var txid = await CallAsync("sendrawtransaction", [signed.GetProperty("hex").GetString()!], ct);
        return txid.GetString() ?? throw new BitcoinWalletException("bitcoind returned no transaction id");
    }

    private static decimal Round(decimal btc) => decimal.Round(btc, 8, MidpointRounding.ToZero);

    private async Task<JsonElement> CallAsync(string method, object[] parameters, CancellationToken ct)
    {
        if (_http is null)
            throw new InvalidOperationException("The Bitcoin wallet RPC isn't configured.");

        var url = _options.Wallet.Length > 0
            ? $"{_options.RpcUrl.TrimEnd('/')}/wallet/{Uri.EscapeDataString(_options.Wallet)}"
            : _options.RpcUrl;
        using var response = await _http.PostAsJsonAsync(url, new { jsonrpc = "1.0", id = "otium", method, @params = parameters }, ct);
        // bitcoind answers RPC errors with HTTP 500 and a JSON body, so read the body before checking the status.
        var text = await response.Content.ReadAsStringAsync(ct);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            response.EnsureSuccessStatusCode();
            throw new BitcoinWalletException($"unexpected response from bitcoind ({(int)response.StatusCode})");
        }
        using (doc)
        {
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                throw new BitcoinWalletException(message ?? "wallet error");
            }
            response.EnsureSuccessStatusCode();
            return doc.RootElement.GetProperty("result").Clone();
        }
    }

    public void Dispose() => _http?.Dispose();
}

/// <summary>A wallet-side problem (usually not enough confirmed balance); the cash-out is retried later.</summary>
public sealed class BitcoinWalletException(string message) : Exception(message);

internal static class Btc
{
    public const decimal SatsPerBtc = 100_000_000m;

    /// <summary>USD (in micro-dollars) to BTC at <paramref name="priceUsd"/>, rounded down to whole satoshis.</summary>
    public static decimal FromMicros(long micros, decimal priceUsd) =>
        decimal.Floor(micros / 1_000_000m / priceUsd * SatsPerBtc) / SatsPerBtc;

    public static long Sats(decimal btc) => (long)decimal.Floor(btc * SatsPerBtc);

    public static string Format(decimal btc) => btc.ToString("0.00000000", CultureInfo.InvariantCulture);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Otium.Server.Payouts;

public sealed record MoneroTransferResult(string TxHash, string? TxKey, long AmountAtomic, long FeeAtomic);

public interface IMoneroWallet
{
    bool IsConfigured { get; }

    /// <summary>Creates a new receive (sub)address in account 0. Used to spread users across pool addresses.</summary>
    Task<string> CreateAddressAsync(string label, CancellationToken ct);

    /// <summary>Sends one transaction paying every destination (Monero allows up to 16 outputs per transaction).</summary>
    Task<MoneroTransferResult> TransferAsync(IReadOnlyList<(string Address, long AmountAtomic)> destinations, CancellationToken ct);
}

/// <summary>
/// Client for monero-wallet-rpc (the operator's hot wallet). Run it on localhost or a private network with
/// --rpc-login; .NET's handler answers its HTTP digest challenge automatically.
/// </summary>
public sealed class MoneroWalletRpc : IMoneroWallet, IDisposable
{
    private readonly MoneroWalletOptions _options;
    private readonly HttpClient? _http;

    public MoneroWalletRpc(IOptions<MoneroWalletOptions> options)
    {
        _options = options.Value;
        if (!_options.IsConfigured)
            return;

        var handler = new HttpClientHandler();
        if (_options.Username.Length > 0)
            handler.Credentials = new NetworkCredential(_options.Username, _options.Password);
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
    }

    public bool IsConfigured => _http is not null;

    public async Task<string> CreateAddressAsync(string label, CancellationToken ct)
    {
        var result = await CallAsync("create_address", new { account_index = 0, label }, ct);
        return result.GetProperty("address").GetString() ?? throw new InvalidOperationException("Wallet returned no address.");
    }

    public async Task<MoneroTransferResult> TransferAsync(IReadOnlyList<(string Address, long AmountAtomic)> destinations, CancellationToken ct)
    {
        var result = await CallAsync("transfer", new
        {
            destinations = destinations.Select(d => new { amount = d.AmountAtomic, address = d.Address }).ToArray(),
            account_index = 0,
            priority = 0,
            get_tx_key = true,
        }, ct);

        return new MoneroTransferResult(
            result.GetProperty("tx_hash").GetString() ?? "",
            result.TryGetProperty("tx_key", out var key) ? key.GetString() : null,
            result.TryGetProperty("amount", out var amount) ? amount.GetInt64() : destinations.Sum(d => d.AmountAtomic),
            result.TryGetProperty("fee", out var fee) ? fee.GetInt64() : 0);
    }

    private async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken ct)
    {
        if (_http is null)
            throw new InvalidOperationException("The Monero wallet RPC isn't configured.");

        using var response = await _http.PostAsJsonAsync(_options.RpcUrl, new RpcRequest(method, parameters), ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new MoneroWalletException(error.TryGetProperty("message", out var m) ? m.GetString() ?? "wallet error" : "wallet error");
        return doc.RootElement.GetProperty("result").Clone();
    }

    public void Dispose() => _http?.Dispose();

    private sealed record RpcRequest(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] object Params)
    {
        [JsonPropertyName("jsonrpc")] public string JsonRpc => "2.0";

        [JsonPropertyName("id")] public string Id => "0";
    }
}

public sealed class MoneroWalletException(string message) : Exception(message);

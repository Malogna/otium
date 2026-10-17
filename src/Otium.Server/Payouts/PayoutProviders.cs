using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <param name="Status"><see cref="CashoutStatuses.Sending"/>, <see cref="CashoutStatuses.Paid"/> or <see cref="CashoutStatuses.Failed"/>;
/// null means "unchanged, try again later".</param>
public sealed record PayoutOutcome(string CashoutId, string? Status, string? ProviderReference = null, string? Note = null);

/// <summary>Sends money for one cash-out method.</summary>
public interface IPayoutProvider
{
    string Method { get; }

    bool IsConfigured { get; }

    /// <summary>How many queued cash-outs to hand over in one call.</summary>
    int BatchSize { get; }

    Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct);

    /// <summary>Follows up on a cash-out already handed to the provider.</summary>
    Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct);

    /// <summary>
    /// Why this method can't take new cash-outs right now (shown to users), or null when it can. A provider that isn't
    /// set up still takes cash-outs: they wait in the review queue for the operator to pay by hand.
    /// </summary>
    Task<string?> GetUnavailableReasonAsync(CancellationToken ct) => Task.FromResult<string?>(null);
}

/// <summary>
/// PayPal Payouts API: one batch per cash-out, using the cash-out id as sender_batch_id so a retry can never pay twice.
/// Needs a PayPal business account approved for Payouts (crypto-related businesses need PayPal's pre-approval).
/// </summary>
public sealed class PayPalPayoutProvider(HttpClient http, IOptions<PayPalOptions> options, ILogger<PayPalPayoutProvider> logger) : IPayoutProvider
{
    private readonly PayPalOptions _options = options.Value;
    private string? _token;
    private DateTimeOffset _tokenExpires;

    public string Method => CashoutMethods.PayPal;

    public bool IsConfigured => _options.IsConfigured;

    public int BatchSize => 1;

    /// <summary>Shown to the user while PayPal holds money for an email that has no PayPal account yet.</summary>
    public const string UnclaimedNote = CashoutStatuses.AwaitingClaimNote;

    private Uri BaseUri => new(_options.UseSandbox ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/");

    public async Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct)
    {
        var outcomes = new List<PayoutOutcome>();
        foreach (var cashout in batch)
        {
            var body = new
            {
                sender_batch_header = new
                {
                    sender_batch_id = cashout.Id,
                    email_subject = "You've received your Otium earnings",
                    email_message = "Thanks for sharing your computer's spare power with Otium.",
                },
                items = new[]
                {
                    new
                    {
                        recipient_type = "EMAIL",
                        amount = new { value = (cashout.Micros / (decimal)OtiumStore.MicrosPerDollar).ToString("0.00", CultureInfo.InvariantCulture), currency = "USD" },
                        receiver = cashout.Destination,
                        note = "Otium earnings",
                        sender_item_id = cashout.Id,
                    },
                },
            };

            using var request = await AuthorizedAsync(HttpMethod.Post, "v1/payments/payouts", ct);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(text);
                var batchId = doc.RootElement.GetProperty("batch_header").GetProperty("payout_batch_id").GetString();
                outcomes.Add(new PayoutOutcome(cashout.Id, CashoutStatuses.Sending, batchId));
            }
            else if ((int)response.StatusCode >= 500)
            {
                outcomes.Add(new PayoutOutcome(cashout.Id, null, Note: "PayPal temporarily unavailable")); // safe to retry: same sender_batch_id
            }
            else
            {
                logger.LogWarning("PayPal rejected payout {Id}: {Status} {Body}", cashout.Id, (int)response.StatusCode, text);
                outcomes.Add(new PayoutOutcome(cashout.Id, CashoutStatuses.Failed, Note: "PayPal couldn't send to that address"));
            }
        }
        return outcomes;
    }

    public async Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct)
    {
        if (sending.ProviderReference is null)
            return new PayoutOutcome(sending.Id, null);

        using var request = await AuthorizedAsync(HttpMethod.Get, $"v1/payments/payouts/{Uri.EscapeDataString(sending.ProviderReference)}", ct);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return new PayoutOutcome(sending.Id, null);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var item = doc.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0 ? items[0] : default;
        var status = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("transaction_status", out var s) ? s.GetString() : null;

        return MapItemStatus(sending.Id, status);
    }

    internal static PayoutOutcome MapItemStatus(string cashoutId, string? status) => status switch
    {
        "SUCCESS" => new PayoutOutcome(cashoutId, CashoutStatuses.Paid),
        // The recipient has 30 days to claim (e.g. by creating a PayPal account); after that PayPal returns the money.
        "UNCLAIMED" => new PayoutOutcome(cashoutId, CashoutStatuses.Sending, Note: UnclaimedNote),
        "FAILED" or "RETURNED" or "BLOCKED" or "REFUNDED" or "REVERSED" or "DENIED" =>
            new PayoutOutcome(cashoutId, CashoutStatuses.Failed, Note: $"PayPal: {status!.ToLowerInvariant()}"),
        _ => new PayoutOutcome(cashoutId, null), // PENDING, ONHOLD, unknown: check again later
    };

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string path, CancellationToken ct)
    {
        if (_token is null || DateTimeOffset.UtcNow >= _tokenExpires)
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, "v1/oauth2/token"))
            {
                Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]),
            };
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));
            using var tokenResponse = await http.SendAsync(tokenRequest, ct);
            tokenResponse.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
            _token = doc.RootElement.GetProperty("access_token").GetString();
            _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 60);
        }

        var request = new HttpRequestMessage(method, new Uri(BaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }
}

/// <summary>
/// Monero payouts from the operator's hot wallet through monero-wallet-rpc. Up to 15 cash-outs share one
/// transaction (Monero allows 16 outputs including change). Otium pays the network fee.
/// </summary>
public sealed class MoneroPayoutProvider(IMoneroWallet wallet, IMoneroPoolApi pool, ILogger<MoneroPayoutProvider> logger) : IPayoutProvider
{
    public const double AtomicPerXmr = 1e12;

    public string Method => CashoutMethods.Monero;

    public bool IsConfigured => wallet.IsConfigured;

    public int BatchSize => 15;

    public async Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct)
    {
        var price = (await pool.GetNetworkStatsAsync(ct)).PriceUsd;
        if (price <= 0)
            return batch.Select(c => new PayoutOutcome(c.Id, null, Note: "no XMR price")).ToList();

        var destinations = batch
            .Select(c => (c.Destination, (long)(c.Micros / (double)OtiumStore.MicrosPerDollar / price * AtomicPerXmr)))
            .ToList();
        try
        {
            var result = await wallet.TransferAsync(destinations, ct);
            var reference = result.TxKey is null ? result.TxHash : $"{result.TxHash} key:{result.TxKey}";
            return batch.Select(c => new PayoutOutcome(c.Id, CashoutStatuses.Paid, reference)).ToList();
        }
        catch (MoneroWalletException e)
        {
            // Usually not enough unlocked balance yet; leave them queued and try again later.
            logger.LogWarning("Monero payout batch failed: {Message}", e.Message);
            return batch.Select(c => new PayoutOutcome(c.Id, null, Note: e.Message)).ToList();
        }
    }

    /// <summary>Monero transfers are final when sent; nothing to follow up.</summary>
    public Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct) => Task.FromResult(new PayoutOutcome(sending.Id, null));
}

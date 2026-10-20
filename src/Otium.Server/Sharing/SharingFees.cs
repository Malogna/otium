using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Otium.Core.Wallets;
using Otium.Server.Data;

namespace Otium.Server.Sharing;

/// <summary>A token transfer on Tempo: to whom, how much (6 decimals: microcredits), and which transfer it was.</summary>
/// <param name="Transfer">Transaction hash and log index, unique per transfer.</param>
public sealed record TempoTransfer(string To, long AmountMicro, string Transfer, long Block);

/// <summary>Reads token transfers on the Tempo network.</summary>
public interface ITempoTransfers
{
    Task<long> GetBlockNumberAsync(CancellationToken ct);

    /// <summary>Transfers of <paramref name="token"/> to any of <paramref name="recipients"/> in the given blocks (inclusive).</summary>
    Task<IReadOnlyList<TempoTransfer>> GetTransfersAsync(string token, IReadOnlyList<string> recipients, long fromBlock, long toBlock, CancellationToken ct);
}

/// <summary>
/// Tempo's JSON-RPC (Ethereum's, chain 4217): token transfers are the standard ERC-20 Transfer event, which TIP-20
/// tokens emit for every movement, memo transfers included. Read with eth_getLogs filtered to the token and recipients.
/// </summary>
public sealed class TempoRpcClient(HttpClient http, IOptions<SharingOptions> options) : ITempoTransfers
{
    /// <summary>keccak256("Transfer(address,address,uint256)").</summary>
    public const string TransferTopic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    public async Task<long> GetBlockNumberAsync(CancellationToken ct)
    {
        using var result = await CallAsync("eth_blockNumber", [], ct);
        return Hex(result.RootElement.GetProperty("result").GetString());
    }

    public async Task<IReadOnlyList<TempoTransfer>> GetTransfersAsync(string token, IReadOnlyList<string> recipients, long fromBlock, long toBlock,
        CancellationToken ct)
    {
        var to = new JsonArray(recipients.Select(r => (JsonNode)("0x" + new string('0', 24) + r.Trim().ToLowerInvariant()[2..])).ToArray());
        var filter = new JsonObject
        {
            ["fromBlock"] = "0x" + fromBlock.ToString("x", CultureInfo.InvariantCulture),
            ["toBlock"] = "0x" + toBlock.ToString("x", CultureInfo.InvariantCulture),
            ["address"] = token,
            ["topics"] = new JsonArray(TransferTopic, null, to),
        };
        using var result = await CallAsync("eth_getLogs", [filter], ct);
        return ParseLogs(result.RootElement.GetProperty("result"));
    }

    internal static IReadOnlyList<TempoTransfer> ParseLogs(JsonElement logs)
    {
        var list = new List<TempoTransfer>();
        foreach (var log in logs.EnumerateArray())
        {
            var topics = log.GetProperty("topics");
            if (topics.GetArrayLength() < 3 || !string.Equals(topics[0].GetString(), TransferTopic, StringComparison.OrdinalIgnoreCase))
                continue;
            var amount = BigInteger.Parse("0" + (log.GetProperty("data").GetString() ?? "0x")[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (amount > long.MaxValue)
                continue;
            var to = "0x" + topics[2].GetString()![^40..].ToLowerInvariant();
            list.Add(new TempoTransfer(to, (long)amount,
                $"{log.GetProperty("transactionHash").GetString()}:{Hex(log.GetProperty("logIndex").GetString())}", Hex(log.GetProperty("blockNumber").GetString())));
        }
        return list;
    }

    private async Task<JsonDocument> CallAsync(string method, JsonArray parameters, CancellationToken ct)
    {
        var body = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters };
        using var response = await http.PostAsync(options.Value.TempoRpcUrl,
            new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Tempo's RPC refused {method} ({(int)response.StatusCode}): {RelayClient.Trim(json)}");
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            doc.Dispose();
            throw new InvalidOperationException($"Tempo's RPC refused {method}: {RelayClient.Trim(error.ToString())}");
        }
        return doc;
    }

    private static long Hex(string? value) => long.Parse((value ?? "0x0")[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <param name="Blocked">No new payout route: a fee payment is overdue, or too much fee is unpaid.</param>
/// <param name="PaymentMicrocredits">The exact amount the app should pay Otium now, or 0.</param>
public sealed record SharingFeeCheck(bool Blocked, long PaymentMicrocredits);

/// <summary>
/// Makes sure Otium's internet-sharing fee is paid without it ever going through the conversion. The app keeps Otium's
/// share of each payout back at Proxybase and pays it to Otium's address on Tempo from $1 (Proxybase's minimum payout).
/// A payment may come from Proxybase's own wallet, so the sender says nothing about whose it is; instead the server
/// names the exact amount for each payment, a few microcredits off the fee and different from every other payment still
/// on its way, and recognises it by that amount when it arrives. The server opens no new payout routes for an account
/// while one of its payments is overdue, or while it owes more than $1 beyond the payout at hand.
///
/// What an account owes counts only payouts that happened: a route's fee counts once a transfer reaches its deposit
/// address. When Tempo can't be read, nothing is blocked (users shouldn't lose payouts to Otium's own outage), so a
/// modified app gets payouts without paying while Tempo can't be read.
/// </summary>
public sealed class SharingFees(OtiumStore store, ITempoTransfers tempo, IOptions<SharingOptions> options, TimeProvider time, ILogger<SharingFees> logger)
{
    public const string ScanName = "tempo-sharing-fees";

    /// <summary>Proxybase pays out from $1, so no fee payment is smaller.</summary>
    public const long MinimumPaymentMicrocredits = 1_000_000;

    /// <summary>The most a payment's amount is moved from the fee to make it unique ($0.0005).</summary>
    public const int MaxOffsetMicrocredits = 500;

    /// <summary>Fees are checked only while Tempo was read this recently; otherwise payouts carry on unchecked.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(30);

    /// <summary>A payment that hasn't arrived after this long stops being expected (and its fee counts as unpaid again).</summary>
    public static readonly TimeSpan PaymentLapse = TimeSpan.FromDays(3);

    /// <summary>Routes are watched for a transfer into them for this long.</summary>
    public static readonly TimeSpan DepositMemory = TimeSpan.FromDays(3);

    /// <summary>On the very first read, how far back to start (about a few hours of Tempo blocks).</summary>
    public const long StartLookbackBlocks = 20_000;

    public const int MaxQueriesPerScan = 50;
    public const int RecipientsPerQuery = 100;

    private string? FeeRecipient =>
        CryptoAddresses.TryValidate(AddressFormat.Evm, options.Value.FeeRecipient, out _) ? options.Value.FeeRecipient!.Trim().ToLowerInvariant() : null;

    /// <summary>A payout route with <paramref name="feeMicro"/> of Otium's fee on it, owed once Proxybase pays into it.</summary>
    public void RecordRoute(string accountId, string deposit, long feeMicro, DateTimeOffset now) => store.AddSharingRoute(accountId, deposit, feeMicro, now);

    /// <summary>
    /// Before a new payout route: whether the account's fees are in order, and the exact fee payment the app should make
    /// now (for what it keeps back once that's $1, or for what it owes beyond that).
    /// </summary>
    /// <param name="reserved">What the app says it keeps back for Otium.</param>
    /// <param name="currentFee">Otium's fee on the payout being asked for.</param>
    public SharingFeeCheck Check(string accountId, long reserved, long currentFee, DateTimeOffset now)
    {
        var open = store.OpenSharingFeePayments(accountId).Where(p => now - p.CreatedAt < PaymentLapse).ToList();
        var payment = 0L;
        if (open.Count == 0 && reserved >= MinimumPaymentMicrocredits)
            payment = Ask(accountId, reserved, open, now);
        else if (open.Count > 0 && reserved >= open[0].AmountMicro - MaxOffsetMicrocredits)
            payment = open[0].AmountMicro; // the app still keeps it back: it hasn't been sent yet

        if (store.ChainScan(ScanName) is not { } scan || now - scan.ThroughAt > FreshFor)
            return new SharingFeeCheck(false, payment); // Tempo isn't being read: don't hold payouts up

        var overdue = open.Any(p => p.CreatedAt + TimeSpan.FromHours(options.Value.FeeGraceHours) < scan.ThroughAt);
        var (owed, paid) = store.SharingFeeTotals(accountId);
        var unpaid = owed - paid - open.Sum(p => p.AmountMicro);
        var behind = unpaid > MinimumPaymentMicrocredits + currentFee;
        if (behind && open.Count == 0)
            payment = Ask(accountId, unpaid, open, now); // owed beyond what the app keeps back (a modified app, or a lost file)
        return new SharingFeeCheck(overdue || behind, payment);
    }

    private long Ask(string accountId, long fee, List<OtiumStore.SharingFeePayment> open, DateTimeOffset now)
    {
        if (store.AddSharingFeePayment(accountId, fee, MinimumPaymentMicrocredits, MaxOffsetMicrocredits, now) is not { } payment)
            return 0;
        open.Add(payment);
        return payment.AmountMicro;
    }

    /// <summary>
    /// Reads Tempo from where the last read stopped: transfers to Otium's address settle the payments of exactly that
    /// amount, and a transfer into a route's deposit address makes that route's fee owed.
    /// </summary>
    public async Task ScanAsync(CancellationToken ct)
    {
        if (FeeRecipient is not { } recipient)
            return;
        var now = time.GetUtcNow();
        var head = await tempo.GetBlockNumberAsync(ct);
        var scan = store.ChainScan(ScanName);
        var from = scan is { } s ? s.Block + 1 : Math.Max(0, head - StartLookbackBlocks);
        var throughAt = scan?.ThroughAt ?? DateTimeOffset.UnixEpoch;
        if (from > head)
        {
            store.SetChainScan(ScanName, head, now);
            return;
        }

        var recipients = store.UnusedSharingDeposits(now - DepositMemory).Prepend(recipient).Distinct().ToList();
        var perQuery = Math.Max(1, options.Value.TempoBlocksPerQuery);
        for (var query = 0; query < MaxQueriesPerScan && from <= head; query++)
        {
            var to = Math.Min(head, from + perQuery - 1);
            foreach (var chunk in recipients.Chunk(RecipientsPerQuery))
            {
                foreach (var transfer in await tempo.GetTransfersAsync(RelayClient.TempoUsdc, chunk, from, to, ct))
                {
                    if (transfer.To != recipient)
                        store.MarkSharingDepositUsed(transfer.To, now);
                    else if (!store.MatchSharingFeePayment(transfer.AmountMicro, transfer.Transfer, now))
                        logger.LogInformation("A sharing fee transfer of {Amount} microcredits ({Transfer}) matched no expected payment", transfer.AmountMicro, transfer.Transfer);
                }
            }
            if (to == head)
                throughAt = now;
            store.SetChainScan(ScanName, to, throughAt);
            from = to + 1;
        }
    }
}

/// <summary>Reads Tempo for Otium's sharing fees every few minutes.</summary>
public sealed class SharingFeeWatcher(SharingFees fees, TimeProvider time, ILogger<SharingFeeWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await fees.ScanAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Tempo couldn't be read; sharing fees aren't checked (payouts carry on) until it can");
            }
            await Task.Delay(TimeSpan.FromMinutes(3), time, stoppingToken);
        }
    }
}

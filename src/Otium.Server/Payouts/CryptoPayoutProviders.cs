using Otium.Core.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <summary>
/// BTC cash-outs straight from the operator's Bitcoin treasury (bitcoind). Up to 25 cash-outs share one transaction,
/// so the network fee, which Otium pays, is a few cents each.
/// </summary>
public sealed class BitcoinPayoutProvider(IBitcoinWallet wallet, IThorchainApi prices, ILogger<BitcoinPayoutProvider> logger) : IPayoutProvider
{
    public string Method => CashoutMethods.Bitcoin;

    public bool IsConfigured => wallet.IsConfigured;

    public int BatchSize => 25;

    public async Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct)
    {
        decimal price;
        try
        {
            price = await prices.GetBtcPriceUsdAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or ThorchainException or System.Text.Json.JsonException)
        {
            return Retry(batch, "no BTC price right now");
        }
        if (price <= 0)
            return Retry(batch, "no BTC price right now");

        // One output per address: a second cash-out to the same address waits for the next batch.
        var sendable = batch.DistinctBy(c => c.Destination).ToList();
        var amounts = sendable.ToDictionary(c => c.Destination, c => Btc.FromMicros(c.Micros, price));
        try
        {
            var balance = await wallet.GetBalanceAsync(ct);
            if (balance < amounts.Values.Sum() * 1.01m)
            {
                logger.LogWarning("Bitcoin treasury too low: {Balance} BTC for {Needed} BTC of payouts", balance, amounts.Values.Sum());
                return Retry(batch, "waiting for the payout wallet to be topped up");
            }

            var txid = await wallet.SendManyAsync(amounts, ct);
            return batch.Select(c => sendable.Contains(c)
                ? new PayoutOutcome(c.Id, CashoutStatuses.Paid, txid)
                : new PayoutOutcome(c.Id, null)).ToList();
        }
        catch (BitcoinWalletException e)
        {
            logger.LogWarning("Bitcoin payout batch failed: {Message}", e.Message);
            return Retry(batch, e.Message);
        }
    }

    /// <summary>A broadcast Bitcoin payout is final from Otium's side; nothing to follow up.</summary>
    public Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct) => Task.FromResult(new PayoutOutcome(sending.Id, null));

    private static IReadOnlyList<PayoutOutcome> Retry(IReadOnlyList<CashoutRecord> batch, string note) =>
        batch.Select(c => new PayoutOutcome(c.Id, null, Note: note)).ToList();
}

/// <summary>
/// ETH, BNB and SOL cash-outs, swapped from the Bitcoin treasury through THORChain: Otium asks for a quote with the
/// user's address as the destination, sends BTC to THORChain's vault with the quote's memo, and THORChain delivers the
/// coin straight to the user, usually within 10–20 minutes. No account, key or KYC is involved. THORChain's swap and
/// network fees (about $0.25–0.50) come out of the amount. If a swap fails, THORChain sends the BTC back to Otium's
/// wallet and the cash-out is returned to the user's balance.
/// </summary>
public sealed class ThorchainPayoutProvider(
    string method,
    string toAsset,
    string chain,
    IBitcoinWallet wallet,
    IThorchainApi thorchain,
    TimeProvider time,
    ILogger<ThorchainPayoutProvider> logger) : IPayoutProvider
{
    /// <summary>The coins Otium pays through THORChain: method → (THORChain asset, chain).</summary>
    public static readonly IReadOnlyDictionary<string, (string Asset, string Chain)> Routes = new Dictionary<string, (string, string)>
    {
        [CashoutMethods.Ethereum] = ("ETH.ETH", "ETH"),
        [CashoutMethods.Bnb] = ("BSC.BNB", "BSC"),
        [CashoutMethods.Solana] = ("SOL.SOL", "SOL"),
    };

    public string Method => method;

    public bool IsConfigured => wallet.IsConfigured;

    /// <summary>Each swap is its own transaction with its own memo.</summary>
    public int BatchSize => 1;

    public async Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct)
    {
        var outcomes = new List<PayoutOutcome>();
        foreach (var cashout in batch)
            outcomes.Add(await SendOneAsync(cashout, ct));
        return outcomes;
    }

    private async Task<PayoutOutcome> SendOneAsync(CashoutRecord cashout, CancellationToken ct)
    {
        try
        {
            if (await thorchain.GetChainPauseReasonAsync(chain, ct) is { } paused)
                return new PayoutOutcome(cashout.Id, null, Note: $"{CashoutMethods.Name(method)} payouts are {paused}");

            var price = await thorchain.GetBtcPriceUsdAsync(ct);
            var btc = Btc.FromMicros(cashout.Micros, price);
            var quote = await thorchain.QuoteFromBtcAsync(toAsset, Btc.Sats(btc), cashout.Destination, ct);
            if (Btc.Sats(btc) < quote.RecommendedMinSats)
            {
                var minimumUsd = Math.Ceiling(quote.RecommendedMinSats / Btc.SatsPerBtc * price);
                return new PayoutOutcome(cashout.Id, CashoutStatuses.Failed,
                    Note: $"too small to swap to {CashoutMethods.Ticker(method)} right now (the minimum is about ${minimumUsd})");
            }
            if (quote.Expiry - time.GetUtcNow() < TimeSpan.FromMinutes(1))
                return new PayoutOutcome(cashout.Id, null, Note: "the swap quote expired; trying again");

            var balance = await wallet.GetBalanceAsync(ct);
            if (balance < btc * 1.02m)
            {
                logger.LogWarning("Bitcoin treasury too low for swap {Id}: {Balance} BTC, need {Needed}", cashout.Id, balance, btc);
                return new PayoutOutcome(cashout.Id, null, Note: "waiting for the payout wallet to be topped up");
            }

            var txid = await wallet.SendWithMemoAsync(quote.InboundAddress, btc, quote.Memo, ct);
            logger.LogInformation("Cash-out {Id}: sent {Btc} BTC to THORChain for {Asset} (tx {Tx})", cashout.Id, Btc.Format(btc), toAsset, txid);
            return new PayoutOutcome(cashout.Id, CashoutStatuses.Sending, txid);
        }
        catch (ThorchainException e) when (IsTemporary(e.Message))
        {
            return new PayoutOutcome(cashout.Id, null, Note: $"the swap network is paused: {e.Message}");
        }
        catch (ThorchainException e)
        {
            logger.LogWarning("THORChain refused cash-out {Id}: {Message}", cashout.Id, e.Message);
            return new PayoutOutcome(cashout.Id, CashoutStatuses.Failed, Note: $"the swap network refused it ({e.Message})");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            return new PayoutOutcome(cashout.Id, null, Note: "the swap network didn't answer");
        }
        catch (BitcoinWalletException e)
        {
            logger.LogWarning("Bitcoin wallet failed for swap {Id}: {Message}", cashout.Id, e.Message);
            return new PayoutOutcome(cashout.Id, null, Note: e.Message);
        }
    }

    public async Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct)
    {
        var inbound = sending.ProviderReference?.Split(' ', 2)[0];
        if (string.IsNullOrEmpty(inbound))
            return new PayoutOutcome(sending.Id, null);
        try
        {
            var status = await thorchain.GetSwapStatusAsync(inbound, ct);
            if (status.Refunded)
            {
                return new PayoutOutcome(sending.Id, CashoutStatuses.Failed,
                    Note: "the swap couldn't be completed and was sent back, so the amount is in your balance again");
            }
            if (status.Done)
                return new PayoutOutcome(sending.Id, CashoutStatuses.Paid, $"{status.OutTxId} in:{inbound}");
        }
        catch (ThorchainException)
        {
            // Not observed by THORChain yet (the BTC transaction is still unconfirmed).
        }
        return new PayoutOutcome(sending.Id, null);
    }

    public Task<string?> GetUnavailableReasonAsync(CancellationToken ct) => thorchain.GetChainPauseReasonAsync(chain, ct);

    private static bool IsTemporary(string message) =>
        message.Contains("halt", StringComparison.OrdinalIgnoreCase) || message.Contains("pause", StringComparison.OrdinalIgnoreCase);
}

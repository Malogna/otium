using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <summary>
/// Sends queued cash-outs through their provider and follows up on ones in flight. A cash-out is marked
/// "sending" before money moves, and only "sending" cash-outs with a provider reference are followed up,
/// so a crash can't cause the same cash-out to be paid twice. The owner gets an email receipt once it's paid.
/// </summary>
public sealed class CashoutProcessor(
    OtiumStore store,
    IEnumerable<IPayoutProvider> providers,
    CashoutReceipts receipts,
    TimeProvider time,
    ILogger<CashoutProcessor> logger,
    CashoutMethodCatalog? catalog = null)
{
    public const int MaxAttempts = 10;

    public async Task ProcessOnceAsync(CancellationToken ct)
    {
        if (catalog is not null)
            await catalog.RefreshAsync(ct);

        foreach (var provider in providers.Where(p => p.IsConfigured))
        {
            var queued = store.CashoutsWithStatus(CashoutStatuses.Queued).Where(c => c.Method == provider.Method).ToList();
            foreach (var batch in queued.Chunk(provider.BatchSize))
            {
                var now = time.GetUtcNow();
                foreach (var c in batch)
                    store.UpdateCashout(c.Id, CashoutStatuses.Sending, now, countAttempt: true);

                IReadOnlyList<PayoutOutcome> outcomes;
                try
                {
                    outcomes = await provider.SendAsync(batch, ct);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(e, "{Method} payout call failed; re-queuing", provider.Method);
                    outcomes = batch.Select(c => new PayoutOutcome(c.Id, null)).ToList();
                }

                foreach (var outcome in outcomes)
                    await ApplyAsync(batch.First(c => c.Id == outcome.CashoutId), outcome, requeueIfUnchanged: true, ct);
            }

            foreach (var sending in store.CashoutsWithStatus(CashoutStatuses.Sending).Where(c => c.Method == provider.Method && c.ProviderReference is not null))
            {
                try
                {
                    await ApplyAsync(sending, await provider.CheckAsync(sending, ct), requeueIfUnchanged: false, ct);
                }
                catch (HttpRequestException e)
                {
                    logger.LogWarning(e, "Couldn't check payout {Id}", sending.Id);
                }
            }
        }
    }

    private async Task ApplyAsync(CashoutRecord cashout, PayoutOutcome outcome, bool requeueIfUnchanged, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var fresh = store.FindCashout(cashout.Id) ?? cashout;
        switch (outcome.Status)
        {
            case CashoutStatuses.Failed:
                store.FailAndRefund(fresh, outcome.Note ?? "payout failed", now);
                logger.LogInformation("Cash-out {Id} failed and was refunded: {Note}", cashout.Id, outcome.Note);
                break;
            case CashoutStatuses.Paid:
                store.UpdateCashout(cashout.Id, outcome.Status, now, outcome.ProviderReference, outcome.Note);
                if (fresh.Status != CashoutStatuses.Paid)
                    await receipts.SendPaidAsync(store.FindCashout(cashout.Id)!, now, ct);
                break;
            case CashoutStatuses.Sending:
                store.UpdateCashout(cashout.Id, outcome.Status, now, outcome.ProviderReference, outcome.Note);
                break;
            case null when requeueIfUnchanged:
                if (fresh.Attempts >= MaxAttempts)
                    store.UpdateCashout(cashout.Id, CashoutStatuses.Review, now, note: $"automatic sending failed {fresh.Attempts} times: {outcome.Note}");
                else
                    store.UpdateCashout(cashout.Id, CashoutStatuses.Queued, now, note: null);
                break;
        }
    }
}

public sealed class CashoutProcessorService(CashoutProcessor processor, IOptions<CashoutOptions> options, ILogger<CashoutProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ProcessInterval);
        do
        {
            try
            {
                await processor.ProcessOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Cash-out processing failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

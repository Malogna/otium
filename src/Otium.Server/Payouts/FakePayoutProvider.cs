using Otium.Core.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <summary>
/// Dev mode only (DevMode:FakePayouts): accepts every cash-out, reports it "sending", then "paid" a few seconds
/// later, without moving any real money. Lets the whole cash-out flow be exercised locally.
/// </summary>
public sealed class FakePayoutProvider(string method, TimeProvider time) : IPayoutProvider
{
    public static readonly TimeSpan PaidAfter = TimeSpan.FromSeconds(15);

    public string Method => method;

    public bool IsConfigured => true;

    public int BatchSize => 50;

    public Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayoutOutcome>>(batch.Select(c => new PayoutOutcome(c.Id, CashoutStatuses.Sending, $"dev-{c.Id[3..11]}")).ToList());

    public Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct) =>
        Task.FromResult(time.GetUtcNow() - sending.UpdatedAt >= PaidAfter
            ? new PayoutOutcome(sending.Id, CashoutStatuses.Paid)
            : new PayoutOutcome(sending.Id, null));
}

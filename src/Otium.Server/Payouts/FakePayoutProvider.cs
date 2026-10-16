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
        Task.FromResult<IReadOnlyList<PayoutOutcome>>(batch.Select(c => new PayoutOutcome(c.Id, CashoutStatuses.Sending, FakeTransactionId(c))).ToList());

    public Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct) =>
        Task.FromResult(time.GetUtcNow() - sending.UpdatedAt >= PaidAfter
            ? new PayoutOutcome(sending.Id, CashoutStatuses.Paid)
            : new PayoutOutcome(sending.Id, null));

    /// <summary>Shaped like the real thing, so the app's transaction links and copy buttons can be tried in dev mode.</summary>
    private string FakeTransactionId(CashoutRecord c)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c.Id));
        return method == CashoutMethods.Solana
            ? string.Concat(Enumerable.Repeat(Convert.ToBase64String(hash).Replace("+", "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal).TrimEnd('='), 3))[..88]
            : Convert.ToHexStringLower(hash);
    }
}

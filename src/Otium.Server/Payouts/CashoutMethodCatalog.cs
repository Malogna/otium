using Microsoft.Extensions.Options;
using Otium.Core.Accounts;

namespace Otium.Server.Payouts;

/// <summary>
/// The payout methods the server offers and whether each works right now (for example, THORChain may pause Solana).
/// The cash-out processor refreshes it every minute; summaries and requests read the last snapshot.
/// </summary>
public sealed class CashoutMethodCatalog(IOptions<CashoutOptions> options, IEnumerable<IPayoutProvider> providers, ILogger<CashoutMethodCatalog> logger)
{
    private readonly CashoutOptions _options = options.Value;
    private readonly Dictionary<string, string?> _unavailable = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<CashoutMethodInfo> Current
    {
        get
        {
            lock (_lock)
            {
                return _options.OfferedMethods.Where(CashoutMethods.IsKnown).Select(method => new CashoutMethodInfo(
                    method,
                    CashoutMethods.Name(method),
                    CashoutMethods.Network(method),
                    _options.MinimumFor(method),
                    FeeNote(method),
                    _unavailable.GetValueOrDefault(method))).ToList();
            }
        }
    }

    public CashoutMethodInfo? Find(string method) => Current.FirstOrDefault(m => m.Method == method);

    public async Task RefreshAsync(CancellationToken ct)
    {
        foreach (var method in _options.OfferedMethods)
        {
            string? reason;
            if (providers.FirstOrDefault(p => p.Method == method) is not { } provider)
            {
                reason = null; // offered without a provider: requests wait in the review queue for a manual payout
            }
            else
            {
                try
                {
                    reason = await provider.GetUnavailableReasonAsync(ct);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or ThorchainException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning("Couldn't check whether {Method} payouts are available: {Message}", method, e.Message);
                    continue; // keep the last known state
                }
            }
            lock (_lock)
                _unavailable[method] = reason;
        }
    }

    private static string? FeeNote(string method) => method switch
    {
        CashoutMethods.Ethereum or CashoutMethods.Bnb or CashoutMethods.Solana =>
            "Swapped from Bitcoin through THORChain; its swap and network fees (about $0.25–0.50) come out of the amount.",
        _ => null,
    };
}

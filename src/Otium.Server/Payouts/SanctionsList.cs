using Microsoft.Extensions.Options;
using Otium.Core.Accounts;

namespace Otium.Server.Payouts;

/// <summary>
/// OFAC-sanctioned crypto addresses, refreshed from public lists. Cash-outs to a listed address are refused, and while
/// a method's lists haven't loaded yet its cash-outs wait for review instead of being sent unscreened.
/// </summary>
public sealed class SanctionsList
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HashSet<string>> _lists = [];

    /// <summary>Which lists cover each method's addresses (BNB Smart Chain uses Ethereum-format addresses).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> ListsFor = new Dictionary<string, string[]>
    {
        [CashoutMethods.Bitcoin] = ["btc"],
        [CashoutMethods.Ethereum] = ["evm"],
        [CashoutMethods.Bnb] = ["evm", "bsc"],
        [CashoutMethods.Solana] = ["sol"],
    };

    /// <summary>True when every list covering <paramref name="method"/> has loaded (methods without lists count as loaded).</summary>
    public bool IsLoaded(string method)
    {
        if (!ListsFor.TryGetValue(method, out var lists))
            return true;
        lock (_lock)
            return lists.All(_lists.ContainsKey);
    }

    public bool IsSanctioned(string method, string address)
    {
        if (!ListsFor.TryGetValue(method, out var lists))
            return false;
        var key = Normalize(address.Trim());
        lock (_lock)
            return lists.Any(l => _lists.TryGetValue(l, out var set) && set.Contains(key));
    }

    public void Load(string list, IEnumerable<string> addresses)
    {
        var set = addresses.Select(a => a.Trim()).Where(a => a.Length > 0 && !a.StartsWith('#')).Select(Normalize).ToHashSet(StringComparer.Ordinal);
        lock (_lock)
            _lists[list] = set;
    }

    public int Count
    {
        get
        {
            lock (_lock)
                return _lists.Values.Sum(s => s.Count);
        }
    }

    /// <summary>Case only matters for base58 (legacy Bitcoin, Solana); bech32 and 0x addresses compare case-insensitively.</summary>
    private static string Normalize(string address) =>
        address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || address.StartsWith("bc1", StringComparison.OrdinalIgnoreCase)
            ? address.ToLowerInvariant()
            : address;
}

/// <summary>Downloads the sanctions lists at startup and then periodically.</summary>
public sealed class SanctionsRefresher(SanctionsList list, HttpClient http, IOptions<SanctionsOptions> options, ILogger<SanctionsRefresher> logger) : BackgroundService
{
    public async Task RefreshOnceAsync(CancellationToken ct)
    {
        foreach (var (name, url) in options.Value.ListUrls)
        {
            try
            {
                var lines = (await http.GetStringAsync(url, ct)).Split('\n');
                list.Load(name, lines);
                logger.LogInformation("Loaded the {List} sanctions list ({Count} lines)", name, lines.Length);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Couldn't load the {List} sanctions list: {Message}", name, e.Message);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RefreshInterval);
        do
        {
            await RefreshOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

using Microsoft.Extensions.Options;
using Otium.Core.Wallets;
using Otium.Server.Data;
using Otium.Server.Payouts;

namespace Otium.Server.Accounts;

/// <summary>
/// Spreads balance accounts over several pool addresses. MoneroOcean tracks only about 50 simultaneously active
/// workers per address separately (the rest are lumped into "all_other_workers"), which would make per-user
/// crediting impossible. Every address is a subaddress of the operator's wallet, so all payouts still land in one place.
/// </summary>
public sealed class PoolWalletAllocator(
    OtiumStore store,
    IMoneroWallet wallet,
    IOptions<EarningsOptions> options,
    TimeProvider time,
    ILogger<PoolWalletAllocator> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _seeded;

    public async Task<string?> AssignAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            SeedFromConfig();
            var loads = store.PoolWalletLoads();
            var max = options.Value.MaxAccountsPerPoolWallet;

            if (loads.Count > 0 && loads[0].Accounts < max)
                return loads[0].Address;

            if (wallet.IsConfigured)
            {
                var address = await wallet.CreateAddressAsync($"otium-pool-{loads.Count + 1}", ct);
                store.AddPoolWallet(address, time.GetUtcNow());
                logger.LogInformation("Created pool subaddress #{Count}", loads.Count + 1);
                return address;
            }

            if (loads.Count == 0)
                return null; // balance mode unavailable: no pool wallet configured

            logger.LogWarning("All pool addresses carry {Max}+ accounts and no wallet RPC is configured to create more; crediting may degrade.", max);
            return loads[0].Address;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void SeedFromConfig()
    {
        if (_seeded)
            return;
        foreach (var address in options.Value.PoolWallets.Where(a => MoneroAddress.TryValidate(a, out _, out _)))
            store.AddPoolWallet(address.Trim(), time.GetUtcNow());
        _seeded = true;
    }
}

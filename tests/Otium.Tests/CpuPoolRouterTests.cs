using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Server;
using Otium.Server.Data;
using Otium.Server.Gpu;

namespace Otium.Tests;

public class CpuPoolRouterTests
{
    private static readonly string PoolWallet = TestAddresses.Generate();

    private static AccountRecord Account(string worker = "ot0123456789ab", string? ownWallet = null) =>
        new("acc", worker, ownWallet is null ? PoolWallet : null, ownWallet, "ABCDEFG", "a@example.com", null, null, null, false, null, null,
            DateTimeOffset.UnixEpoch);

    private static CpuPoolRouter Router(int percent, bool kryptexEnabled = true, string? kryptexAccount = "otium") =>
        new(Options.Create(new CpuMiningOptions { KryptexPercent = percent }),
            Options.Create(new KryptexOptions { Enabled = kryptexEnabled, Account = kryptexAccount }));

    [Fact]
    public void By_default_everyone_stays_on_moneroocean()
    {
        var route = Router(0).For(Account())!;

        Assert.Equal(new CpuPoolRoute(CpuPoolRoute.MoneroOcean, "gulf.moneroocean.stream", 20128, true, PoolWallet, "ot0123456789ab"), route);
    }

    [Fact]
    public void At_100_percent_everyone_mines_on_kryptex_under_the_operator_account()
    {
        var route = Router(100).For(Account())!;

        Assert.Equal(new CpuPoolRoute(CpuPoolRoute.Kryptex, "xmr.kryptex.network", 8029, true, "otium.ot0123456789ab", "x"), route);
    }

    [Theory]
    [InlineData(false, "otium")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    public void Without_a_kryptex_account_nobody_is_moved(bool enabled, string? account)
    {
        Assert.Equal(CpuPoolRoute.MoneroOcean, Router(100, enabled, account).For(Account())!.Kind);
    }

    [Fact]
    public void Own_wallet_accounts_get_no_route()
    {
        Assert.Null(Router(100).For(Account(ownWallet: TestAddresses.Generate())));
    }

    [Fact]
    public void A_canary_moves_about_that_share_of_accounts_and_always_the_same_ones()
    {
        var router = Router(25);
        var workers = Enumerable.Range(0, 2000).Select(i => $"ot{i:x12}").ToList();

        var moved = workers.Count(router.OnKryptex);

        Assert.InRange(moved, 2000 * 0.20, 2000 * 0.30);
        Assert.All(workers, w => Assert.Equal(router.OnKryptex(w), Router(25).OnKryptex(w)));
        // Raising the share only adds accounts; nobody who moved goes back.
        Assert.All(workers.Where(router.OnKryptex), w => Assert.True(Router(50).OnKryptex(w)));
    }
}

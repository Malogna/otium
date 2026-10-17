using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Fees;
using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Core.Switching;
using Otium.Providers.SrbMiner;
using Otium.Providers.XMRig;

namespace Otium.Tests;

/// <summary>Mining to the user's own address: unMineable for most coins, MoneroOcean for Monero.</summary>
public class UnmineableMiningTests
{
    private const string Usdt = "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    private const string OtiumUsdt = "USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ";
    private const string Bnb = "BNB:0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";

    private static JsonElement XmrigPool(string target, PayoutRole role = PayoutRole.User)
    {
        var json = XmrigConfig.Build(
            new ProviderRunOptions(new MiningTarget(target, role), "ot1234abcd", 50),
            PoolEndpoint.MoneroOcean, httpPort: 45678, accessToken: "secret", referralCode: "ztjq-jr7s");
        return JsonDocument.Parse(json).RootElement.GetProperty("pools")[0].Clone();
    }

    [Fact]
    public void Cpu_mines_on_unmineable_over_tls_with_the_referral_code()
    {
        var pool = XmrigPool(Usdt);
        Assert.Equal("rx.unmineable.com:443", pool.GetProperty("url").GetString());
        Assert.True(pool.GetProperty("tls").GetBoolean());
        Assert.Equal($"{Usdt}.ot1234abcd#ztjq-jr7s", pool.GetProperty("user").GetString());
        Assert.Equal("x", pool.GetProperty("pass").GetString());
    }

    [Fact]
    public void A_fee_slice_changes_only_the_login()
    {
        var pool = XmrigPool(OtiumUsdt, PayoutRole.App);
        Assert.Equal("rx.unmineable.com:443", pool.GetProperty("url").GetString());
        Assert.Equal($"{OtiumUsdt}.ot1234abcd#ztjq-jr7s", pool.GetProperty("user").GetString());
    }

    [Fact]
    public void Monero_still_mines_on_moneroocean_to_the_bare_address()
    {
        var wallet = TestAddresses.Generate();
        var pool = XmrigPool($"XMR:{wallet}");
        Assert.Equal("gulf.moneroocean.stream:20128", pool.GetProperty("url").GetString());
        Assert.Equal(wallet, pool.GetProperty("user").GetString());
        Assert.Equal("ot1234abcd", pool.GetProperty("pass").GetString());
    }

    [Fact]
    public void Without_a_referral_code_the_login_has_no_suffix()
    {
        Assert.Equal($"{Usdt}.w", PayoutRouting.UnmineableLogin(Usdt, "w", null));
        Assert.Equal($"{Usdt}.otium", PayoutRouting.UnmineableLogin(Usdt, "!!", ""));
    }

    [Fact]
    public void Slices_only_go_to_targets_on_the_users_pool()
    {
        var xmr = TestAddresses.Generate();
        Assert.Equal(PayoutPool.Unmineable, PayoutRouting.PoolFor(Bnb));
        Assert.Equal(PayoutPool.MoneroOcean, PayoutRouting.PoolFor(xmr));
        Assert.Null(PayoutRouting.PoolFor("not an address"));

        Assert.True(PayoutRouting.SamePool(Usdt, Bnb));
        Assert.True(PayoutRouting.SamePool(xmr, $"XMR:{xmr}"));
        Assert.False(PayoutRouting.SamePool(Usdt, xmr));

        // Otium's fee goes to its target on the user's pool, or stays with the user when the build has none there.
        string[] fees = [OtiumUsdt, $"XMR:{TestAddresses.Generate()}"];
        Assert.Equal(OtiumUsdt, PayoutRouting.FeeTargetFor(Bnb, fees));
        Assert.Equal(fees[1], PayoutRouting.FeeTargetFor(xmr, fees));
        Assert.Null(PayoutRouting.FeeTargetFor(xmr, [OtiumUsdt]));
    }

    [Fact]
    public void A_referrer_with_the_users_own_address_typed_differently_gets_no_slice()
    {
        var now = DateTimeOffset.UtcNow;
        var standing = new ReferralStanding("bnb:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", now.AddDays(-1), null);
        var schedule = new FeeSchedule(FeePolicy.Default, Bnb, OtiumUsdt, standing);
        Assert.False(schedule.HasActiveReferrer(now));
        Assert.Equal(PayoutRole.App, schedule.RoleAt(FeePolicy.Default.CycleLength - TimeSpan.FromSeconds(1), now));
    }

    [Fact]
    public void The_fee_slice_is_about_half_an_hour_a_day()
    {
        var slice = FeePolicy.Default.CycleLength * (FeePolicy.Default.FeePercent / 100);
        Assert.InRange(slice.TotalMinutes, 28, 30);
    }

    private static readonly GpuInfo Rtx3080 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 10);

    private static readonly GpuPolicy Policy = new(
        DateTimeOffset.UtcNow,
        [
            new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "unmineable-pearl", 1e-14, 0.75),
            new GpuAlgorithmPolicy("kawpow", GpuTiers.FreePowerOnly, GpuRoutes.MoneroOcean, 1e-9, 1),
            new GpuAlgorithmPolicy("quantus", GpuTiers.Primary, "kryptex-qtc", 1e-6, 1),
        ],
        [
            new GpuPoolRoute("unmineable-pearl", GpuRoutes.Unmineable, "pearlpow.unmineable.com", 443, null, Tls: true),
            new GpuPoolRoute(GpuRoutes.MoneroOcean, GpuRoutes.MoneroOcean, "gulf.moneroocean.stream", 10128, null),
            new GpuPoolRoute("kryptex-qtc", GpuRoutes.Kryptex, "qtc.kryptex.network", 7777, "krxOTIUM"),
        ]);

    private static SrbMinerGpuProvider Gpu(GpuPolicy? policy) => new(
        new HttpClient(), null!, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), $"otium-bench-{Guid.NewGuid():N}.json"),
        [Rtx3080], null!, () => 0.15, asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64),
        policy: () => policy, referralCode: "ztjq-jr7s");

    [Fact]
    public void Gpu_logs_in_to_unmineable_with_the_slices_target_and_tls()
    {
        var pearl = GpuAlgorithms.Find("pearlhash")!;
        var login = Gpu(Policy).LoginFor(pearl, new ProviderRunOptions(new MiningTarget(OtiumUsdt, PayoutRole.App), "ot1234abcdg", 100));

        Assert.Equal(new GpuPoolEndpoint("pearlpow.unmineable.com", 443, Tls: true), login.Pool);
        Assert.Equal($"{OtiumUsdt}.ot1234abcdg#ztjq-jr7s", login.Wallet);
        Assert.Equal("x", login.Password);

        var args = SrbMinerArgs.Build(pearl, login, 40001, [Rtx3080]);
        Assert.Equal("true", args[args.ToList().IndexOf("--tls") + 1]);
        Assert.Equal("pearlhash", args[args.ToList().IndexOf("--algorithm") + 1]);
    }

    [Fact]
    public void Monero_users_graphics_cards_mine_pearl_on_unmineable_paid_in_xmr()
    {
        var pearl = GpuAlgorithms.Find("pearlhash")!;
        var xmr = TestAddresses.Generate();
        var options = new ProviderRunOptions(new MiningTarget($"XMR:{xmr}", PayoutRole.User), "w", 100);

        var login = Gpu(Policy).LoginFor(pearl, options);
        Assert.Equal(new GpuPoolEndpoint("pearlpow.unmineable.com", 443, Tls: true), login.Pool);
        Assert.Equal($"XMR:{xmr}.w#ztjq-jr7s", login.Wallet);

        // Outside the US only: US users' graphics cards stay on MoneroOcean.
        Assert.Equal(PayoutPool.Unmineable, PayoutRouting.GpuPoolFor($"XMR:{xmr}", unitedStates: false));
        Assert.Equal(PayoutPool.MoneroOcean, PayoutRouting.GpuPoolFor($"XMR:{xmr}", unitedStates: true));
        Assert.Equal(PayoutPool.Unmineable, PayoutRouting.GpuPoolFor(Usdt, unitedStates: false));
        Assert.Null(PayoutRouting.GpuPoolFor("nonsense", unitedStates: false));

        // A bad target can't be logged in with, and without the server's policy there's no unMineable route or price.
        Assert.Throws<InvalidOperationException>(() => Gpu(Policy).LoginFor(pearl, options with { Target = new MiningTarget("nonsense", PayoutRole.User) }));
        Assert.Throws<InvalidOperationException>(() => Gpu(null).LoginFor(pearl, options with { Target = new MiningTarget(Usdt, PayoutRole.User) }));
    }

    [Fact]
    public void Each_payout_pool_keeps_only_its_own_routes()
    {
        var unmineable = Policy.OnlyRoutes(GpuRoutes.Unmineable);
        Assert.Equal(["pearlhash"], unmineable.Algorithms.Select(a => a.Algorithm));

        var moneroOcean = Policy.OnlyRoutes(GpuRoutes.MoneroOcean);
        Assert.Equal(["kawpow"], moneroOcean.Algorithms.Select(a => a.Algorithm));

        // Kryptex coins are mined to Otium's own account, so users paid by their pool never get them.
        Assert.DoesNotContain("quantus", Policy.WithoutOperatorPools().Algorithms.Select(a => a.Algorithm));
        Assert.Contains("pearlhash", Policy.WithoutOperatorPools().Algorithms.Select(a => a.Algorithm));
    }

    [Fact]
    public void The_new_unmineable_algorithms_use_srbminers_names()
    {
        // Xelis loses money at normal power prices (October 2026), so it only runs on free power, like FishHash.
        Assert.Equal(GpuTiers.FreePowerOnly, GpuAlgorithms.Find("xelishash")!.DefaultTier);
        Assert.Equal(GpuTiers.Primary, GpuAlgorithms.Find("pearlhash")!.DefaultTier);
        Assert.Equal("xelishashv3", GpuAlgorithms.Find("xelishash")!.MinerName);
        Assert.Equal(GpuRoutes.Unmineable, GpuAlgorithms.Find("fishhash")!.DefaultRoute);
        Assert.Equal(GpuRoutes.Unmineable, GpuAlgorithms.Find("pearlhash")!.DefaultRoute);
    }
}

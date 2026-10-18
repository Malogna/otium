using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Fees;
using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Core.Switching;
using Otium.Core.Wallets;
using Otium.Providers.SrbMiner;
using Otium.Server;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>Quantus mined to the user's own wallet on Kryptex, and the kill switch for Pearl's FP8 fork.</summary>
public sealed class QuantusAndPearlForkTests
{
    // Real Quantus addresses from Kryptex's public top-miners list (17 October 2026).
    private const string Quantus1 = "qzjq98egzWEkH44qJPvhULTxfQNmeiSWfeRfHKgD87YLceZg6";
    private const string Quantus2 = "qzoyybv3zbLQZueutk2w5QRSfWdQjKMESqc4BjRLpStFQH2oa";
    private const string Usdt = "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    private const string OtiumUsdt = "USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ";

    [Theory]
    [InlineData("", "786a02f742015903c6c6fd852552d272912f4740e15847618a86e217f71f5419d25e1031afee585313896444934eb04b903a685b1448b755d56f701afe9be2ce")]
    [InlineData("abc", "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923")]
    public void Blake2b_matches_the_reference(string input, string expected) =>
        Assert.Equal(expected, Convert.ToHexStringLower(Blake2b.Hash(System.Text.Encoding.ASCII.GetBytes(input))));

    [Fact]
    public void Blake2b_handles_whole_and_multiple_blocks_and_short_outputs()
    {
        // Python's hashlib.blake2b as the reference.
        Assert.Equal("2319e3789c47e2daa5fe807f61bec2a1a6537fa03f19ff32e87eecbfd64b7e0e8ccff439ac333b040f19b0c4ddd11a61e24ac1fe0f10a039806c5dcc0da3d115",
            Convert.ToHexStringLower(Blake2b.Hash(Enumerable.Range(0, 128).Select(i => (byte)i).ToArray())));
        Assert.Equal("c59ab1095ca4579525338b6b74689ff234bc3fe9765fe26dfb04ddceaee0ab84dfd8967594cb261fcd88687f4454d80f718116c1b3c32f9f7e169357468cbe67",
            Convert.ToHexStringLower(Blake2b.Hash([.. Enumerable.Range(0, 256).Select(i => (byte)i), .. Enumerable.Range(0, 256).Select(i => (byte)i)])));
        Assert.Equal("bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319",
            Convert.ToHexStringLower(Blake2b.Hash("abc"u8, outputLength: 32)));
    }

    [Theory]
    [InlineData(Quantus1, true)]
    [InlineData(Quantus2, true)]
    [InlineData("qzjq98egzWEkH44qJPvhULTxfQNmeiSWfeRfHKgD87YLceZg7", false)] // last character changed: checksum fails
    [InlineData("qzjq98egzWEkH44qJPvhULTxfQNmeiSWfeRfHKgD87YLceZg", false)] // one character short
    [InlineData("15oF4uVJwmo4TdGW7VfQxNLavjCXviqxT9S1MgbjMNHr6Sp5", false)] // a Polkadot address: SS58, but another network
    [InlineData("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", false)]
    public void Quantus_addresses_are_checked_with_their_ss58_checksum(string address, bool valid) =>
        Assert.Equal(valid, CryptoAddresses.TryValidate(AddressFormat.Quantus, address, out _));

    [Fact]
    public void Quantus_is_a_graphics_card_only_coin()
    {
        // Not a main payout coin: nothing mines it on the processor.
        Assert.False(PayoutTarget.TryParse($"QTC:{Quantus1}", out _, out var error));
        Assert.Contains("second address for the graphics card", error);
        Assert.DoesNotContain(PayoutCoins.Quantus, PayoutCoins.All);

        Assert.True(PayoutTarget.TryParseAny($"qtc:{Quantus1}", out var target, out _));
        Assert.Equal($"QTC:{Quantus1}", target.ToString());
        Assert.Equal(PayoutPool.Kryptex, PayoutRouting.PoolFor($"QTC:{Quantus1}"));
        Assert.Equal($"QTC:{Quantus1}", PayoutRouting.QuantusTarget($" QTC:{Quantus1} "));
        Assert.Null(PayoutRouting.QuantusTarget(Usdt));
        Assert.Null(PayoutRouting.QuantusTarget(null));

        // Otium's Quantus fee address never counts as the fee target for a USDT user's pool.
        Assert.Equal(OtiumUsdt, PayoutRouting.FeeTargetFor(Usdt, [$"QTC:{Quantus2}", OtiumUsdt]));
    }

    [Fact]
    public void Each_slice_carries_its_quantus_target()
    {
        var now = DateTimeOffset.UtcNow;
        var user = $"QTC:{Quantus1}";
        var otium = $"QTC:{Quantus2}";
        var standing = new ReferralStanding("USDT:TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf", now.AddDays(-1), null);
        var schedule = new FeeSchedule(FeePolicy.Default, Usdt, OtiumUsdt, standing, user, otium);
        var cycle = FeePolicy.Default.CycleLength;

        Assert.Equal(new MiningTarget(Usdt, PayoutRole.User, user), schedule.TargetAt(TimeSpan.Zero, now));
        // Referrers register only their main address, so Quantus mined in their slice stays with the user.
        var referrerSlice = schedule.TargetAt(cycle - TimeSpan.FromMinutes(25), now);
        Assert.Equal(PayoutRole.Referrer, referrerSlice.Role);
        Assert.Equal(user, referrerSlice.QuantusTarget);
        Assert.Equal(new MiningTarget(OtiumUsdt, PayoutRole.App, otium), schedule.TargetAt(cycle - TimeSpan.FromSeconds(1), now));

        // Without Otium's Quantus address the user keeps Quantus mined in the fee slice; without the user's, there's none.
        Assert.Equal(user, new FeeSchedule(FeePolicy.Default, Usdt, OtiumUsdt, standing, user).TargetAt(cycle - TimeSpan.FromSeconds(1), now).QuantusTarget);
        Assert.Null(new FeeSchedule(FeePolicy.Default, Usdt, OtiumUsdt, standing).TargetAt(TimeSpan.Zero, now).QuantusTarget);
    }

    private static readonly GpuInfo Rtx3080 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 10);
    private static readonly GpuInfo Rtx5090 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 5090", 32);
    private static readonly GpuInfo Rx6700 = new(GpuVendor.Amd, 0, "AMD Radeon RX 6700 XT", 12);

    private static GpuPolicy Policy(IReadOnlyList<string>? pearlOnly = null) => new(
        DateTimeOffset.UtcNow,
        [
            new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "unmineable-pearlhash", 3.4e-14, 0.75, OnlyGpus: pearlOnly),
            new GpuAlgorithmPolicy("quantus", GpuTiers.Primary, "kryptex-address-qtc", 1.6e-9, 3),
        ],
        [
            new GpuPoolRoute("unmineable-pearlhash", GpuRoutes.Unmineable, "pearlpow.unmineable.com", 443, null, Tls: true),
            new GpuPoolRoute("kryptex-address-qtc", GpuRoutes.KryptexAddress, "qtc.kryptex.network", 8049, null, Tls: true),
        ]);

    private static SrbMinerGpuProvider Gpu(GpuPolicy? policy, params GpuInfo[] gpus) => new(
        new HttpClient(), null!, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), $"otium-bench-{Guid.NewGuid():N}.json"),
        gpus.Length > 0 ? gpus : [Rtx3080], null!, () => 0.15, asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64),
        policy: () => policy, referralCode: "ztjq-jr7s");

    [Fact]
    public void Quantus_logs_in_to_kryptex_with_the_slices_own_wallet_and_a_separate_worker()
    {
        var quantus = GpuAlgorithms.Find("quantus")!;
        var options = new ProviderRunOptions(new MiningTarget(Usdt, PayoutRole.User, $"QTC:{Quantus1}"), "ot1234abcdg", 100);

        var login = Gpu(Policy()).LoginFor(quantus, options);
        Assert.Equal(new GpuPoolEndpoint("qtc.kryptex.network", 8049, Tls: true), login.Pool);
        Assert.Equal(Quantus1, login.Wallet);
        Assert.Equal("ot1234abcdg", login.Worker);

        var args = SrbMinerArgs.Build(quantus, login, 40001, [Rtx3080]).ToList();
        Assert.Equal(Quantus1, args[args.IndexOf("--wallet") + 1]);
        Assert.Equal("ot1234abcdg", args[args.IndexOf("--worker") + 1]);
        Assert.Equal("true", args[args.IndexOf("--tls") + 1]);

        // Pearl in the same slice still goes to the payout address on unMineable.
        Assert.Equal($"{Usdt}.ot1234abcdg#ztjq-jr7s", Gpu(Policy()).LoginFor(GpuAlgorithms.Find("pearlhash")!, options).Wallet);

        // No Quantus wallet: no Quantus login (the app leaves the route out of its policy in that case anyway).
        Assert.Throws<InvalidOperationException>(() => Gpu(Policy()).LoginFor(quantus, options with { Target = new MiningTarget(Usdt, PayoutRole.User) }));
    }

    [Fact]
    public async Task After_pearls_fork_only_whitelisted_cards_are_offered_pearl()
    {
        string[] blackwellAndHopper = [@"\bRTX 50\d0\b", @"\bG?H(100|200|800)\b"];
        Assert.Contains(await Algorithms(Policy(), Rtx3080), a => a == "pearlhash");
        Assert.DoesNotContain(await Algorithms(Policy(blackwellAndHopper), Rtx3080), a => a == "pearlhash");
        Assert.Contains(await Algorithms(Policy(blackwellAndHopper), Rtx3080), a => a == "quantus"); // the fallback
        Assert.Contains(await Algorithms(Policy(blackwellAndHopper), Rtx5090), a => a == "pearlhash");
        // Every card in the computer has to qualify, since one miner drives them all.
        Assert.DoesNotContain(await Algorithms(Policy(blackwellAndHopper), Rtx5090, Rtx3080), a => a == "pearlhash");

        var policy = new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "r", 1, 1, OnlyGpus: ["(unclosed"]);
        Assert.False(policy.Allows(["NVIDIA GeForce RTX 5090"])); // a broken pattern allows nothing, never everything
        Assert.True((policy with { OnlyGpus = null }).Allows([Rx6700.Name]));

        static async Task<IEnumerable<string>> Algorithms(GpuPolicy policy, params GpuInfo[] gpus) =>
            (await Gpu(policy, gpus).QuotesAsync(CancellationToken.None)).Select(q => q.Algorithm.PoolName);
    }

    [Fact]
    public void The_default_whitelist_matches_blackwell_and_hopper_names_only()
    {
        var supported = new PearlForkOptions().SupportedGpus;
        bool Allowed(string name) => new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "r", 1, 1, OnlyGpus: supported).Allows([name]);

        Assert.True(Allowed("NVIDIA GeForce RTX 5090"));
        Assert.True(Allowed("NVIDIA GeForce RTX 5060 Ti"));
        Assert.True(Allowed("NVIDIA RTX PRO 6000 Blackwell Workstation Edition"));
        Assert.True(Allowed("NVIDIA H100 80GB HBM3"));
        Assert.True(Allowed("NVIDIA GH200 480GB"));
        Assert.True(Allowed("NVIDIA B200"));
        Assert.False(Allowed("NVIDIA GeForce RTX 4090"));
        Assert.False(Allowed("NVIDIA GeForce RTX 3080"));
        Assert.False(Allowed("NVIDIA RTX A6000"));
        Assert.False(Allowed("AMD Radeon RX 7900 XTX"));
    }

    [Fact]
    public void Quantus_balances_are_read_from_kryptex()
    {
        // Kryptex's live answer for a Quantus address (17 October 2026).
        using var doc = JsonDocument.Parse("""
            {"total":2.5399579220527224,"unconfirmed":0.7141184545637369,"confirmed":1.8258394674889857,"threshold":0.1,
             "reached_pct":1826.0,"last_active":1792321558000,"solo_effort":0,"maturation_time":1200,"banners":[]}
            """);
        var balance = KryptexAddressClient.ParseBalance(doc.RootElement);
        Assert.Equal(2.5399579220527224m, balance.Unpaid);
        Assert.Equal(0.1m, balance.Threshold);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792321558000), balance.LastActive);

        using var empty = JsonDocument.Parse("""{"total":0.0,"unconfirmed":0.0,"confirmed":0.0,"threshold":0.0,"reached_pct":0.0,"last_active":0,"banners":[]}""");
        Assert.Null(KryptexAddressClient.ParseBalance(empty.RootElement).LastActive);

        var target = PayoutTarget.ParseAnyOrNull($"QTC:{Quantus1}")!;
        Assert.Equal($"https://pool.kryptex.com/qtc/miner/stats/{Quantus1}", KryptexAddressClient.DashboardUrl(target).ToString());
    }

    [Fact]
    public void The_cost_breakdown_adds_kryptex_and_srbminer_on_quantus()
    {
        var lines = CostBreakdown.For(FeePolicy.Default, PayoutPool.Unmineable, PayoutPool.Unmineable, referralCode: true, quantus: true);
        Assert.Equal("while the graphics card mines Quantus for your Quantus wallet, Kryptex keeps 3% and SRBMiner 2.5%: about 7.5% in total.",
            Assert.Single(lines, l => l.Who == "Quantus").What);
        Assert.DoesNotContain(CostBreakdown.For(FeePolicy.Default, PayoutPool.Unmineable, gpuPool: null, referralCode: true, quantus: true), l => l.Who == "Quantus");
    }

    // ---------- Pearl's fork ----------

    /// <summary>PIP-3's text as of 17 October 2026 (trimmed): a draft whose activation height is still to be decided.</summary>
    private const string Pip3Draft = """
        ---
        pip: 3
        title: FP PoUW Certificates
        status: Draft
        type: Standards Track
        category: Consensus
        created: 2026-07-30
        ---

        ## Abstract

        Certificate version 3 becomes mandatory at the activation height: version 1 and 2 certificates are rejected from this height onward.

        Hardware is therefore whitelisted - this proposal supports the Hopper and Blackwell architectures.

        Jackpot Policy: TBD
        """;

    [Theory]
    [InlineData(Pip3Draft, null)]
    [InlineData("---\npip: 3\nstatus: Final\nactivation-height: 150000\n---\nText", 150000L)]
    [InlineData("Version 3 activates at block 150,000 on mainnet.", 150000L)]
    [InlineData("The activation height is 162500.", 162500L)]
    [InlineData("Activation height: TBD (expected around block 150000)", null)]
    [InlineData("", null)]
    public void The_activation_height_is_read_from_pip3_only_when_it_names_one(string text, long? expected) =>
        Assert.Equal(expected, PearlForkWatcher.ActivationHeightIn(text));

    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly GpuPolicyTests.FakeKryptex _kryptex = new();
    private readonly GpuPolicyTests.FakePip _pip = new() { Text = Pip3Draft };
    private readonly GpuPolicyOptions _policyOptions = new();

    private PearlForkWatcher Watcher() => new(_kryptex, _pip, Options.Create(_policyOptions), _time, NullLogger<PearlForkWatcher>.Instance);

    [Fact]
    public async Task The_fork_activates_when_the_chain_reaches_the_height_pip3_names()
    {
        _kryptex.Heights["prl"] = 129_679;
        var watcher = Watcher();
        Assert.False((await watcher.GetAsync(CancellationToken.None)).Active); // still a draft without a height

        // PIP-3 gains a height; the watcher sees it at its next check.
        _pip.Text = Pip3Draft.Replace("status: Draft", "status: Final\nactivation-height: 140000", StringComparison.Ordinal);
        Assert.False((await watcher.GetAsync(CancellationToken.None)).Active); // cached for an hour
        _time.Advance(_policyOptions.PearlFork.CheckInterval);
        var before = await watcher.GetAsync(CancellationToken.None);
        Assert.False(before.Active);
        Assert.Equal(140_000, before.ActivationHeight);
        Assert.Contains("activates at block 140000 (now 129679)", before.Reason);

        _kryptex.Heights["prl"] = 140_000;
        _time.Advance(_policyOptions.PearlFork.CheckInterval);
        Assert.True((await watcher.GetAsync(CancellationToken.None)).Active);
    }

    [Fact]
    public async Task The_operator_can_set_the_height_or_the_state()
    {
        _kryptex.Heights["prl"] = 150_000;
        _policyOptions.PearlFork.ActivationHeight = 149_000; // overrides PIP-3, which names none
        Assert.True((await Watcher().GetAsync(CancellationToken.None)).Active);

        _policyOptions.PearlFork.Mode = "off";
        Assert.False((await Watcher().GetAsync(CancellationToken.None)).Active);

        _policyOptions.PearlFork.Mode = "active";
        _policyOptions.PearlFork.ActivationHeight = null;
        _kryptex.Heights.Clear();
        Assert.True((await Watcher().GetAsync(CancellationToken.None)).Active);
    }

    [Fact]
    public async Task An_active_fork_limits_pearl_in_the_policy_to_supported_cards()
    {
        var feed = new GpuPolicyTests.FakeFeed();
        feed.Coins[469] = new CoinRevenue(3.4e-14, false, _time.GetUtcNow());
        feed.Coins[473] = new CoinRevenue(1.6e-9, false, _time.GetUtcNow());
        _policyOptions.PearlFork.Mode = "active";
        var service = new GpuPolicyService(new GpuPolicyTests.FakePool(), feed, _kryptex, Options.Create(_policyOptions),
            Options.Create(new KryptexOptions()), _time, NullLogger<GpuPolicyService>.Instance,
            Options.Create(new UnmineableOptions()), Options.Create(new CustodyOptions()), Watcher());

        var policy = await service.BuildAsync(CancellationToken.None);
        var pearl = policy.For("pearlhash")!;
        Assert.Equal(GpuTiers.Primary, pearl.Tier); // still mined, but only by the cards the fork keeps
        Assert.Equal(_policyOptions.PearlFork.SupportedGpus, pearl.OnlyGpus);
        Assert.Contains("only Hopper and Blackwell cards", pearl.Note);
        Assert.Null(policy.For("quantus")!.OnlyGpus);
        Assert.Equal(GpuRoutes.KryptexAddress, policy.Route(policy.For("quantus")!.Route)!.Kind);

        // The policy reaches apps as JSON: the whitelist must survive the trip.
        var json = JsonSerializer.Serialize(policy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var back = JsonSerializer.Deserialize<GpuPolicy>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(pearl.OnlyGpus, back.For("pearlhash")!.OnlyGpus);
    }
}

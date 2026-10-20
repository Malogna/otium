using System.Runtime.InteropServices;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;
using Otium.Providers.Proxybase;

namespace Otium.Tests;

/// <summary>
/// Internet sharing through Proxybase's client, with a fake client that answers like the real one (proxybase-cli
/// 0.1.60's output): wallet set-up, sign-in, the direct-only relay, and paying earnings into the conversion the server
/// opens to the user's own coin and address.
/// </summary>
public sealed class ProxybaseTests : IDisposable
{
    private const string NodeAddress = "0xb956cb455901fba57a330221bb1caa00c3ec7acb";
    private const string DepositAddress = "0xae1c7232180e29a332e9eee015f9a1082c8a2beb";

    private readonly string _engines = Path.Combine(Path.GetTempPath(), $"otium-proxybase-{Guid.NewGuid():N}");
    private readonly FakeCli _cli = new();
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly List<SharingRouteRequest> _routeRequests = [];
    private SharingRoute _route = new(SharingRouteDecisions.Pay, DepositAddress, 0, "4.18 USDT (TRON)", 16.5);

    private sealed class FakeSeller : ISellerProcess
    {
        public bool HasExited { get; set; }
        public bool Stopped { get; private set; }
        public string RecentOutput => "";

        public Task StopAsync(CancellationToken ct)
        {
            Stopped = true;
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeCli : IProxybaseCli
    {
        public bool HasWallet { get; set; }
        public bool LoginWorks { get; set; } = true;
        public long SellerAvailable { get; set; }
        public List<string> Commands { get; } = [];
        public List<FakeSeller> Sellers { get; } = [];

        public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
        {
            var command = string.Join(' ', arguments);
            Commands.Add(command);
            return Task.FromResult(command switch
            {
                "wallet info" => HasWallet
                    ? new CliResult(0, $"Address: {NodeAddress}\nStatus: Loaded\n")
                    : new CliResult(0, "No wallet found. Run 'proxybase-cli wallet create' first.\n"),
                "wallet create" => Create(),
                "login" => LoginWorks
                    ? new CliResult(0, $"Got challenge nonce: 1a2b3c4d5e6f7a8b...\nAuthenticated as: {NodeAddress}\n  Role: seller\nSession token saved.\n")
                    : new CliResult(1, "Error: Failed to load wallet."),
                "buyer balance" => new CliResult(0, $$"""
                    {
                      "buyer_available": 0,
                      "seller_available": {{SellerAvailable}},
                      "spendable_balance": 0
                    }
                    """),
                _ when command.StartsWith("seller payout create", StringComparison.Ordinal) => Payout(),
                _ => new CliResult(2, "unexpected"),
            });
        }

        private CliResult Create()
        {
            HasWallet = true;
            return new CliResult(0, $"Wallet created successfully!\nAddress: {NodeAddress}\nMnemonic (SAVE THIS SECURELY):\n  apple banana cherry dog elephant fox grape horse igloo jaguar kite lemon\n");
        }

        private int _payouts;

        private CliResult Payout()
        {
            var arguments = Commands[^1].Split(' ');
            SellerAvailable -= long.Parse(arguments[Array.IndexOf(arguments, "--amount") + 1], System.Globalization.CultureInfo.InvariantCulture);
            return new CliResult(0, $$"""{ "payout_id": "po_{{(++_payouts == 1 ? "123" : _payouts.ToString(System.Globalization.CultureInfo.InvariantCulture))}}", "status": "pending" }""");
        }

        public ISellerProcess StartSeller()
        {
            var seller = new FakeSeller();
            Sellers.Add(seller);
            return seller;
        }
    }

    private ProxybaseBandwidthProvider Provider() =>
        new(new HttpClient(), _engines, (request, _) =>
        {
            _routeRequests.Add(request);
            return Task.FromResult(_route.Decision == SharingRouteDecisions.Pay ? _route with { AmountMicrocredits = request.AvailableMicrocredits } : _route);
        }, _cli, new ProxybaseAsset("test.zip", "00"), _time);

    [Fact]
    public void Seller_status_says_whether_buyers_are_connected_and_how_the_connection_tests()
    {
        // What `seller status` printed on a real node in Norway that was online but hadn't sold anything yet.
        const string output = """
            Daemon:  running (PID: 25736)
            Node Type: Standard
            Backend node_type: Standard
            {
              "active_streams": 0,
              "country": "NO",
              "earnings": { "seller_available": 0, "seller_payout_locked": 0, "seller_pending": 0, "spendable_balance": 0 },
              "max_streams": 200,
              "node_type": "standard",
              "pool": "production",
              "proxy_category": "residential",
              "qos_latency_ms": 1536.8177499152357,
              "qos_throughput_mbps": null,
              "qos_uptime": 0.9995784790654522,
              "wallet_address": "0x0000000000000000000000000000000000000000"
            }
            """;
        var node = ProxybaseOutput.Node(output)!;
        Assert.Equal("Online, waiting for a buyer", node.Activity);
        Assert.Equal(
            "Listed for Proxybase's buyers as a residential connection in Norway, and waiting for the first one. Nothing sold yet. " +
            "Proxybase's checks: reachable 99.95% of the time, answers in 1.5 s.",
            node.Describe(earnedAnything: false));

        Assert.False(node.Suspended);

        // Open streams include Proxybase's own test requests, so they're connections, not buyers.
        var busy = node with { ActiveStreams = 2, PendingMicrocredits = 120_000 };
        Assert.Equal("2 connections open now", busy.Activity);
        Assert.StartsWith("Open now: 2 connections through this computer, from buyers or Proxybase's own tests.", busy.Describe(earnedAnything: true));
        Assert.EndsWith("$0.120 is still clearing at Proxybase.", busy.Describe(earnedAnything: true));
        Assert.Null(ProxybaseOutput.Node("Daemon:  not running"));
    }

    [Fact]
    public void The_trial_pool_and_buyers_across_Proxybase_are_shown()
    {
        // What `health` printed on 20 October 2026, and the same Norwegian node in Proxybase's trial pool.
        var network = ProxybaseOutput.Network("""
            {
              "active_sessions": 4,
              "connected_sellers": 788,
              "status": "ok",
              "uptime_seconds": 1792519824
            }
            """);
        Assert.Equal(new ProxybaseNetwork(4, 788), network);
        Assert.Null(ProxybaseOutput.Network("Error: backend unreachable"));

        var trial = new ProxybaseNode(0, 200, "NO", "residential", 3668, 0.9999, 0, "trial", network);
        Assert.True(trial.Trial);
        Assert.Equal("Trial pool · waiting for a buyer · 4 buyer sessions on Proxybase", trial.Activity);
        var text = trial.Describe(earnedAnything: false);
        Assert.StartsWith("In Proxybase's trial pool: new computers are tested with a few buyers first", text);
        Assert.Contains("Across Proxybase right now: 4 buyer sessions and 788 sellers online.", text);

        var production = trial with { Pool = "production", ActiveStreams = 1, Network = new ProxybaseNetwork(1, 790) };
        Assert.Equal("1 connection open now · 1 buyer session on Proxybase", production.Activity);
        Assert.DoesNotContain("trial pool", production.Describe(earnedAnything: false));
        Assert.Equal("Paused by Proxybase: too slow in its tests", (trial with { Pool = "suspended" }).Activity);
    }

    [Fact]
    public void A_node_Proxybase_suspended_says_so_instead_of_waiting_for_buyers()
    {
        // The same node in Norway later that day: Proxybase's tests through it took 13.9 s and it left the buyers' pool.
        const string output = """
            {
              "active_streams": 0,
              "country": "NO",
              "earnings": { "seller_available": 0, "seller_payout_locked": 0, "seller_pending": 0, "spendable_balance": 0 },
              "max_streams": 200,
              "node_type": "standard",
              "pool": "suspended",
              "proxy_category": "residential",
              "qos_latency_ms": 13936.304795475138,
              "qos_throughput_mbps": null,
              "qos_uptime": 0.9999923080844556,
              "wallet_address": "0x0000000000000000000000000000000000000000"
            }
            """;
        var node = ProxybaseOutput.Node(output)!;
        Assert.True(node.Suspended);
        Assert.Equal("Paused by Proxybase: too slow in its tests", node.Activity);
        Assert.Equal(
            "Proxybase has paused this computer, so buyers aren't sent to it: its test requests through it were too slow. " +
            "Otium keeps it online so Proxybase can test it again and list it once it answers quicker. " +
            "Proxybase's checks: reachable 99.99% of the time, answers in 13.9 s.",
            node.Describe(earnedAnything: false));
    }

    [Fact]
    public async Task First_start_makes_a_wallet_signs_in_and_relays_this_connection_only()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);

        Assert.Equal(["wallet info", "wallet create", "login"], _cli.Commands);
        Assert.Equal(NodeAddress, provider.NodeAddress);
        Assert.Single(_cli.Sellers);
        var config = await File.ReadAllTextAsync(Path.Combine(_engines, "proxybase-data", "seller_config.json"));
        Assert.Contains("\"no_direct\":false", config);
        Assert.Contains("\"upstream_proxies\":[]", config);
        Assert.Equal(BandwidthProviderState.Online, (await provider.GetStatusAsync(CancellationToken.None)).State);

        // A second start reuses the wallet, and stopping ends the relay.
        await provider.StopAsync(CancellationToken.None);
        Assert.True(_cli.Sellers[0].Stopped);
        _cli.Commands.Clear();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        Assert.Equal(["wallet info", "login"], _cli.Commands);
    }

    [Fact]
    public async Task A_wallet_the_client_cant_open_is_never_replaced()
    {
        // "wallet info" says "No wallet found" for a damaged or locked file too; "wallet create" would overwrite it.
        var wallet = Path.Combine(_engines, "proxybase-data", "wallet");
        Directory.CreateDirectory(wallet);
        await File.WriteAllTextAsync(Path.Combine(wallet, "keyfile.enc"), "damaged");

        var e = await Assert.ThrowsAsync<ProviderProblemException>(() => Provider().StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None));
        Assert.Contains("couldn't open this computer's wallet", e.Message);
        Assert.Equal(["wallet info"], _cli.Commands); // no "wallet create", no sign-in, nothing relayed
        Assert.Empty(_cli.Sellers);
        Assert.Equal("damaged", await File.ReadAllTextAsync(Path.Combine(wallet, "keyfile.enc")));
    }

    [Fact]
    public void The_client_only_sees_the_environment_it_needs()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Windows\system32",
            ["SystemRoot"] = @"C:\Windows",
            ["TEMP"] = @"C:\Temp",
            ["HTTPS_PROXY"] = "http://proxy.example:3128",
            ["GITHUB_TOKEN"] = "ghp_secret", // the client sends it with its update checks
            ["GH_TOKEN"] = "gho_secret",
            ["PROXYBASE_PASSWORD"] = "x", // would change how the wallet is opened
            ["PROXYBASE_UPDATE_REPO"] = "someone/else",
            ["PROXYBASE_BACKEND"] = "https://evil.example",
            ["AWS_SECRET_ACCESS_KEY"] = "secret",
            ["OTIUM_SERVER_URL"] = "https://otium.lol",
        };
        ProxybaseCli.LimitEnvironment(environment, @"C:\Otium\proxybase-data");

        Assert.Equal(["HTTPS_PROXY", "NO_COLOR", "PATH", "PROXYBASE_DIR", "SystemRoot", "TEMP"], environment.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(@"C:\Otium\proxybase-data", environment["PROXYBASE_DIR"]);
    }

    [Fact]
    public async Task A_failed_sign_in_is_explained_and_nothing_relays()
    {
        _cli.LoginWorks = false;
        var e = await Assert.ThrowsAsync<ProviderProblemException>(() => Provider().StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None));
        Assert.Contains("sign in", e.Message);
        Assert.Empty(_cli.Sellers);
    }

    [Fact]
    public async Task Earnings_are_paid_into_the_conversion_the_server_opens_for_the_users_coin()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);

        _cli.SellerAvailable = 420_000; // $0.42: below Proxybase's minimum, so the server isn't even asked
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Empty(_routeRequests);
        Assert.Equal("$0.41 earned so far: $0.41 waiting at Proxybase, $0.00 paid out to your wallet", provider.EarningsText()); // 2% is Otium's

        _cli.SellerAvailable = 16_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        var asked = Assert.Single(_routeRequests);
        Assert.Equal(16_000_000, asked.AvailableMicrocredits);
        Assert.Equal(NodeAddress, asked.RefundAddress); // a failed conversion refunds to this computer's own wallet
        Assert.Contains($"seller payout create --amount 16000000 --tempo-address {DepositAddress}", _cli.Commands);
        var payout = Assert.Single(provider.Payouts);
        Assert.Equal("po_123", payout.PayoutId);
        Assert.Equal("4.18 USDT (TRON)", payout.ExpectedOutput);
        Assert.Equal("$16.00 earned so far: $0.00 waiting at Proxybase, $16.00 paid out to your wallet (last: 4.18 USDT (TRON))", provider.EarningsText());

        // The record survives a restart of Otium.
        Assert.Single(Provider().Payouts);
    }

    [Fact]
    public async Task Nothing_is_paid_to_a_deposit_address_that_isnt_one()
    {
        // A compromised or broken server: Relay's deposit addresses are 0x addresses on Tempo, so anything else is refused.
        _route = _route with { DepositAddress = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t" };
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        _cli.SellerAvailable = 16_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.DoesNotContain(_cli.Commands, c => c.StartsWith("seller payout create", StringComparison.Ordinal));
        Assert.Empty(provider.Payouts);
    }

    [Fact]
    public async Task Otiums_fee_is_kept_back_and_paid_to_otiums_address_from_one_dollar_never_through_the_conversion()
    {
        const string otium = "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";
        var provider = new ProxybaseBandwidthProvider(new HttpClient(), _engines, (request, _) =>
        {
            _routeRequests.Add(request);
            var fee = request.AvailableMicrocredits / 50; // 2%, as the server works it out
            return Task.FromResult(_route with { AmountMicrocredits = request.AvailableMicrocredits - fee, FeeAddress = otium, FeeMicrocredits = fee });
        }, _cli, new ProxybaseAsset("test.zip", "00"), _time);
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);

        // $20: $19.60 goes to the user's conversion; Otium's $0.40 stays at Proxybase, below its $1 minimum.
        _cli.SellerAvailable = 20_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Contains($"seller payout create --amount 19600000 --tempo-address {DepositAddress}", _cli.Commands);
        Assert.DoesNotContain(_cli.Commands, c => c.EndsWith(otium, StringComparison.Ordinal));
        Assert.Equal(400_000, provider.Fee.Reserved);
        Assert.Equal(400_000, _cli.SellerAvailable);

        // $40 more: the kept-back $0.40 isn't offered for conversion, and once Otium's share reaches $1.20 it's paid to Otium.
        _cli.SellerAvailable += 40_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Equal(40_000_000, _routeRequests[^1].AvailableMicrocredits);
        Assert.Contains($"seller payout create --amount 39200000 --tempo-address {DepositAddress}", _cli.Commands);
        Assert.Contains($"seller payout create --amount 1200000 --tempo-address {otium}", _cli.Commands);
        Assert.Equal(new ProxybaseFee(0, otium, 1_200_000), provider.Fee);
        Assert.Equal(0, _cli.SellerAvailable);
        Assert.Equal(58_800_000, provider.Payouts.Sum(p => p.Microcredits)); // the user's payouts only
    }

    [Fact]
    public async Task What_sharing_earned_and_its_pace_are_known_and_survive_a_restart()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        _route = new SharingRoute(SharingRouteDecisions.Wait, Message: "Waiting");

        _cli.SellerAvailable = 1_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Null(provider.Earned.UserUsdPerDay(_time.GetUtcNow())); // not an hour of history yet

        _time.Advance(TimeSpan.FromHours(2));
        _cli.SellerAvailable = 3_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Equal(3 * 0.98, provider.Earned.UserUsd, precision: 6); // the user's share, after Otium's 2%
        Assert.Equal(24 * 0.98, provider.Earned.UserUsdPerDay(_time.GetUtcNow())!.Value, precision: 6); // $2 in 2 hours

        // Status carries the numbers, and a restarted Otium starts from the last known total (the history never dips).
        var status = await provider.GetStatusAsync(CancellationToken.None);
        Assert.Equal(3 * 0.98, status.UserUsd!.Value, precision: 6);
        Assert.Equal(3 * 0.98, ((IBandwidthProvider)Provider()).LastKnownUserUsd!.Value, precision: 6);
    }

    [Fact]
    public async Task When_the_server_says_wait_nothing_is_paid_and_the_reason_shows()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        _route = new SharingRoute(SharingRouteDecisions.Wait, Message: "Waiting until about $15 has built up, so the network fee ($0.75 a payout) stays small.");
        _cli.SellerAvailable = 5_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);

        Assert.DoesNotContain(_cli.Commands, c => c.StartsWith("seller payout", StringComparison.Ordinal));
        _time.Advance(ProxybaseBandwidthProvider.BalanceInterval);
        Assert.Equal(_route.Message, (await provider.GetStatusAsync(CancellationToken.None)).Message);
    }

    [Fact]
    public async Task The_balance_is_read_every_ten_minutes_not_every_tick()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        for (var i = 0; i < 60 * 12; i++) // an hour of 5-second ticks
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            await provider.GetStatusAsync(CancellationToken.None);
        }
        Assert.InRange(_cli.Commands.Count(c => c == "buyer balance"), 6, 7);
    }

    [Fact]
    public async Task A_stopped_relay_is_reported_so_it_gets_restarted()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        _cli.Sellers[0].HasExited = true;
        Assert.Equal(BandwidthProviderState.Error, (await provider.GetStatusAsync(CancellationToken.None)).State);

        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        Assert.Equal(2, _cli.Sellers.Count);
    }

    [Fact]
    public void The_clients_output_is_read_as_published()
    {
        Assert.Equal(NodeAddress, ProxybaseOutput.Address($"Wallet created successfully!\nAddress: {NodeAddress.ToUpperInvariant().Replace("0X", "0x")}\n"));
        Assert.Null(ProxybaseOutput.Address("No wallet found. Run 'proxybase-cli wallet create' first."));
        Assert.Equal(3_500_000, ProxybaseOutput.SellerAvailable("Update available: 0.1.61\n{\n  \"seller_available\": 3500000\n}"));
        Assert.Null(ProxybaseOutput.SellerAvailable("Error: Not authenticated. Run 'proxybase-cli login' first."));
        Assert.Equal("po_9", ProxybaseOutput.PayoutId("""{"payout_id":"po_9"}"""));
        Assert.Null(ProxybaseOutput.PayoutId("""{"error":"insufficient balance"}"""));
    }

    [Fact]
    public void Pinned_clients_exist_for_windows_linux_and_apple_silicon()
    {
        Assert.Equal("proxybase-cli-x86_64-pc-windows-msvc.zip", ProxybaseRelease.For(OSPlatform.Windows, Architecture.X64)!.FileName);
        Assert.NotNull(ProxybaseRelease.For(OSPlatform.Linux, Architecture.X64));
        Assert.NotNull(ProxybaseRelease.For(OSPlatform.Linux, Architecture.Arm64));
        Assert.NotNull(ProxybaseRelease.For(OSPlatform.OSX, Architecture.Arm64));
        Assert.Null(ProxybaseRelease.For(OSPlatform.OSX, Architecture.X64)); // no Intel Mac build
        Assert.Equal(
            "https://github.com/proxybasehq/proxybase-cli/releases/download/proxybase-cli-v0.1.60/proxybase-cli-x86_64-pc-windows-msvc.zip",
            ProxybaseRelease.For(OSPlatform.Windows, Architecture.X64)!.DownloadUrl.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_engines))
            Directory.Delete(_engines, recursive: true);
    }

    [Fact]
    public void Proxybases_rate_where_the_user_is_gives_the_estimate_before_there_is_a_real_pace()
    {
        var norway = ProxybaseMarkets.For("NO");
        Assert.True(norway.Listed);
        Assert.Equal(2.10, norway.SellerUsdPerGb);
        Assert.Equal(2.10 * 0.98, ProxybaseMarkets.EstimatedUserUsdPerDay(norway, 0.02), precision: 6); // 1 GB a day, after Otium's 2%

        var elsewhere = ProxybaseMarkets.For("KE"); // not listed on its own: Proxybase's worldwide rate
        Assert.False(elsewhere.Listed);
        Assert.Equal(ProxybaseMarkets.WorldWideUsdPerGb, elsewhere.SellerUsdPerGb);
        Assert.False(ProxybaseMarkets.For(null).Listed);
    }
}

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
        Assert.Equal("$0.42 waiting at Proxybase; $0.00 paid out to your wallet so far", provider.EarningsText());

        _cli.SellerAvailable = 16_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        var asked = Assert.Single(_routeRequests);
        Assert.Equal(16_000_000, asked.AvailableMicrocredits);
        Assert.Equal(NodeAddress, asked.RefundAddress); // a failed conversion refunds to this computer's own wallet
        Assert.Contains($"seller payout create --amount 16000000 --tempo-address {DepositAddress}", _cli.Commands);
        var payout = Assert.Single(provider.Payouts);
        Assert.Equal("po_123", payout.PayoutId);
        Assert.Equal("4.18 USDT (TRON)", payout.ExpectedOutput);
        Assert.Equal("$0.00 waiting at Proxybase; $16.00 paid out to your wallet so far (last: 4.18 USDT (TRON))", provider.EarningsText());

        // The record survives a restart of Otium.
        Assert.Single(Provider().Payouts);
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
}

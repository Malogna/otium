using System.Runtime.InteropServices;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;
using Otium.Providers.Proxybase;

namespace Otium.Tests;

/// <summary>
/// Internet sharing through Proxybase's client, with a fake client that answers like the real one (proxybase-cli
/// 0.1.60's output): wallet set-up, sign-in, the direct-only relay, and paying earnings to the user's own address.
/// </summary>
public sealed class ProxybaseTests : IDisposable
{
    private const string NodeAddress = "0xb956cb455901fba57a330221bb1caa00c3ec7acb";
    private const string UserAddress = "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";

    private readonly string _engines = Path.Combine(Path.GetTempPath(), $"otium-proxybase-{Guid.NewGuid():N}");
    private readonly FakeCli _cli = new();
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private string? _beneficiary = UserAddress;

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

        private CliResult Payout()
        {
            SellerAvailable = 0;
            return new CliResult(0, """{ "payout_id": "po_123", "status": "pending" }""");
        }

        public ISellerProcess StartSeller()
        {
            var seller = new FakeSeller();
            Sellers.Add(seller);
            return seller;
        }
    }

    private ProxybaseBandwidthProvider Provider() =>
        new(new HttpClient(), _engines, () => _beneficiary, _cli, new ProxybaseAsset("test.zip", "00"), _time);

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
    public async Task From_one_dollar_earnings_are_paid_to_the_users_own_address()
    {
        var provider = Provider();
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);

        _cli.SellerAvailable = 420_000; // $0.42: below the minimum
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.DoesNotContain(_cli.Commands, c => c.StartsWith("seller payout", StringComparison.Ordinal));
        Assert.Equal("$0.42 waiting at Proxybase; $0.00 paid to 0x5aae…eaed so far, in USDC on Tempo, automatically from $1", provider.EarningsText());

        _cli.SellerAvailable = 1_250_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.Contains($"seller payout create --amount 1250000 --tempo-address {UserAddress}", _cli.Commands);
        var payout = Assert.Single(provider.Payouts);
        Assert.Equal("po_123", payout.PayoutId);
        Assert.Contains("$1.25 paid", provider.EarningsText());

        // The record survives a restart of Otium.
        Assert.Single(Provider().Payouts);
    }

    [Fact]
    public async Task Without_an_address_nothing_is_paid_and_sharing_says_why()
    {
        _beneficiary = null;
        var provider = Provider();
        Assert.Equal("Add a 0x address for internet sharing payouts.", provider.UnavailableReason);

        _beneficiary = UserAddress;
        await provider.StartAsync(new BandwidthRunOptions("w1"), CancellationToken.None);
        _beneficiary = null;
        _cli.SellerAvailable = 2_000_000;
        await provider.CheckBalanceAndPayAsync(CancellationToken.None);
        Assert.DoesNotContain(_cli.Commands, c => c.StartsWith("seller payout", StringComparison.Ordinal));
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

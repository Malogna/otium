using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Core.Wallets;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Gpu;
using Otium.Server.Sharing;

namespace Otium.Tests;

/// <summary>
/// Internet sharing payouts reach the user's own coin: the server opens a one-time conversion from Proxybase's Tempo
/// USDC (Relay, plus SideShift for LTC or ChangeNOW for XMR), with no Otium fee in it; Otium's share is paid separately,
/// the exact amount the server names, and the server holds payouts while a payment is overdue.
/// </summary>
public sealed class SharingRouteTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-fees-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeTempo _tempo = new();
    private OtiumStore? _store;

    private OtiumStore Store => _store ??= new OtiumStore($"Data Source={_dbPath};Pooling=False");

    public void Dispose()
    {
        if (_store is null)
            return;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            File.Delete(file);
    }

    /// <summary>Tempo as the fee check reads it: a chain head and the transfers made so far.</summary>
    private sealed class FakeTempo : ITempoTransfers
    {
        public long Head { get; set; } = 1_000_000;
        public List<TempoTransfer> Transfers { get; } = [];
        public bool Down { get; set; }

        public void Pay(string to, long amount) => Transfers.Add(new TempoTransfer(to, amount, $"0x{Transfers.Count:x64}:0", ++Head));

        public Task<long> GetBlockNumberAsync(CancellationToken ct) =>
            Down ? throw new HttpRequestException("Tempo's RPC didn't answer") : Task.FromResult(Head);

        public Task<IReadOnlyList<TempoTransfer>> GetTransfersAsync(string token, IReadOnlyList<string> recipients, long fromBlock, long toBlock, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TempoTransfer>>(Transfers.Where(t => recipients.Contains(t.To) && t.Block >= fromBlock && t.Block <= toBlock).ToList());
    }

    private SharingFees Fees(SharingOptions? options = null) =>
        new(Store, _tempo, Options.Create(options ?? new SharingOptions { FeeRecipient = OtiumFee }), _time, NullLogger<SharingFees>.Instance);
    private const string Refund = "0xb956cb455901fba57a330221bb1caa00c3ec7acb";
    private const string OtiumFee = "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";

    /// <summary>Relay's answer for $5 of Tempo USDC to USDT on TRON (trimmed from a live read-only quote, 18 October 2026).</summary>
    private const string RelayUsdtQuote = """
        {"requestId":"0x179235315618aa83",
         "steps":[{"id":"deposit","kind":"transaction","requestId":"0x179235315618aa83",
           "depositAddress":"0xae1c7232180e29a332e9eee015f9a1082c8a2beb",
           "items":[{"status":"incomplete","data":{"to":"0x20c0","chainId":4217}}]}],
         "fees":{"gas":{"amountUsd":"0.000037"},"relayer":{"amountUsd":"0.775564"},"relayerGas":{"amountUsd":"0.747569"},
                 "app":{"amount":"50000","amountFormatted":"0.05","amountUsd":"0.049994"}},
         "details":{"currencyIn":{"amount":"5000000","amountFormatted":"5.0","amountUsd":"4.999405"},
                    "currencyOut":{"amount":"4178330","amountFormatted":"4.17833","amountUsd":"4.173124"}}}
        """;

    [Fact]
    public void Relays_quote_is_read_at_the_paths_it_answers_with()
    {
        var quote = RelayClient.ParseQuote(RelayUsdtQuote);
        Assert.Equal("0xae1c7232180e29a332e9eee015f9a1082c8a2beb", quote.DepositAddress);
        Assert.Equal("0x179235315618aa83", quote.RequestId);
        Assert.Equal("4.17833", quote.OutAmount);
        Assert.Equal(4.999405, quote.InUsd, precision: 6);
        Assert.Equal(4.173124, quote.OutUsd, precision: 6);
        Assert.Equal(0.049994, quote.AppFeeUsd, precision: 6);
    }

    private sealed class CapturingHandler(string response) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }

    [Fact]
    public async Task The_quote_asks_for_an_open_deposit_address_with_otiums_fee_to_the_users_address()
    {
        var handler = new CapturingHandler(RelayUsdtQuote);
        var relay = new RelayClient(new HttpClient(handler), Options.Create(new SharingOptions { RelayApiKey = "k" }));
        await relay.QuoteDepositAddressAsync(RelayClient.DirectFor(PayoutCoins.Usdt)!, "TDqSquXBgUCLYvYC4XZgrprLK589dkhSCf", 5_000_000, Refund, OtiumFee, 200, CancellationToken.None);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.relay.link/quote/v2", request.RequestUri!.ToString());
        Assert.Equal("k", request.Headers.GetValues("x-api-key").Single());
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal(4217, root.GetProperty("originChainId").GetInt64());
        Assert.Equal(RelayClient.TempoUsdc, root.GetProperty("originCurrency").GetString());
        Assert.Equal(728126428, root.GetProperty("destinationChainId").GetInt64());
        Assert.Equal("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", root.GetProperty("destinationCurrency").GetString());
        Assert.Equal("5000000", root.GetProperty("amount").GetString());
        Assert.Equal("TDqSquXBgUCLYvYC4XZgrprLK589dkhSCf", root.GetProperty("recipient").GetString());
        Assert.True(root.GetProperty("useDepositAddress").GetBoolean());
        Assert.False(root.TryGetProperty("strict", out _));
        Assert.Equal(Refund, root.GetProperty("refundTo").GetString());
        var fee = Assert.Single(root.GetProperty("appFees").EnumerateArray().ToList());
        Assert.Equal(OtiumFee, fee.GetProperty("recipient").GetString());
        Assert.Equal("200", fee.GetProperty("fee").GetString());
    }

    private sealed class FakeRelay : IRelayApi
    {
        public double NetworkCostUsd { get; set; } = 0.75;
        public List<(RelayDestination Destination, string Recipient, long Amount, bool WithAppFee)> Quotes { get; } = [];

        public Task<RelayQuote> QuoteDepositAddressAsync(RelayDestination destination, string recipient, long amountMicro, string refundTo,
            string? feeRecipient, int feeBps, CancellationToken ct)
        {
            Quotes.Add((destination, recipient, amountMicro, feeRecipient is not null && feeBps > 0));
            var usd = amountMicro / 1e6;
            var app = feeRecipient is null ? 0 : usd * feeBps / 10_000;
            var cost = destination.ChainId == 728126428 ? NetworkCostUsd : 0.03;
            return Task.FromResult(new RelayQuote($"0xdeposit{Quotes.Count}", $"req{Quotes.Count}", (usd - app - cost).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), usd, usd - app - cost, app));
        }
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private SharingRouteService Service(FakeRelay relay, SharingOptions? options = null, TimeProvider? time = null, HttpMessageHandler? partner = null)
    {
        time ??= _time;
        options ??= new SharingOptions { FeeRecipient = OtiumFee };
        return new(relay, new SideShiftLitecoin(new HttpClient(partner ?? new HttpClientHandler()), Options.Create(options)),
            new ChangeNowMonero(new HttpClient(), Options.Create(options)),
            Options.Create(options), Options.Create(new FeeOptions { FeePercent = 2 }),
            Options.Create(new ProxybaseOptions()), new ConnectionQuotas(time), Fees(options), time, NullLogger<SharingRouteService>.Instance);
    }

    private static Session SessionFor(string payout, string? accountId = null)
    {
        var id = accountId ?? Guid.NewGuid().ToString("N");
        return new Session(new DeviceRecord(id, "x", id, T0, T0), new AccountRecord(id, "w" + id, null, payout, "c" + id, null, null, null, null, false, null, null, T0));
    }

    [Fact]
    public void Small_payouts_wait_until_the_fixed_network_fee_is_a_small_share()
    {
        var service = Service(new FakeRelay());
        var quote = RelayClient.ParseQuote(RelayUsdtQuote); // $5 in, $0.78 of it network and relayer costs
        var wait = service.Decide(quote, 5, "4.18 USDT (TRON)", 5_000_000);
        Assert.Equal(SharingRouteDecisions.Wait, wait.Decision);
        Assert.Contains("about $16", wait.Message);

        var big = quote with { InUsd = 16, OutUsd = 16 - 0.78 - 0.32, AppFeeUsd = 0.32 };
        var pay = service.Decide(big, 16, "14.9 USDT (TRON)", 16_000_000);
        Assert.Equal(SharingRouteDecisions.Pay, pay.Decision);
        Assert.Equal("0xae1c7232180e29a332e9eee015f9a1082c8a2beb", pay.DepositAddress);
        Assert.Equal(6.875, pay.CostPercent, precision: 3); // network plus Otium's 2%
    }

    [Fact]
    public void Otiums_cut_defaults_to_the_mining_fee()
    {
        Assert.Equal(200, Service(new FakeRelay()).FeeBps);
        Assert.Equal(500, Service(new FakeRelay(), new SharingOptions { FeeBps = 500 }).FeeBps);
    }

    // ---------- through the API ----------

    private sealed class Server : IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-sharing-{Guid.NewGuid():N}.db");
        public FakeRelay Relay { get; } = new();
        public ManualTime Time { get; } = new(T0);
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Http { get; }

        public Server(params (string Key, string Value)[] settings)
        {
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
                builder.UseSetting("DisableBackgroundJobs", "true");
                builder.UseSetting("Sharing:FeeRecipient", OtiumFee);
                foreach (var (key, value) in settings)
                    builder.UseSetting(key, value);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IMoneroPoolApi>();
                    services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                    services.RemoveAll<IKryptexApi>();
                    services.AddSingleton<IKryptexApi>(new GpuPolicyTests.FakeKryptex());
                    services.RemoveAll<IPearlPipFeed>();
                    services.AddSingleton<IPearlPipFeed>(new GpuPolicyTests.FakePip());
                    services.RemoveAll<IProfitFeed>();
                    services.AddSingleton<IProfitFeed>(new GpuPolicyTests.FakeFeed());
                    services.RemoveAll<IRelayApi>();
                    services.AddSingleton<IRelayApi>(Relay);
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(Time);
                });
            });
            Http = Factory.CreateClient();
        }

        public async Task<(string Device, string Secret)> Register(string payout)
        {
            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var device = Guid.NewGuid().ToString("N");
            var response = await Http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, payout, null));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (device, secret);
        }

        public Task<SharingRoute> Route(string device, string secret, long micro, string? refund = Refund) =>
            new AccountClient(Http).GetSharingRouteAsync(device, secret, new SharingRouteRequest(micro, refund), CancellationToken.None);

        public void Dispose()
        {
            Http.Dispose();
            Factory.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                File.Delete(file);
        }
    }

    [Fact]
    public async Task Payouts_always_go_to_the_accounts_own_address_in_its_coin()
    {
        using var server = new Server();
        var usdt = TestAddresses.Usdt(3);
        var (device, secret) = await server.Register(usdt);

        var route = await server.Route(device, secret, 20_000_000);
        Assert.Equal(SharingRouteDecisions.Pay, route.Decision);
        var quote = Assert.Single(server.Relay.Quotes);
        Assert.Equal(usdt["USDT:".Length..], quote.Recipient); // the registered address, whatever the app might want
        Assert.Equal(728126428, quote.Destination.ChainId);
        Assert.Equal("0xdeposit1", route.DepositAddress);

        // Otium's 2% isn't in the conversion: only the user's part is converted, with no app fee, and the app pays
        // Otium's share to Otium's own address itself.
        Assert.Equal(19_600_000, quote.Amount);
        Assert.False(quote.WithAppFee);
        Assert.Equal(19_600_000, route.AmountMicrocredits);
        Assert.Equal(400_000, route.FeeMicrocredits);
        Assert.Equal(OtiumFee, route.FeeAddress);

        server.Time.Advance(SharingRouteService.RouteInterval);
        Assert.Equal(SharingRouteDecisions.Wait, (await server.Route(device, secret, 5_000_000)).Decision);
        Assert.Equal(SharingRouteDecisions.Wait, (await server.Route(device, secret, 500_000)).Decision);
        await Assert.ThrowsAsync<AccountRequestException>(() => server.Route(device, secret, 20_000_000, refund: null));
    }

    [Fact]
    public async Task Without_a_fee_address_there_is_no_fee()
    {
        var relay = new FakeRelay();
        var route = await Service(relay, new SharingOptions()).RouteAsync(SessionFor(TestAddresses.Usdt(1)), new SharingRouteRequest(20_000_000, Refund, 0), null, CancellationToken.None);
        Assert.Equal(SharingRouteDecisions.Pay, route.Decision);
        Assert.Equal(20_000_000, Assert.Single(relay.Quotes).Amount);
        Assert.Equal(0, route.FeeMicrocredits);
    }

    [Fact]
    public async Task Asking_again_within_five_minutes_gets_the_same_route_without_new_quotes()
    {
        using var server = new Server();
        var (device, secret) = await server.Register(TestAddresses.Usdt(3));

        var first = await server.Route(device, secret, 20_000_000);
        // A modified app asking over and over (claiming any amount) gets the same answer and costs Relay nothing more.
        for (var i = 0; i < 5; i++)
            Assert.Equal(first, await server.Route(device, secret, 50_000_000 + i));
        Assert.Single(server.Relay.Quotes);

        // A new payout address gets its own route straight away; the old one isn't handed out for it.
        var changed = await server.Http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, TestAddresses.Usdt(4), null));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var moved = await server.Route(device, secret, 20_000_000);
        Assert.Equal(2, server.Relay.Quotes.Count);
        Assert.Equal(TestAddresses.Usdt(4)["USDT:".Length..], server.Relay.Quotes[^1].Recipient);
        Assert.Equal("0xdeposit2", moved.DepositAddress);

        // Five minutes on, a new route is made.
        server.Time.Advance(SharingRouteService.RouteInterval);
        await server.Route(device, secret, 20_000_000);
        Assert.Equal(3, server.Relay.Quotes.Count);
    }

    [Fact]
    public async Task Each_internet_connection_gets_a_limited_number_of_new_routes_a_day()
    {
        var time = new ManualTime(T0);
        var relay = new FakeRelay();
        var service = Service(relay, time: time);
        var connection = System.Net.IPAddress.Parse("2001:db8:1:2::10");
        for (var i = 0; i < ConnectionQuotas.SharingRoutesPerDay; i++)
            Assert.Equal(SharingRouteDecisions.Pay,
                (await service.RouteAsync(SessionFor(TestAddresses.Usdt(1)), new SharingRouteRequest(20_000_000, Refund), connection, CancellationToken.None)).Decision);

        // Fresh accounts on the same connection (another address in its /64) get no more today, and cost no quotes.
        var quotes = relay.Quotes.Count;
        var refused = await service.RouteAsync(SessionFor(TestAddresses.Usdt(1)), new SharingRouteRequest(20_000_000, Refund),
            System.Net.IPAddress.Parse("2001:db8:1:2::99"), CancellationToken.None);
        Assert.Equal(SharingRouteDecisions.Wait, refused.Decision);
        Assert.Contains("tomorrow", refused.Message);
        Assert.Equal(quotes, relay.Quotes.Count);

        // Another connection isn't affected, and the next day the first one can ask again.
        Assert.Equal(SharingRouteDecisions.Pay, (await service.RouteAsync(SessionFor(TestAddresses.Usdt(1)), new SharingRouteRequest(20_000_000, Refund),
            System.Net.IPAddress.Parse("198.51.100.7"), CancellationToken.None)).Decision);
        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(SharingRouteDecisions.Pay, (await service.RouteAsync(SessionFor(TestAddresses.Usdt(1)), new SharingRouteRequest(20_000_000, Refund),
            connection, CancellationToken.None)).Decision);
    }

    [Theory]
    [InlineData("2001:db8::1234", "2001:db8::1234")] // IPv6 stays IPv6 (mapping it to IPv4 would make up an address)
    [InlineData("::ffff:198.51.100.7", "198.51.100.7")]
    [InlineData("198.51.100.7", "198.51.100.7")]
    public void The_users_own_address_is_passed_on_as_it_is(string seen, string passed) =>
        Assert.Equal(passed, ClientNetwork.Text(System.Net.IPAddress.Parse(seen)));

    [Fact]
    public async Task Sideshift_is_told_the_users_ipv6_address()
    {
        var sideShift = new CapturingHandler("""{"depositAddress":"0x1111111111111111111111111111111111111111","depositMin":"3"}""");
        var relay = new FakeRelay();
        var service = Service(relay, new SharingOptions { FeeRecipient = OtiumFee, SideShiftSecret = "s", SideShiftAffiliateId = "a" }, partner: sideShift);

        var route = await service.RouteAsync(SessionFor("LTC:ltc1qg42tkwuuxefutzxezdkdel39gfstuap288mfea"), new SharingRouteRequest(20_000_000, Refund),
            System.Net.IPAddress.Parse("2001:db8::1234"), CancellationToken.None);

        Assert.Equal(SharingRouteDecisions.Pay, route.Decision);
        var (request, _) = Assert.Single(sideShift.Requests);
        Assert.Equal("2001:db8::1234", request.Headers.GetValues("x-user-ip").Single());
        // Neither Relay leg carries an Otium fee: the estimate and the real deposit convert only the user's part.
        Assert.All(relay.Quotes, q => Assert.False(q.WithAppFee));
        Assert.All(relay.Quotes, q => Assert.Equal(19_600_000, q.Amount));
        Assert.Equal("0x1111111111111111111111111111111111111111", relay.Quotes[^1].Recipient);
    }

    [Fact]
    public async Task Litecoin_needs_sideshift_configured_before_it_can_be_paid()
    {
        using var server = new Server();
        var (ltcDevice, ltcSecret) = await server.Register("LTC:ltc1qg42tkwuuxefutzxezdkdel39gfstuap288mfea");
        var ltc = await server.Route(ltcDevice, ltcSecret, 20_000_000);
        Assert.Equal(SharingRouteDecisions.Unsupported, ltc.Decision);
        Assert.Contains("LTC", ltc.Message);
        Assert.Empty(server.Relay.Quotes);
    }

    // ---------- Otium's fee: exact amounts, checked on Tempo ----------

    [Fact]
    public void Each_fee_payment_on_its_way_has_an_amount_of_its_own()
    {
        var amounts = Enumerable.Range(0, 4).Select(i => Store.AddSharingFeePayment($"account{i}", 1_200_000, SharingFees.MinimumPaymentMicrocredits,
            SharingFees.MaxOffsetMicrocredits, _time.GetUtcNow())!.AmountMicro).ToList();
        Assert.Equal([1_200_001, 1_199_999, 1_200_002, 1_199_998], amounts); // never the bare fee, never twice

        // Never under Proxybase's $1 minimum, and an amount is free again once its payment arrived.
        Assert.Equal(1_000_001, Store.AddSharingFeePayment("a", 1_000_000, 1_000_000, 500, _time.GetUtcNow())!.AmountMicro);
        Assert.Equal(1_000_002, Store.AddSharingFeePayment("b", 1_000_000, 1_000_000, 500, _time.GetUtcNow())!.AmountMicro);
        Assert.True(Store.MatchSharingFeePayment(1_200_001, "0xabc:0", _time.GetUtcNow()));
        Assert.Equal(1_200_001, Store.AddSharingFeePayment("c", 1_200_000, 1_000_000, 500, _time.GetUtcNow())!.AmountMicro);
    }

    [Fact]
    public async Task Tempo_transfers_settle_the_payment_of_exactly_their_amount_and_make_used_routes_owed()
    {
        var fees = Fees();
        fees.RecordRoute("alex", "0xDEPOSIT0000000000000000000000000000000001", 400_000, _time.GetUtcNow());
        fees.RecordRoute("alex", "0xdeposit0000000000000000000000000000000002", 300_000, _time.GetUtcNow()); // never paid into
        var alex = Store.AddSharingFeePayment("alex", 1_200_000, 1_000_000, 500, _time.GetUtcNow())!;
        var sam = Store.AddSharingFeePayment("sam", 1_200_000, 1_000_000, 500, _time.GetUtcNow())!;

        _tempo.Pay("0xdeposit0000000000000000000000000000000001", 19_600_000);
        _tempo.Pay(OtiumFee, sam.AmountMicro);
        _tempo.Pay(OtiumFee, 1_200_000); // a payment nobody was asked for: matches no one
        await fees.ScanAsync(CancellationToken.None);
        await fees.ScanAsync(CancellationToken.None); // reading again changes nothing

        Assert.Equal((400_000L, 0L), Store.SharingFeeTotals("alex")); // one route went through; Alex's payment isn't there
        Assert.Equal((0L, sam.AmountMicro), Store.SharingFeeTotals("sam"));
        Assert.Equal(alex.AmountMicro, Assert.Single(Store.OpenSharingFeePayments("alex")).AmountMicro);
    }

    [Fact]
    public async Task Tempo_logs_are_read_as_transfers_to_the_recipients_asked_for()
    {
        var handler = new CapturingHandler("""
            {"jsonrpc":"2.0","id":1,"result":[{"address":"0x20c000000000000000000000b9537d11c60e8b50",
              "topics":["0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
                        "0x000000000000000000000000aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        "0x0000000000000000000000005aaeb6053f3e94c9b9a09f33669435e7ef1beaed"],
              "data":"0x0000000000000000000000000000000000000000000000000000000000124f81",
              "blockNumber":"0x10","transactionHash":"0xfeed","logIndex":"0x2"}]}
            """);
        var client = new TempoRpcClient(new HttpClient(handler), Options.Create(new SharingOptions()));
        var transfer = Assert.Single(await client.GetTransfersAsync(RelayClient.TempoUsdc, [OtiumFee], 15, 20, CancellationToken.None));
        Assert.Equal(new TempoTransfer(OtiumFee, 1_200_001, "0xfeed:2", 16), transfer);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://rpc.tempo.xyz/", request.RequestUri!.ToString());
        using var doc = JsonDocument.Parse(body);
        var filter = doc.RootElement.GetProperty("params")[0];
        Assert.Equal("eth_getLogs", doc.RootElement.GetProperty("method").GetString());
        Assert.Equal("0xf", filter.GetProperty("fromBlock").GetString());
        Assert.Equal(RelayClient.TempoUsdc, filter.GetProperty("address").GetString());
        Assert.Equal(TempoRpcClient.TransferTopic, filter.GetProperty("topics")[0].GetString());
        Assert.Equal(JsonValueKind.Null, filter.GetProperty("topics")[1].ValueKind);
        Assert.Equal("0x0000000000000000000000005aaeb6053f3e94c9b9a09f33669435e7ef1beaed", filter.GetProperty("topics")[2][0].GetString());
    }

    private Task<SharingRoute> Ask(SharingRouteService service, Session session, long available, long reserved) =>
        service.RouteAsync(session, new SharingRouteRequest(available, Refund, reserved), null, CancellationToken.None);

    [Fact]
    public async Task Payouts_wait_while_a_fee_payment_is_overdue_and_resume_once_it_arrives()
    {
        var relay = new FakeRelay();
        var service = Service(relay);
        var fees = Fees();
        var alex = SessionFor(TestAddresses.Usdt(1));
        await fees.ScanAsync(CancellationToken.None);

        // A first $20 payout: Otium's $0.40 is kept back, and Proxybase pays the rest into the conversion.
        var first = await Ask(service, alex, 20_000_000, 0);
        Assert.Equal((SharingRouteDecisions.Pay, 400_000L, 0L), (first.Decision, first.FeeMicrocredits, first.FeePaymentMicrocredits));
        _tempo.Pay(first.DepositAddress!, 19_600_000);

        // Later, with $1.20 kept back: the server names the exact amount to pay Otium now, and pays out as usual.
        _time.Advance(TimeSpan.FromMinutes(10));
        await fees.ScanAsync(CancellationToken.None);
        var second = await Ask(service, alex, 20_000_000, 1_200_000);
        Assert.Equal(SharingRouteDecisions.Pay, second.Decision);
        Assert.Equal(1_200_001, second.FeePaymentMicrocredits);
        Assert.Equal(OtiumFee, second.FeeAddress);

        // Three hours on and the payment never arrived: no new payouts, and the app is reminded what to pay.
        _time.Advance(TimeSpan.FromHours(3));
        await fees.ScanAsync(CancellationToken.None);
        var held = await Ask(service, alex, 20_000_000, 1_200_000);
        Assert.Equal(SharingRouteDecisions.Wait, held.Decision);
        Assert.Contains("hasn't arrived yet", held.Message);
        Assert.Equal(1_200_001, held.FeePaymentMicrocredits);
        var quotes = relay.Quotes.Count;

        // Once it arrives, payouts carry on.
        _tempo.Pay(OtiumFee, 1_200_001);
        await fees.ScanAsync(CancellationToken.None);
        var resumed = await Ask(service, alex, 20_000_000, 0);
        Assert.Equal(SharingRouteDecisions.Pay, resumed.Decision);
        Assert.Equal(0, resumed.FeePaymentMicrocredits);
        Assert.Equal(quotes + 1, relay.Quotes.Count);
    }

    [Fact]
    public async Task An_app_that_never_pays_is_held_once_it_owes_more_than_a_dollar_and_asked_for_it()
    {
        var service = Service(new FakeRelay());
        var fees = Fees();
        var modified = SessionFor(TestAddresses.Usdt(2));
        for (var i = 0; i < 4; i++)
        {
            await fees.ScanAsync(CancellationToken.None);
            var route = await Ask(service, modified, 20_000_000, 0); // it says it keeps nothing back
            Assert.Equal(SharingRouteDecisions.Pay, route.Decision);
            _tempo.Pay(route.DepositAddress!, 19_600_000);
            _time.Advance(TimeSpan.FromMinutes(10));
        }

        // $1.60 owed, more than $1 plus this payout's $0.40: held, and asked for the $1.60 from what's waiting.
        await fees.ScanAsync(CancellationToken.None);
        var held = await Ask(service, modified, 20_000_000, 0);
        Assert.Equal(SharingRouteDecisions.Wait, held.Decision);
        Assert.Equal(1_600_001, held.FeePaymentMicrocredits);
    }

    [Fact]
    public async Task When_tempo_cant_be_read_payouts_carry_on_unchecked()
    {
        var service = Service(new FakeRelay());
        var fees = Fees();
        var alex = SessionFor(TestAddresses.Usdt(1));
        await fees.ScanAsync(CancellationToken.None);
        Assert.Equal(1_200_001, (await Ask(service, alex, 20_000_000, 1_200_000)).FeePaymentMicrocredits);

        // The payment is overdue, but Tempo's RPC has been down since: Otium's outage shouldn't cost users their payouts.
        _tempo.Down = true;
        _time.Advance(TimeSpan.FromHours(3));
        await Assert.ThrowsAsync<HttpRequestException>(() => fees.ScanAsync(CancellationToken.None));
        Assert.Equal(SharingRouteDecisions.Pay, (await Ask(service, alex, 20_000_000, 1_200_000)).Decision);
    }

    [Fact]
    public async Task Apps_that_dont_say_what_they_keep_back_are_told_to_update_once_the_check_is_required()
    {
        var old = new SharingRouteRequest(20_000_000, Refund);
        Assert.Equal(SharingRouteDecisions.Pay, (await Service(new FakeRelay()).RouteAsync(SessionFor(TestAddresses.Usdt(1)), old, null, CancellationToken.None)).Decision);

        var required = Service(new FakeRelay(), new SharingOptions { FeeRecipient = OtiumFee, RequireFeeCheck = true });
        var refused = await required.RouteAsync(SessionFor(TestAddresses.Usdt(1)), old, null, CancellationToken.None);
        Assert.Equal(SharingRouteDecisions.Unsupported, refused.Decision);
        Assert.Contains("Update Otium", refused.Message);
    }

    [Fact]
    public async Task The_operator_can_switch_sharing_payouts_off()
    {
        using var server = new Server(("Proxybase:Enabled", "false"));
        var (device, secret) = await server.Register(TestAddresses.Usdt(4));
        Assert.Equal(SharingRouteDecisions.Unsupported, (await server.Route(device, secret, 20_000_000)).Decision);
    }
}

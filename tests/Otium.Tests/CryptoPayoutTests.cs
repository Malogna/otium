using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Wallets;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Payouts;

namespace Otium.Tests;

public class CryptoAddressTests
{
    [Theory]
    [InlineData("btc", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4")]                               // P2WPKH (BIP-173)
    [InlineData("btc", "BC1QW508D6QEJXTDG4Y5R3ZARVARY0C5XW7KV8F3T4")]                               // upper case is valid bech32
    [InlineData("btc", "bc1qrp33g0q5c5txsp9arysrx4k6zdkfs4nce4xj0gdcccefvpysxf3qccfmv3")]           // P2WSH
    [InlineData("btc", "bc1p0xlxvlhemja6c4dqv22uapctqupfhlxm9h8z3k2e72q4k9hcz7vqzk5jj0")]           // Taproot (BIP-350)
    [InlineData("btc", "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2")]                                       // P2PKH
    [InlineData("btc", "3J98t1WpEZ73CNmQviecrnyiWrnqRhWNLy")]                                       // P2SH
    [InlineData("eth", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]                               // EIP-55
    [InlineData("eth", "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359")]
    [InlineData("bnb", "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed")]                               // all lower case: no checksum
    [InlineData("sol", "So11111111111111111111111111111111111111112")]
    [InlineData("sol", "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA")]
    [InlineData("sol", "11111111111111111111111111111111")]
    public void Real_addresses_are_accepted(string method, string address)
    {
        Assert.True(CryptoAddresses.TryValidate(method, address, out var error), error);
    }

    [Theory]
    [InlineData("btc", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t5")]          // checksum typo
    [InlineData("btc", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kemeawh")]          // v0 program with a bech32m checksum
    [InlineData("btc", "bc1QW508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4")]          // mixed case
    [InlineData("btc", "tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx")]          // testnet
    [InlineData("btc", "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN3")]                  // checksum typo
    [InlineData("btc", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]          // wrong network
    [InlineData("eth", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeD")]          // EIP-55 checksum typo
    [InlineData("eth", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAe")]           // too short
    [InlineData("eth", "5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]            // no 0x
    [InlineData("bnb", "bnb1grpf0955h0ykzq3ar5nmum7y6gdfl6lxfn46h2")]          // the old BNB Beacon Chain, not BSC
    [InlineData("sol", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData("sol", "So1111111111111111111111111111111111111111O")]          // 'O' isn't base58
    [InlineData("sol", "1111")]
    [InlineData("btc", "")]
    public void Typos_and_wrong_networks_are_refused(string method, string address)
    {
        Assert.False(CryptoAddresses.TryValidate(method, address, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void A_checksum_typo_gets_a_specific_message()
    {
        CryptoAddresses.TryValidate("eth", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeD", out var error);
        Assert.Contains("typo", error);
    }

    [Theory]
    [InlineData("btc", "ab", "https://mempool.space/tx/ab")]
    [InlineData("eth", "CC3A", "https://etherscan.io/tx/0xcc3a")]
    [InlineData("bnb", "0xCC3A", "https://bscscan.com/tx/0xcc3a")]
    [InlineData("sol", "5Abc", "https://solscan.io/tx/5Abc")]
    public void Transactions_link_to_a_block_explorer(string method, string tx, string url)
    {
        Assert.Equal(url, CashoutMethods.ExplorerUrl(method, tx));
    }
}

public sealed class CryptoPayoutTests : MoneyTestBase
{
    private const string UserBtc = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4";
    private const string UserEth = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
    private const string UserBnb = "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
    private const string OutTx = "CC3A4EBCEDEB193D577F8FED233AA57A4B087B70377F71E35950935670247807";

    private readonly FakeTreasury _treasury = new();
    private readonly FakeThorchain _thorchain = new();

    private BitcoinPayoutProvider Bitcoin => new(_treasury, _thorchain, NullLogger<BitcoinPayoutProvider>.Instance);

    private ThorchainPayoutProvider Swap(string method) =>
        new(method, ThorchainPayoutProvider.Routes[method].Asset, ThorchainPayoutProvider.Routes[method].Chain,
            _treasury, _thorchain, Time, NullLogger<ThorchainPayoutProvider>.Instance);

    private IReadOnlyList<IPayoutProvider> Providers => [Bitcoin, Swap("eth"), Swap("bnb"), Swap("sol")];

    private CashoutProcessor Processor(IReadOnlyList<IPayoutProvider> providers) =>
        new(Store, providers, Receipts, Time, NullLogger<CashoutProcessor>.Instance);

    [Fact]
    public async Task Bitcoin_cashouts_share_one_transaction_at_the_pool_price()
    {
        var a = NewAccount();
        var b = NewAccount();
        Fund(a, 20m);
        Fund(b, 10m);
        var service = NewCashoutService(Providers);
        var ca = service.Request(a, new CashoutRequest("btc", UserBtc));
        var cb = service.Request(b, new CashoutRequest("btc", "bc1p0xlxvlhemja6c4dqv22uapctqupfhlxm9h8z3k2e72q4k9hcz7vqzk5jj0"));

        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        var sent = Assert.Single(_treasury.SendMany);
        Assert.Equal(0.0002m, sent[UserBtc]); // $20 at $100,000/BTC
        Assert.Equal(0.0001m, sent["bc1p0xlxvlhemja6c4dqv22uapctqupfhlxm9h8z3k2e72q4k9hcz7vqzk5jj0"]);
        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(ca.Id)!.Status);
        Assert.Equal(FakeTreasury.TxId, AccountService.ToInfo(Store.FindCashout(cb.Id)!).TransactionId);
        Assert.Contains("https://mempool.space/tx/", Outbox.Sent.First().Body);
    }

    [Fact]
    public async Task An_empty_treasury_holds_payouts_instead_of_failing_them()
    {
        var account = NewAccount();
        Fund(account, 20m);
        _treasury.Balance = 0.0001m;
        var info = NewCashoutService(Providers).Request(account, new CashoutRequest("btc", UserBtc));

        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(_treasury.SendMany);
        Assert.Equal(CashoutStatuses.Queued, Store.FindCashout(info.Id)!.Status);
        Assert.Equal(0m, Available(account)); // still reserved for the payout

        _treasury.Balance = 1m;
        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(info.Id)!.Status);
    }

    [Fact]
    public async Task Eth_is_swapped_through_thorchain_and_paid_when_it_arrives()
    {
        var account = NewAccount();
        Fund(account, 30m);
        var info = NewCashoutService(Providers).Request(account, new CashoutRequest("eth", UserEth));
        _thorchain.Status = new ThorchainSwapStatus(false, false, null);

        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        var (vault, btc, memo) = Assert.Single(_treasury.Memos);
        Assert.Equal(FakeThorchain.Vault, vault);
        Assert.Equal(0.0003m, btc);
        Assert.Equal($"=:e:{UserEth}", memo);
        Assert.Equal(("ETH.ETH", 30_000L, UserEth), _thorchain.Quotes.Single());
        var sending = Store.FindCashout(info.Id)!;
        Assert.Equal(CashoutStatuses.Sending, sending.Status);
        Assert.Equal(FakeTreasury.TxId, sending.ProviderReference);
        Assert.Empty(Outbox.Sent);

        _thorchain.Status = new ThorchainSwapStatus(true, false, OutTx);
        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        var paid = AccountService.ToInfo(Store.FindCashout(info.Id)!);
        Assert.Equal(CashoutStatuses.Paid, paid.Status);
        Assert.Equal(OutTx, paid.TransactionId);
        Assert.Contains("https://etherscan.io/tx/0xcc3a4e", Single(Outbox.Sent).Body);
        Assert.Contains("Ether (ETH) on Ethereum", Outbox.Sent.Single().Body);
    }

    [Fact]
    public async Task A_refunded_swap_puts_the_money_back_in_the_balance()
    {
        var account = NewAccount();
        Fund(account, 12m);
        var info = NewCashoutService(Providers).Request(account, new CashoutRequest("bnb", UserBnb));
        _thorchain.Status = new ThorchainSwapStatus(false, false, null);
        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        _thorchain.Status = new ThorchainSwapStatus(true, true, "abc");
        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        var failed = AccountService.ToInfo(Store.FindCashout(info.Id)!);
        Assert.Equal(CashoutStatuses.Failed, failed.Status);
        Assert.Contains("sent back", failed.Note);
        Assert.Equal(12m, Available(account));
    }

    [Fact]
    public async Task A_swap_below_thorchains_minimum_is_returned_with_the_minimum()
    {
        var account = NewAccount();
        Fund(account, 6m);
        _thorchain.RecommendedMinSats = 8_000; // $8 at $100k
        var info = NewCashoutService(Providers).Request(account, new CashoutRequest("bnb", UserBnb));

        await Processor(Providers).ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(_treasury.Memos);
        var failed = AccountService.ToInfo(Store.FindCashout(info.Id)!);
        Assert.Equal(CashoutStatuses.Failed, failed.Status);
        Assert.Contains("minimum is about $8", failed.Note);
        Assert.Equal(6m, Available(account));
    }

    [Fact]
    public async Task A_paused_coin_is_shown_as_paused_and_refused()
    {
        _thorchain.Paused["SOL"] = "paused by the swap network right now";
        var catalog = new CashoutMethodCatalog(Options.Create(Cashouts), Providers, NullLogger<CashoutMethodCatalog>.Instance);
        await catalog.RefreshAsync(CancellationToken.None);

        var sol = catalog.Find("sol")!;
        Assert.False(sol.Available);
        Assert.True(catalog.Find("eth")!.Available);
        Assert.Equal("Swapped from Bitcoin through THORChain; its swap and network fees (about $0.25–0.50) come out of the amount.", sol.FeeNote);

        var account = NewAccount();
        Fund(account, 20m);
        var service = new CashoutService(Store, Providers, Options.Create(Cashouts), catalog, Sanctions, Time, NullLogger<CashoutService>.Instance);
        var error = Assert.Throws<OtiumException>(() => service.Request(account, new CashoutRequest("sol", "So11111111111111111111111111111111111111112")));
        Assert.Equal(409, error.StatusCode);
        Assert.Contains("Solana payouts are paused", error.Message);
        Assert.Equal(20m, Available(account));
    }

    [Fact]
    public void Bad_addresses_never_debit_the_balance()
    {
        var account = NewAccount();
        Fund(account, 20m);
        var service = NewCashoutService(Providers);

        var error = Assert.Throws<OtiumException>(() => service.Request(account, new CashoutRequest("eth", "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeD")));
        Assert.Equal(400, error.StatusCode);
        Assert.Throws<OtiumException>(() => service.Request(account, new CashoutRequest("btc", UserEth)));
        Assert.Equal(20m, Available(account));
    }

    [Fact]
    public void Methods_the_server_does_not_offer_are_refused()
    {
        Cashouts.Methods = ["btc", "eth"];
        var account = NewAccount();
        Fund(account, 20m);

        var error = Assert.Throws<OtiumException>(() => NewCashoutService(Providers).Request(account, new CashoutRequest("paypal", "me@example.com")));

        Assert.Contains("Choose one of: Bitcoin, Ether", error.Message);
    }

    [Fact]
    public void Sanctioned_addresses_are_refused()
    {
        Sanctions.Load("evm", [UserEth.ToUpperInvariant().Replace("0X", "0x", StringComparison.Ordinal)]);
        var account = NewAccount();
        Fund(account, 20m);

        var error = Assert.Throws<OtiumException>(() => NewCashoutService(Providers).Request(account, new CashoutRequest("bnb", UserEth)));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal(20m, Available(account));
    }

    [Fact]
    public void Until_the_sanctions_lists_load_crypto_cashouts_wait_for_review()
    {
        var unloaded = new SanctionsList();
        var catalog = new CashoutMethodCatalog(Options.Create(Cashouts), Providers, NullLogger<CashoutMethodCatalog>.Instance);
        var service = new CashoutService(Store, Providers, Options.Create(Cashouts), catalog, unloaded, Time, NullLogger<CashoutService>.Instance);
        var account = NewAccount();
        Fund(account, 20m);

        var info = service.Request(account, new CashoutRequest("btc", UserBtc));

        Assert.Equal(CashoutStatuses.Review, info.Status);
        Assert.Contains("sanctions list not loaded", Store.FindCashout(info.Id)!.Note);
    }

    private static SentEmail Single(IReadOnlyList<SentEmail> sent) => Assert.Single(sent);

    private sealed class FakeTreasury : IBitcoinWallet
    {
        public const string TxId = "a3f1c2d4e5b6a7980112233445566778899aabbccddeeff00112233445566778";

        public decimal Balance { get; set; } = 1m;
        public List<IReadOnlyDictionary<string, decimal>> SendMany { get; } = [];
        public List<(string Address, decimal Btc, string Memo)> Memos { get; } = [];

        public bool IsConfigured => true;

        public Task<decimal> GetBalanceAsync(CancellationToken ct) => Task.FromResult(Balance);

        public Task<string> SendManyAsync(IReadOnlyDictionary<string, decimal> amountsBtc, CancellationToken ct)
        {
            SendMany.Add(amountsBtc);
            return Task.FromResult(TxId);
        }

        public Task<string> SendWithMemoAsync(string address, decimal amountBtc, string memo, CancellationToken ct)
        {
            Memos.Add((address, amountBtc, memo));
            return Task.FromResult(TxId);
        }
    }

    private sealed class FakeThorchain : IThorchainApi
    {
        public const string Vault = "bc1q704ak5tqw547nql6m65rjyaqgryq6h54d6ks2s";

        public Dictionary<string, string> Paused { get; } = [];
        public long RecommendedMinSats { get; set; } = 6_024;
        public ThorchainSwapStatus Status { get; set; } = new(true, false, OutTx);
        public List<(string Asset, long Sats, string Destination)> Quotes { get; } = [];

        public Task<decimal> GetBtcPriceUsdAsync(CancellationToken ct) => Task.FromResult(100_000m);

        public Task<string?> GetChainPauseReasonAsync(string chain, CancellationToken ct) =>
            Task.FromResult(Paused.TryGetValue(chain, out var reason) ? reason : null);

        public Task<ThorchainQuote> QuoteFromBtcAsync(string toAsset, long sats, string destination, CancellationToken ct)
        {
            Quotes.Add((toAsset, sats, destination));
            var code = toAsset == "ETH.ETH" ? "e" : "s";
            return Task.FromResult(new ThorchainQuote(Vault, $"=:{code}:{destination}", 100_000, RecommendedMinSats,
                DateTimeOffset.UtcNow.AddMinutes(10), 11_570, 606));
        }

        public Task<ThorchainSwapStatus> GetSwapStatusAsync(string inboundTxId, CancellationToken ct) => Task.FromResult(Status);
    }
}

/// <summary>The THORNode and bitcoind clients against the response shapes the real services return.</summary>
public class CryptoClientTests
{
    [Fact]
    public async Task Thorchain_quotes_prices_pauses_and_status_are_read_from_thornode()
    {
        var http = new StubHandler()
            .On("pool/BTC.BTC", """{"asset":"BTC.BTC","status":"Available","asset_tor_price":"8155142439634"}""")
            .On("inbound_addresses", """[{"chain":"BTC","halted":false,"chain_trading_paused":false,"global_trading_paused":false},{"chain":"SOL","halted":true,"chain_trading_paused":true}]""")
            .On("quote/swap", """
                {"inbound_address":"bc1q704ak5tqw547nql6m65rjyaqgryq6h54d6ks2s","fees":{"asset":"ETH.ETH","outbound":"10236","liquidity":"1334","total":"11570","total_bps":170},
                 "expiry":1792164870,"recommended_min_amount_in":"6024","memo":"=:e:0x8894E0a0c962CB723c1976a4421c95949bE2D4E3",
                 "expected_amount_out":"656024","total_swap_seconds":606}
                """)
            .On("tx/status/AAA", """
                {"tx":{"id":"AAA"},"stages":{"outbound_signed":{"completed":true}},
                 "out_txs":[{"id":"0000000000000000000000000000000000000000000000000000000000000000","chain":"THOR","memo":"OUT:AAA"},
                            {"id":"CC3A4EBCEDEB193D577F8FED233AA57A4B087B70377F71E35950935670247807","chain":"BSC","memo":"OUT:AAA"}]}
                """)
            .On("tx/status/BBB", """{"stages":{"outbound_signed":{"completed":true}},"out_txs":[{"id":"DD","chain":"BTC","memo":"REFUND:BBB"}]}""")
            .On("tx/status/CCC", """{"stages":{"inbound_observed":{"completed":false}}}""");
        var client = new ThorchainClient(new HttpClient(http), Options.Create(new ThorchainOptions { ApiBase = "https://thornode.test/" }));

        Assert.Equal(81_551.42439634m, await client.GetBtcPriceUsdAsync(default));
        Assert.Null(await client.GetChainPauseReasonAsync("BTC", default));
        Assert.NotNull(await client.GetChainPauseReasonAsync("SOL", default));
        Assert.Equal("not supported by the swap network", await client.GetChainPauseReasonAsync("XMR", default));

        var quote = await client.QuoteFromBtcAsync("ETH.ETH", 20_000, "0x8894E0a0c962CB723c1976a4421c95949bE2D4E3", default);
        Assert.Equal("bc1q704ak5tqw547nql6m65rjyaqgryq6h54d6ks2s", quote.InboundAddress);
        Assert.Equal("=:e:0x8894E0a0c962CB723c1976a4421c95949bE2D4E3", quote.Memo);
        Assert.Equal(6_024, quote.RecommendedMinSats);
        Assert.Equal(11_570, quote.TotalFee);
        Assert.Contains("amount=20000&destination=0x8894E0a0c962CB723c1976a4421c95949bE2D4E3", http.Requests.Single(r => r.Contains("quote")));
        Assert.DoesNotContain("refund_address", http.Requests.Single(r => r.Contains("quote")));

        Assert.Equal(new ThorchainSwapStatus(true, false, "CC3A4EBCEDEB193D577F8FED233AA57A4B087B70377F71E35950935670247807"),
            await client.GetSwapStatusAsync("AAA", default));
        Assert.True((await client.GetSwapStatusAsync("BBB", default)).Refunded);
        Assert.False((await client.GetSwapStatusAsync("CCC", default)).Done);
    }

    [Fact]
    public async Task Thorchain_refusals_carry_its_message()
    {
        var http = new StubHandler().On("quote/swap", """{"code":3,"message":"failed to simulate swap: trading is halted","details":[]}""", HttpStatusCode.BadRequest);
        var client = new ThorchainClient(new HttpClient(http), Options.Create(new ThorchainOptions { ApiBase = "https://thornode.test/" }));

        var error = await Assert.ThrowsAsync<ThorchainException>(() => client.QuoteFromBtcAsync("SOL.SOL", 20_000, "So11111111111111111111111111111111111111112", default));
        Assert.Contains("trading is halted", error.Message);
    }

    [Fact]
    public async Task Swap_transactions_put_the_vault_first_change_second_and_the_memo_last()
    {
        var http = new StubHandler()
            .OnRpc("createrawtransaction", "\"rawhex\"")
            .OnRpc("fundrawtransaction", """{"hex":"fundedhex","fee":0.00000300,"changepos":1}""")
            .OnRpc("signrawtransactionwithwallet", """{"hex":"signedhex","complete":true}""")
            .OnRpc("sendrawtransaction", "\"txid123\"");
        var wallet = new BitcoinWalletRpc(new BitcoinWalletOptions { RpcUrl = "http://bitcoind.test:8332", Wallet = "otium", Username = "u", Password = "p" }, http);

        var txid = await wallet.SendWithMemoAsync("bc1q704ak5tqw547nql6m65rjyaqgryq6h54d6ks2s", 0.0003m, "=:e:0xabc", default);

        Assert.Equal("txid123", txid);
        Assert.All(http.Requests, r => Assert.StartsWith("http://bitcoind.test:8332/wallet/otium", r));
        var create = http.Bodies.Single(b => b.Contains("createrawtransaction"));
        using (var doc = JsonDocument.Parse(create))
        {
            var outputs = doc.RootElement.GetProperty("params")[1];
            Assert.Equal(0.0003m, outputs[0].GetProperty("bc1q704ak5tqw547nql6m65rjyaqgryq6h54d6ks2s").GetDecimal());
            Assert.Equal(Convert.ToHexStringLower(Encoding.UTF8.GetBytes("=:e:0xabc")), outputs[1].GetProperty("data").GetString());
        }
        using (var doc = JsonDocument.Parse(http.Bodies.Single(b => b.Contains("fundrawtransaction"))))
            Assert.Equal(1, doc.RootElement.GetProperty("params")[1].GetProperty("changePosition").GetInt32());
    }

    [Fact]
    public async Task Bitcoind_errors_become_wallet_exceptions()
    {
        var http = new StubHandler().OnRpc("getbalances", null, error: "Insufficient funds", status: HttpStatusCode.InternalServerError);
        var wallet = new BitcoinWalletRpc(new BitcoinWalletOptions { RpcUrl = "http://bitcoind.test:8332" }, http);

        var error = await Assert.ThrowsAsync<BitcoinWalletException>(() => wallet.GetBalanceAsync(default));
        Assert.Equal("Insufficient funds", error.Message);
    }

    [Fact]
    public async Task Memos_longer_than_bitcoin_allows_are_refused_before_anything_is_built()
    {
        var http = new StubHandler();
        var wallet = new BitcoinWalletRpc(new BitcoinWalletOptions { RpcUrl = "http://bitcoind.test:8332" }, http);

        await Assert.ThrowsAsync<BitcoinWalletException>(() => wallet.SendWithMemoAsync("bc1q", 0.001m, new string('x', 81), default));
        Assert.Empty(http.Requests);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly List<(string Match, string? Body, HttpStatusCode Status, bool Rpc, string? Error)> _routes = [];

        public List<string> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        public StubHandler On(string urlFragment, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _routes.Add((urlFragment, body, status, false, null));
            return this;
        }

        public StubHandler OnRpc(string method, string? result, string? error = null, HttpStatusCode status = HttpStatusCode.OK)
        {
            _routes.Add((method, result, status, true, error));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(url);
            Bodies.Add(body);
            foreach (var (match, result, status, rpc, error) in _routes)
            {
                if (rpc && body.Contains($"\"method\":\"{match}\"", StringComparison.Ordinal))
                {
                    var json = error is null
                        ? $$"""{"result":{{result}},"error":null,"id":"otium"}"""
                        : $$"""{"result":null,"error":{"code":-6,"message":"{{error}}"},"id":"otium"}""";
                    return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }
                if (!rpc && url.Contains(match, StringComparison.Ordinal))
                    return new HttpResponseMessage(status) { Content = new StringContent(result!, Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}

using System.Text.Json;
using Otium.Core.Pools;
using Otium.Core.Wallets;

namespace Otium.Tests;

public class PayoutTargetTests
{
    private const string Monero = "44AFFq5kSiGBoZ4NMDwYtN18obc8AemS33DBLWs3H7otXft3XjrpDtQGv7SqSsaBYBb98uNbr2VBBEt7f2wfn3RVGQBEP3A";

    [Theory]
    [InlineData(AddressFormat.Tron, "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t")]                                  // the USDT contract
    [InlineData(AddressFormat.Tron, "TLa2f6VPqDgRE67v1736s7bJ8Ray5wYjU7")]
    [InlineData(AddressFormat.Litecoin, "LM2WMpR1Rp6j3Sa59cMXMs1SPzj9eXpGc1")]                              // P2PKH
    [InlineData(AddressFormat.Litecoin, "MNNqZ7ZZMsWRUrCEmkAm1SH9JyFFi8CJKq")]                              // P2SH
    [InlineData(AddressFormat.Litecoin, "3GAhFE9bQkezgLvLfsBRBo2jzGeohWEYTA")]                              // old-style P2SH
    [InlineData(AddressFormat.Litecoin, "ltc1qnmxapll3j6kxtu3nvutcaecqkgsz7h0dtn4pt8")]                     // P2WPKH
    [InlineData(AddressFormat.Litecoin, "ltc1q94c3vs4hy6cygqtz0j5lhtpj7hy9xra3jq7vfkczykr30ys6fzqsc9egat")] // P2WSH
    [InlineData(AddressFormat.Litecoin, "ltc1p94c3vs4hy6cygqtz0j5lhtpj7hy9xra3jq7vfkczykr30ys6fzqsjjep9h")] // Taproot
    public void Tron_and_litecoin_addresses_are_accepted(AddressFormat format, string address)
    {
        Assert.True(CryptoAddresses.TryValidate(format, address, out var error), error);
    }

    [Theory]
    [InlineData(AddressFormat.Tron, "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6u")]                    // checksum typo
    [InlineData(AddressFormat.Tron, "LZhdatxywWafqyv4iuW83Bja3xjNEfT2EJ")]                    // a Litecoin address
    [InlineData(AddressFormat.Tron, "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData(AddressFormat.Litecoin, "LZhdatxywWafqyv4iuW83Bja3xjNEfT2EK")]                // checksum typo
    [InlineData(AddressFormat.Litecoin, "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2")]                // a Bitcoin P2PKH address
    [InlineData(AddressFormat.Litecoin, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4")]        // a Bitcoin SegWit address
    [InlineData(AddressFormat.Litecoin, "ltc1qnmxapll3j6kxtu3nvutcaecqkgsz7h0d709dw9")]       // v0 with a bech32m checksum
    [InlineData(AddressFormat.Bitcoin, "ltc1qnmxapll3j6kxtu3nvutcaecqkgsz7h0dtn4pt8")]
    public void Wrong_networks_and_typos_are_refused(AddressFormat format, string address)
    {
        Assert.False(CryptoAddresses.TryValidate(format, address, out _));
    }

    [Fact]
    public void Targets_read_and_write_as_coin_and_address()
    {
        Assert.True(PayoutTarget.TryParse(" usdt:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t ", out var target, out var error), error);
        Assert.Same(PayoutCoins.Usdt, target.Coin);
        Assert.Equal("USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", target.ToString());

        // Case-insensitive addresses are stored lower case, so the same wallet always compares equal.
        var evm = PayoutTarget.Create(PayoutCoins.Bnb, "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
        Assert.Equal("BNB:0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed", evm.ToString());
        Assert.True(PayoutTarget.SameAs("bnb:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", evm.ToString()));
        Assert.False(PayoutTarget.SameAs("ETH:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", evm.ToString()));
        Assert.Equal(evm, PayoutTarget.ParseOrNull("BNB:0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"));
    }

    [Fact]
    public void A_bare_monero_address_from_older_settings_reads_as_xmr()
    {
        Assert.True(PayoutTarget.TryParse(Monero, out var target, out _));
        Assert.Same(PayoutCoins.Monero, target.Coin);
        Assert.Equal($"XMR:{Monero}", PayoutTarget.Normalize(Monero));
    }

    [Theory]
    [InlineData("", "Enter your address.")]
    [InlineData("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", "Choose which coin")]                  // no coin
    [InlineData("DOGE:DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L", "can't pay out in DOGE")]
    [InlineData("USDT:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "on TRON (TRC-20)")]     // USDT is offered on TRON only
    [InlineData("ETH:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeD", "typo")]                  // EIP-55 checksum
    public void Bad_targets_say_what_to_fix(string text, string expected)
    {
        Assert.False(PayoutTarget.TryParse(text, out _, out var error));
        Assert.Contains(expected, error);
    }

    [Fact]
    public void The_default_coin_is_usdt_on_tron_with_a_small_threshold()
    {
        Assert.Same(PayoutCoins.Usdt, PayoutCoins.Default);
        Assert.Equal("1.5 USDT", PayoutCoins.Default.ThresholdText);
        Assert.Equal("Tether (USDT) on TRON (TRC-20)", PayoutCoins.Default.Label);
        Assert.Same(PayoutCoins.Litecoin, PayoutCoins.Find("ltc"));
        Assert.Null(PayoutCoins.Find("DOGE"));
    }
}

/// <summary>Shapes captured from unMineable's live v5 API (October 2026).</summary>
public class UnmineableClientTests
{
    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data").Clone();

    [Fact]
    public void Address_reads_balance_threshold_and_auto_pay()
    {
        var address = UnmineableClient.ParseAddress(Data("""
            {"success":true,"msg":"Ok","data":{"balance":"0.138709","balance_payable":"0.138709","precision":"6",
            "payment_threshold":"1.5","mining_fee":"1","auto":false,"enabled":true,"maintenance_message":null,
            "enabled_auto_only":false,"uuid":"6aabf502-d955-4790-b488-8283269f9829","mining_key":"1346688","fresh":false,
            "delisted_chain":false,"address":"TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t","network":"TRX",
            "err_flags":{"payment_error":true,"missing_memo":false,"not_activated":false,"restricted":false},"chains":[]}}
            """));

        Assert.Equal("6aabf502-d955-4790-b488-8283269f9829", address.Uuid);
        Assert.Equal(0.138709m, address.Balance);
        Assert.Equal(1.5m, address.Threshold);
        Assert.False(address.AutoPay);
        Assert.True(address.Enabled);
        Assert.Equal("TRX", address.Network);
        Assert.Contains(address.Problems, p => p.Contains("payment", StringComparison.OrdinalIgnoreCase));
        Assert.Null(address.Maintenance);
    }

    [Fact]
    public void Stats_read_rewards_and_the_last_payment()
    {
        var stats = UnmineableClient.ParseStats(Data("""
            {"success":true,"msg":"Ok","data":{"balance_mining":"0.138709","balance_referral":"0.02","balance":"0.158709",
            "rewarded":{"past_24h":"0.5","past_7d":"3.1","past_30d":"12"},"paid":"53.056042","last_payment":1751833695000,
            "payment_threshold":"1.5","mining_fee":"1","coin":"USDT","network":"TRX","mining_key":"1346688"}}
            """));

        Assert.Equal("USDT", stats.Coin);
        Assert.Equal(0.02m, stats.ReferralBalance);
        Assert.Equal(0.158709m, stats.Balance);
        Assert.Equal(0.5m, stats.Rewarded24h);
        Assert.Equal(12m, stats.Rewarded30d);
        Assert.Equal(53.056042m, stats.Paid);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1751833695000), stats.LastPayment);
    }

    [Fact]
    public void Payments_are_newest_first_with_explorer_links()
    {
        var payments = UnmineableClient.ParsePayments(Data("""
            {"success":true,"message":"Ok","data":{"explorer":"https://tronscan.org/#/transaction/","coin":"USDT","list":[
            {"tx":"8b39","amount":"1.756013","timestamp":1716426078000,"status":"success","id":5208044,"network":"TRX",
             "explorer_link":"https://tronscan.org/#/transaction/8b39"},
            {"tx":"15b0","amount":"1.573443","timestamp":1751833695000,"status":"success","id":6066581,"network":"TRX",
             "explorer_link":"https://tronscan.org/#/transaction/15b0"}],
            "has_next":true,"has_prev":false,"last_page":3,"count":25}}
            """));

        Assert.Equal(2, payments.Count);
        Assert.Equal(1.573443m, payments[0].Amount);
        Assert.Equal("https://tronscan.org/#/transaction/15b0", payments[0].ExplorerUrl);
        Assert.Equal("TRX", payments[1].Network);
    }

    [Fact]
    public void Workers_are_flattened_across_algorithms()
    {
        var workers = UnmineableClient.ParseWorkers(Data("""
            {"success":true,"msg":"Ok","data":{
            "randomx":{"workers":[{"uuid":"76ac16b0-1d14-4279-b1d6-f68a6f8ce785","online":true,"name":"otiumspikecpu",
                       "last":1792251900000,"rhr":"194","chr":"194.44","referral":"ztjq-jr7s"}],
                       "chart":{"reported":{"data":["138.89","194.44"],"timestamps":[1792251600000,1792251900000]},
                                "calculated":{"data":["138.89","194.44"],"timestamps":[1792251600000,1792251900000]}}},
            "pearlpow":{"workers":[{"uuid":"1bc04a5f-89d0-4c46-8fd7-810cb5aa2770","online":false,"name":"otiumspikegpu",
                       "last":1792251900000,"rhr":"5003999585967","chr":"5003999585967","referral":null}],"chart":{}},
            "kawpow":{"workers":[],"chart":{}}}}
            """));

        Assert.Equal(2, workers.Count);
        var cpu = workers[0];
        Assert.Equal("randomx", cpu.Algorithm);
        Assert.Equal("otiumspikecpu", cpu.Name);
        Assert.True(cpu.Online);
        Assert.Equal(194.44, cpu.CalculatedHps);
        Assert.Equal("ztjq-jr7s", cpu.Referral);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792251900000), cpu.LastSeen);
        Assert.False(workers[1].Online);
        Assert.Null(workers[1].Referral);
        Assert.Equal(5_003_999_585_967, workers[1].CalculatedHps);
    }

    [Fact]
    public void The_dashboard_link_names_the_coin()
    {
        var target = PayoutTarget.Create(PayoutCoins.Usdt, "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t");
        Assert.Equal("https://unmineable.com/address/TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t?coin=USDT",
            UnmineableClient.DashboardUrl(target).ToString());
    }
}

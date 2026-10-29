using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Providers.Aioz;

namespace Otium.Tests;

/// <summary>
/// Sharing storage and upload bandwidth through AIOZ's DePIN node: the pinned 1.2.5 node per platform, its own local
/// port, its balance read in AIOZ and shown in dollars, withdrawals to the user's address, and the storage limit Otium
/// enforces because 1.2.5 has none.
/// </summary>
public class AiozTests
{
    [Theory]
    [InlineData("WINDOWS", Architecture.X64, "aioz-depin-windows-amd64-1.2.5.zip", "aioz-depin-cli-windows-amd64.exe")]
    [InlineData("LINUX", Architecture.X64, "aioz-depin-linux-amd64-1.2.5.tar.gz", "aioz-depin-cli-linux-amd64")]
    [InlineData("OSX", Architecture.Arm64, null, null)] // 1.2.5's Mac archives don't say which is for Apple silicon
    [InlineData("OSX", Architecture.X64, null, null)]
    [InlineData("WINDOWS", Architecture.Arm64, null, null)]
    [InlineData("LINUX", Architecture.Arm64, null, null)]
    public void Each_platform_gets_the_pinned_node(string os, Architecture arch, string? file, string? binary)
    {
        var asset = AiozRelease.For(OSPlatform.Create(os), arch);
        Assert.Equal(file, asset?.FileName);
        Assert.Equal(binary, asset?.BinaryName);
        if (asset is not null)
        {
            Assert.Matches("^[0-9a-f]{64}$", asset.Sha256);
            Assert.Equal($"https://github.com/AIOZNetwork/aioz-depin-cli/releases/download/v1.2.5/{file}", asset.DownloadUrl.ToString());
        }
    }

    [Fact]
    public void The_node_listens_on_its_own_loopback_port_and_keeps_its_files_in_otiums_folder()
    {
        var args = AiozBandwidthProvider.StartArguments("/data/home", "/data/privkey.json", 41234);
        Assert.Equal("start", args[0]);
        Assert.Contains("--laddr=tcp://127.0.0.1:41234", args); // not AIOZ's default 1317, often reserved on Windows
        Assert.Contains("--home=/data/home", args); // or it would use ~/.aiozworker
        Assert.Contains("--priv-key-file=/data/privkey.json", args);
        Assert.Contains("--no-auto-update", args); // Otium pins the version
    }

    [Fact]
    public void Withdrawals_go_to_the_users_address_in_whole_aioz()
    {
        var args = AiozBandwidthProvider.WithdrawArguments("/data/home", "/data/privkey.json", 41234, "0xabc", 7m);
        Assert.Equal(["reward", "withdraw"], args.Take(2));
        Assert.Contains("--address=0xabc", args);
        Assert.Contains("--amount=7aioz", args);
        Assert.Contains("--endpoint=http://127.0.0.1:41234", args);
        Assert.Contains("--home=/data/home", args);
    }

    [Fact]
    public void The_balance_is_read_in_aioz_from_its_smallest_unit()
    {
        var balance = AiozBandwidthProvider.ParseBalance("""
            {
              "balance": [{"denom": "attoaioz", "amount": "2500000000000000000"}],
              "storage_earned": [{"denom": "attoaioz", "amount": "3000000000000000000"}],
              "delivery_earned": [{"denom": "attoaioz", "amount": "500000000000000000"}, {"denom": "other", "amount": "9"}],
              "transcoding_earned": [],
              "withdraw": [{"denom": "attoaioz", "amount": "1000000000000000000"}],
              "delivery_counter": 3
            }
            """);
        Assert.Equal(2.5m, balance.Waiting);
        Assert.Equal(3.5m, balance.Earned);
        Assert.Equal(1m, balance.Withdrawn);

        // A brand-new node: every list empty.
        var empty = AiozBandwidthProvider.ParseBalance("""{"balance":[],"storage_earned":[],"delivery_earned":[],"transcoding_earned":[],"withdraw":[],"delivery_counter":0}""");
        Assert.Equal(0m, empty.Earned);
    }

    [Fact]
    public void Stats_say_what_the_node_stores()
    {
        var stats = AiozBandwidthProvider.ParseStats("""{"storage":{"total_count":4,"total_size":311320},"delivery":{"upstream_speed":0},"transcoding":{"status":"Standby"}}""");
        Assert.Equal(4, stats.StoredFiles);
        Assert.Equal("4 files stored for AIOZ (304 KB)", AiozBandwidthProvider.ActivityText(stats));
        Assert.Equal("Waiting for AIOZ to send files", AiozBandwidthProvider.ActivityText(stats with { StoredFiles = 0 }));
    }

    [Fact]
    public void Earnings_turn_into_dollars_at_todays_price()
    {
        var start = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var earned = AiozEarned.Empty.With(start, 1.0m, null);
        Assert.Null(earned.UserUsd); // no price yet

        earned = earned.With(start.AddHours(12), 3.0m, 0.25);
        Assert.Equal(3.0 * 0.25, earned.UserUsd!.Value, 9);
        Assert.Equal(2.0 * 0.25 / 12 * 24, earned.UserUsdPerDay(start.AddHours(12))!.Value, 9);

        var restored = JsonSerializer.Deserialize<AiozEarned>(JsonSerializer.Serialize(earned))!;
        Assert.Equal(earned.UserUsd, restored.UserUsd);
    }

    [Fact]
    public async Task The_node_pauses_once_its_files_reach_the_users_storage_limit()
    {
        var root = Directory.CreateTempSubdirectory("otium-aioz-").FullName;
        try
        {
            var data = Directory.CreateDirectory(Path.Combine(root, "aioz-data", "home", "data")).FullName;
            await using (var file = File.Create(Path.Combine(data, "piece")))
                file.SetLength(AiozBandwidthProvider.LimitBytes(1) + 1);

            await using var provider = new AiozBandwidthProvider(new HttpClient(), root, () => "0xabc", () => 1,
                AiozRelease.For(OSPlatform.Windows, Architecture.X64));
            // Not started, and no node needed: the limit is checked first.
            await provider.StartAsync(new BandwidthRunOptions("worker"), CancellationToken.None);
            var status = await provider.GetStatusAsync(CancellationToken.None);
            Assert.Equal(BandwidthProviderState.Error, status.State);
            Assert.Contains("1 GB storage limit", status.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void It_runs_without_an_0x_address_and_keeps_aioz_on_this_computer_until_there_is_one()
    {
        // One click, like the other networks: no converter takes AIOZ on its own network, so with a non-0x payout coin
        // the AIOZ waits in the node's own key instead of blocking the switch.
        var provider = new AiozBandwidthProvider(new HttpClient(), Path.GetTempPath(), () => null, () => 20,
            AiozRelease.For(OSPlatform.Windows, Architecture.X64));
        Assert.Null(provider.UnavailableReason);
        Assert.Equal("aioz", provider.Id);

        var noFee = AiozFee.StartAt(0);
        var little = new AiozBalance(1.5m, 1.5m, 0, 0, 0);
        Assert.EndsWith("1.5 AIOZ waiting on this computer, kept safe until there's enough to send.",
            AiozBandwidthProvider.EarningsText(little, noFee, 0, hasAddress: false, null));
        var enough = little with { Waiting = 6, Storage = 6 };
        Assert.EndsWith("Add an 0x address in Advanced settings to receive it.", AiozBandwidthProvider.EarningsText(enough, noFee, 0, hasAddress: false, null));
        Assert.Contains("sent to your 0x address on the AIOZ network once it reaches 5 AIOZ", AiozBandwidthProvider.EarningsText(enough, noFee, 0, hasAddress: true, null));
    }

    [Fact]
    public void Otiums_share_builds_up_only_on_what_the_node_earns_after_it_starts()
    {
        // The first reading after the update: 3 AIOZ earned before stays fee-free.
        var fee = AiozFee.StartAt(3m).Accrue(3m, 2);
        Assert.Equal(0m, fee.Owed);

        fee = fee.Accrue(13m, 2); // 10 AIOZ more: 2% of it is Otium's
        Assert.Equal(0.2m, fee.Owed);
        Assert.Equal(0.2m, fee.Kept);
        Assert.Equal(13m, fee.ChargedThrough);

        Assert.Equal(fee, fee.Accrue(13m, 2)); // nothing new, nothing added
        Assert.Equal(fee, fee.Accrue(12m, 2)); // a lower reading never charges twice later on
        Assert.Equal(0.2m + 0.5m, fee.Accrue(18m, 99).Owed); // capped at the built-in most (10%)

        // Without a fee address the share is 0, and those earnings are counted as fee-free.
        var free = fee.Accrue(20m, 0);
        Assert.Equal(0.2m, free.Owed);
        Assert.Equal(20m, free.ChargedThrough);

        var restored = JsonSerializer.Deserialize<AiozFee>(JsonSerializer.Serialize(fee))!;
        Assert.Equal(fee, restored);
    }

    [Fact]
    public void The_users_payout_leaves_otiums_share_in_the_node_and_otium_is_paid_on_a_day_of_its_own()
    {
        const string user = "0xuser", otium = "0xotium";

        // 6.3 waiting, 0.3 of it Otium's: the user gets 6 whole AIOZ, Otium's share stays behind.
        var fee = new AiozFee(100m, Owed: 0.3m, Kept: 0.3m);
        Assert.Equal(new AiozWithdrawal(user, 6m, ToOtium: false), AiozBandwidthProvider.NextWithdrawal(6.3m, fee, user, otium));

        // Not enough for the user yet, and under a whole AIOZ owed: nothing.
        Assert.Null(AiozBandwidthProvider.NextWithdrawal(2.3m, fee, user, otium));

        // A whole AIOZ owed and no payout for the user due: Otium's share goes to Otium's address.
        var owed = fee with { Owed = 1.4m };
        Assert.Equal(new AiozWithdrawal(otium, 1m, ToOtium: true), AiozBandwidthProvider.NextWithdrawal(3m, owed, user, otium));
        // The user comes first when both are due.
        Assert.False(AiozBandwidthProvider.NextWithdrawal(7m, owed, user, otium)!.ToOtium);
        // Never more than the node holds, and nothing to Otium without its address.
        Assert.Null(AiozBandwidthProvider.NextWithdrawal(0.5m, owed, user, otium));
        Assert.Null(AiozBandwidthProvider.NextWithdrawal(3m, owed, user, feeAddress: null));
        // Without a user address Otium's share can still go.
        Assert.True(AiozBandwidthProvider.NextWithdrawal(3m, owed, userAddress: null, otium)!.ToOtium);

        // Only what reached Otium stops being owed.
        Assert.Equal(0.4m, owed.Sent(1m).Owed);
        Assert.Equal(0m, owed.Sent(5m).Owed);
        Assert.Equal(owed.Kept, owed.Sent(1m).Kept); // sending doesn't change what the user has earned

        var args = AiozBandwidthProvider.WithdrawArguments("/data/home", "/data/privkey.json", 41234, otium, 1m);
        Assert.Contains("--address=0xotium", args);
        Assert.Contains("--amount=1aioz", args);
    }

    [Fact]
    public void The_user_sees_earnings_and_waiting_without_otiums_share()
    {
        var balance = new AiozBalance(Waiting: 4.3m, Storage: 15m, Delivery: 0, Transcoding: 0, Withdrawn: 10m);
        var fee = new AiozFee(15m, Owed: 0.3m, Kept: 0.3m);
        var text = AiozBandwidthProvider.EarningsText(balance, fee, 2, hasAddress: true, null);
        Assert.StartsWith("14.7 AIOZ earned in total after Otium's 2%; 4 AIOZ waiting", text);
    }
}

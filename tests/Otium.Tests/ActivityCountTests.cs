using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Tests;

/// <summary>
/// The admin dashboard's users and computers: each computer and account counts once per UTC day it's seen, mining
/// counts when the reported mining time grows, and only day totals are kept (nothing per computer by day).
/// </summary>
public sealed class ActivityCountTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = Day1.AddDays(1);
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"otium-activity-{Guid.NewGuid():N}.db");
    private readonly OtiumStore _store;

    public ActivityCountTests() => _store = new OtiumStore($"Data Source={_db};Pooling=False");

    [Fact]
    public void Computers_and_accounts_count_once_a_day()
    {
        _store.Upsert(new DeviceRecord("d1", "h", "acc1", Day1, Day1));
        _store.Upsert(new DeviceRecord("d2", "h", "acc1", Day1, Day1)); // a second computer on the same account
        _store.TouchDevice("d1", Day1.AddHours(1), TimeSpan.FromMinutes(5)); // checks in again the same day

        _store.TouchDevice("d1", Day2, TimeSpan.FromMinutes(5), "0.1.1");
        _store.TouchDevice("d2", Day2.AddMinutes(10), TimeSpan.FromMinutes(5));

        var days = _store.DailyActivity(Day2, 2);
        Assert.Equal(new OtiumStore.DayActivity(DateOnly.FromDateTime(Day1.UtcDateTime), 2, 1, 0, 0, 2, 0), days[0]);
        Assert.Equal(new OtiumStore.DayActivity(DateOnly.FromDateTime(Day2.UtcDateTime), 2, 1, 0, 0, 0, 0), days[1]);
        Assert.Equal(1, _store.AccountsSeenSince(Day2));
        Assert.Equal((2, 2), _store.Retention(Day1.AddHours(1), Day2));
        Assert.Equal([("0.1.1", 1L), (null, 1L)], _store.AppVersionsSince(Day2));
    }

    [Fact]
    public void A_computer_counts_as_mining_once_its_reported_time_grows()
    {
        _store.Upsert(new DeviceRecord("d1", "h", "acc1", Day1, Day1));
        _store.RecordMiningTime("d1", 3600, 0, Day1); // the first report only sets where it starts
        Assert.Equal((0, 0, 0), _store.DevicesMiningSince(Day1));

        _store.RecordMiningTime("d1", 3660, 0, Day1.AddMinutes(1));
        _store.RecordMiningTime("d1", 3720, 0, Day1.AddMinutes(2)); // still once for the day
        _store.RecordMiningTime("d1", 3720, 0, Day1.AddMinutes(3)); // stopped: the time didn't grow

        Assert.Equal((1, 0, 1), _store.DevicesMiningSince(Day1));
        Assert.Equal((0, 0, 0), _store.DevicesMiningSince(Day1.AddMinutes(10)));
        var today = _store.DailyActivity(Day1, 1)[0];
        Assert.Equal((1, 0), (today.CpuMining, today.GpuMining));
    }

    [Fact]
    public void Test_accounts_add_nothing_to_the_day_totals()
    {
        _store.Upsert(new AccountRecord("acc1", "w1", null, null, "code1", null, null, null, null, false, null, null, Day1));
        Assert.True(_store.SetTestAccount("acc1", true));
        _store.Upsert(new DeviceRecord("d1", "h", "acc1", Day1, Day1));
        _store.RecordMiningTime("d1", 3600, 0, Day1);
        _store.RecordMiningTime("d1", 3660, 0, Day1.AddMinutes(1));
        _store.TouchDevice("d1", Day2, TimeSpan.FromMinutes(5));

        Assert.All(_store.DailyActivity(Day2, 2), d => Assert.Equal((0, 0, 0, 0), (d.Devices, d.Accounts, d.CpuMining, d.NewDevices)));
        Assert.Equal(0, _store.DeviceCount());
        Assert.Equal((0, 0, 0), _store.DevicesMiningSince(Day1));
    }

    [Theory]
    [InlineData("USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ", "USDT:TB2xNF…y4wJ")]
    [InlineData("4AdUndXHHZ6cfufTMvppY6JwXNouMBzSkbLYfpAV5Usx3skxNgYeYTRj5UzqtReoS44qo9mtmXCqY45DJ852K5Jv2684Rge", "XMR:4AdUnd…4Rge")]
    [InlineData(null, null)]
    public void Payout_addresses_are_shortened_for_the_list(string? wallet, string? shown) =>
        Assert.Equal(shown, OtiumStore.ShortPayout(wallet));

    [Theory]
    [InlineData("0.1.1+3aeeb18", "0.1.1")]
    [InlineData("0.1.2-rc.1", "0.1.2-rc.1")]
    [InlineData("not a version", null)]
    [InlineData("", null)]
    public void Only_real_version_numbers_are_kept(string sent, string? kept) =>
        Assert.Equal(kept, AccountService.CleanVersion(sent));

    public void Dispose()
    {
        foreach (var file in new[] { _db, _db + "-wal", _db + "-shm" })
            File.Delete(file);
    }
}

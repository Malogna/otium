using Otium.Core.Gpu;
using Otium.Core.Platform;
using Otium.Core.Settings;
using Otium.Core.Switching;

namespace Otium.Tests;

/// <summary>Closing the user's tuning programs while the card earns, protecting a hot card, and the readings shown on its row.</summary>
public class TuningAndThermalTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"otium-tools-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    private sealed class FakeProcesses : IToolProcesses
    {
        public List<RunningTool> Running { get; } = [];
        public HashSet<string> WontClose { get; } = [];
        public List<string> Started { get; } = [];

        public IReadOnlyList<RunningTool> FindRunning() => [.. Running];

        public Task<bool> CloseAsync(RunningTool tool, TimeSpan wait, CancellationToken ct)
        {
            if (WontClose.Contains(tool.Name))
                return Task.FromResult(false);
            Running.Remove(tool);
            return Task.FromResult(true);
        }

        public bool Start(string exePath)
        {
            Started.Add(exePath);
            return true;
        }
    }

    private TuningToolsGuard Guard(FakeProcesses processes) => new(processes, new JsonFileStore<TuningToolsState>(_file));

    private static RunningTool Afterburner(string? path = @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe") =>
        new("MSI Afterburner", "MSIAfterburner", 100, path);

    [Fact]
    public async Task Running_tuning_programs_are_closed_remembered_and_started_again_afterwards()
    {
        var processes = new FakeProcesses { Running = { Afterburner() } };
        var guard = Guard(processes);

        var result = await guard.PauseAsync(CancellationToken.None);

        Assert.Equal(["MSI Afterburner"], result.Closed);
        Assert.Empty(result.CouldNot);
        Assert.Empty(processes.Running);
        Assert.Equal(@"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe", Assert.Single(guard.Closed).ExePath);

        Assert.Equal(["MSI Afterburner"], guard.Restore());
        Assert.Equal([@"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe"], processes.Started);
        Assert.Empty(guard.Closed);
    }

    [Fact]
    public async Task What_was_closed_survives_a_restart_of_otium_so_the_overclock_still_comes_back()
    {
        var first = new FakeProcesses { Running = { Afterburner() } };
        await Guard(first).PauseAsync(CancellationToken.None);

        var after = new FakeProcesses(); // Otium started again; the program is still closed
        Assert.Equal(["MSI Afterburner"], Guard(after).Restore());
        Assert.Single(after.Started);
    }

    [Fact]
    public async Task A_program_that_wont_say_where_it_starts_from_is_left_alone_because_it_could_not_be_brought_back()
    {
        var processes = new FakeProcesses { Running = { Afterburner(path: null) } };
        var guard = Guard(processes);

        var result = await guard.PauseAsync(CancellationToken.None);

        Assert.Empty(result.Closed);
        Assert.Equal(["MSI Afterburner"], result.CouldNot);
        Assert.Single(processes.Running); // still running: nothing was closed
        Assert.Empty(guard.Closed);
    }

    [Fact]
    public async Task A_program_that_does_not_close_is_not_remembered_as_closed()
    {
        var processes = new FakeProcesses { Running = { Afterburner() }, WontClose = { "MSI Afterburner" } };
        var guard = Guard(processes);

        var result = await guard.PauseAsync(CancellationToken.None);

        Assert.Equal(["MSI Afterburner"], result.CouldNot);
        Assert.Empty(guard.Closed); // so it isn't started a second time later
    }

    [Fact]
    public async Task A_program_the_user_started_again_themselves_is_not_started_twice()
    {
        var processes = new FakeProcesses { Running = { Afterburner() } };
        var guard = Guard(processes);
        await guard.PauseAsync(CancellationToken.None);

        processes.Running.Add(Afterburner()); // the user opened it again before Otium did

        Assert.Empty(guard.Restore());
        Assert.Empty(processes.Started);
    }

    [Fact]
    public async Task Programs_that_are_not_tuning_tools_are_never_touched()
    {
        Assert.Null(TuningToolCatalog.ByProcess("chrome"));
        Assert.Null(TuningToolCatalog.ByProcess("Discord"));
        Assert.Equal("MSI Afterburner", TuningToolCatalog.ByProcess("msiafterburner")?.Name);
        Assert.Equal("ASUS GPU Tweak", TuningToolCatalog.ByProcess("GPUTweakII")?.Name);
        var nothing = new FakeProcesses();
        Assert.Empty((await Guard(nothing).PauseAsync(CancellationToken.None)).Closed);
    }

    // ---------- overheating ----------

    private static GpuStats Card(double temp, double fan, double load = 100, GpuThrottle throttle = GpuThrottle.None) =>
        new("NVIDIA GeForce RTX 3080", temp, load, fan, 300, 1800, 2, 12, Throttle: throttle);

    [Theory]
    [InlineData(70, 60, null)]
    [InlineData(89, 70, null)]
    [InlineData(90, 80, "RTX 3080 reached 90 °C")]
    [InlineData(95, 100, "RTX 3080 reached 95 °C")]
    [InlineData(84, 0, "RTX 3080's fan isn't spinning while it is at 84 °C")]
    [InlineData(60, 0, null)]   // a cold card may have its fan stopped on purpose
    public void A_card_is_stopped_at_90_degrees_or_when_its_fan_stands_still_while_warm(double temp, double fan, string? expected) =>
        Assert.Equal(expected, ThermalGuard.StopReason([Card(temp, fan)]));

    [Fact]
    public void A_fan_reading_of_zero_on_an_idle_warm_card_is_not_a_fault() =>
        Assert.Null(ThermalGuard.StopReason([Card(84, 0, load: 5)]));

    [Fact]
    public void A_card_counts_as_cooled_down_at_80_degrees_or_below()
    {
        Assert.False(ThermalGuard.CooledDown([Card(81, 50)]));
        Assert.True(ThermalGuard.CooledDown([Card(80, 50)]));
        Assert.False(ThermalGuard.CooledDown([Card(70, 50), Card(85, 50)]));
        Assert.True(ThermalGuard.CooledDown([new GpuStats("x", null, null, null, null, null, null, null)])); // no reading: nothing to wait for
    }

    [Theory]
    [InlineData(10, 0, 70.0, false)]
    [InlineData(10, 3, 70.0, true)]   // 30% of the readings throttled by heat
    [InlineData(10, 2, 70.0, false)]
    [InlineData(10, 0, 88.0, true)]   // never flagged as throttled, but nearly 90 °C
    [InlineData(2, 2, 90.0, false)]   // too few readings to judge
    public void A_test_counts_as_run_too_hot_when_much_of_it_was_throttled_by_heat_or_it_ended_near_90(int readings, int throttled, double peak, bool hot) =>
        Assert.Equal(hot, ThermalGuard.TestWasThrottled(readings, throttled, peak));

    [Fact]
    public void A_test_measured_on_a_hot_card_stands_for_two_hours_then_is_measured_again()
    {
        var measured = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var hot = new BenchmarkResult("pearlhash", 32e12, 110, measured, Throttled: true);
        var fine = new BenchmarkResult("pearlhash", 105e12, 110, measured);

        Assert.True(hot.IsCurrent(measured.AddHours(1)));
        Assert.False(hot.IsCurrent(measured.AddHours(2)));
        Assert.True(fine.IsCurrent(measured.AddDays(20)));
    }

    // ---------- the readings on the row ----------

    [Fact]
    public void The_cards_readings_become_lines_with_the_trouble_marked()
    {
        var card = new GpuStats("RTX 3080", 91, 100, 0, 245, 900, 1.3, 12, PowerLimitWatts: 245, MemoryClockMhz: 9501, MaxClockMhz: 2115,
            Throttle: GpuThrottle.SoftwareThermal | GpuThrottle.PowerCap);

        var lines = card.ToLines().ToDictionary(l => l.Label);

        Assert.Equal("91 °C", lines["Temperature"].Value);
        Assert.Equal(StatLevel.Bad, lines["Temperature"].Level);
        Assert.Equal(StatLevel.Bad, lines["Fan"].Level); // not spinning while hot
        Assert.Equal("245 W of 245 W limit", lines["Power"].Value);
        Assert.Equal("900 of 2,115 MHz", lines["Core clock"].Value);
        Assert.Equal("9,501 MHz", lines["Memory clock"].Value);
        Assert.Equal("too hot, at its power limit", lines["Speed limited"].Value);
        Assert.True(card.IsThermalThrottled);
    }

    [Fact]
    public void A_healthy_card_shows_no_warnings_and_no_speed_limit_line_when_nothing_holds_it_back()
    {
        var lines = new GpuStats("RTX 3080", 68, 98, 45, 240, 1900, 2, 12, 245, 9501, 2115, GpuThrottle.Idle).ToLines();

        Assert.All(lines, l => Assert.Equal(StatLevel.Normal, l.Level));
        Assert.DoesNotContain(lines, l => l.Label == "Speed limited");
    }

    [Fact]
    public void A_processors_readings_show_its_load_and_only_the_temperature_it_can_read()
    {
        Assert.Equal(["Load (all programs)"], new CpuStats(31, null).ToLines().Select(l => l.Label));
        Assert.Equal(["Load (all programs)", "Temperature"], new CpuStats(31, 62).ToLines().Select(l => l.Label));
        Assert.Empty(new CpuStats(null, null).ToLines());
    }
}

using Otium.Core.Providers;

namespace Otium.Core.Bandwidth;

/// <summary>
/// Dev mode stand-in for a bandwidth partner SDK: comes online after a moment and relays bursty, made-up traffic
/// (average set by OTIUM_BANDWIDTH_SIM_KBPS, default 40 KB/s). Nothing leaves the computer.
/// </summary>
public sealed class SimulatedBandwidthProvider(TimeProvider? time = null) : IBandwidthProvider
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Random _random = new();
    private readonly double _averageBytesPerSecond = 1024 * (double.TryParse(
        Environment.GetEnvironmentVariable("OTIUM_BANDWIDTH_SIM_KBPS"), System.Globalization.CultureInfo.InvariantCulture, out var kbps) && kbps > 0 ? kbps : 40);

    private DateTimeOffset? _startedAt;
    private DateTimeOffset _lastSample;
    private double _bytes;

    public string Id => "simulated";

    public string DisplayName => "Simulated bandwidth partner";

    public string? UnavailableReason => null;

    public bool IsInstalled => true;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

    public Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
    {
        _startedAt ??= _time.GetUtcNow();
        _lastSample = _startedAt.Value;
        _bytes = 0;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _startedAt = null;
        return Task.CompletedTask;
    }

    public Task<BandwidthStatus> GetStatusAsync(CancellationToken ct)
    {
        if (_startedAt is not { } started)
            return Task.FromResult(new BandwidthStatus(BandwidthProviderState.Stopped, 0));

        var now = _time.GetUtcNow();
        if (now - started < TimeSpan.FromSeconds(3))
            return Task.FromResult(new BandwidthStatus(BandwidthProviderState.Starting, 0));

        // Demand comes in bursts: some intervals carry several times the average, many carry nothing.
        var elapsed = (now - _lastSample).TotalSeconds;
        _lastSample = now;
        if (_random.NextDouble() < 0.6)
            _bytes += elapsed * _averageBytesPerSecond / 0.6 * (0.5 + _random.NextDouble());
        return Task.FromResult(new BandwidthStatus(BandwidthProviderState.Online, (long)_bytes));
    }

    public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

    public Task OptOutAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

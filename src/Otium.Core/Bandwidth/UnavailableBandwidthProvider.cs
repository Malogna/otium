using Otium.Core.Providers;

namespace Otium.Core.Bandwidth;

/// <summary>Used where no bandwidth partner SDK can run (unsupported OS, or a build without the partner's component).</summary>
public sealed class UnavailableBandwidthProvider(string reason) : IBandwidthProvider
{
    public string Id => "none";

    public string DisplayName => "Internet sharing";

    public string? UnavailableReason => reason;

    public bool IsInstalled => false;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

    public Task StartAsync(BandwidthRunOptions options, CancellationToken ct) => throw new InvalidOperationException(reason);

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<BandwidthStatus> GetStatusAsync(CancellationToken ct) =>
        Task.FromResult(new BandwidthStatus(BandwidthProviderState.Stopped, 0, reason));

    public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

    public Task OptOutAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

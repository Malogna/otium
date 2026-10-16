using Otium.Core.Bandwidth;
using Otium.Core.Providers;

namespace Otium.Providers.Honeygain;

/// <summary>
/// Internet sharing through the Honeygain SDK, running inside Otium as the publisher app. Honeygain pays Otium per
/// active device (weighted by uptime and country) and reports no traffic volume to the app, so this provider reports
/// time online rather than bytes, and the server credits users per sharing hour.
/// Consent comes from Otium's own explanation screen; the SDK is told with opt-in and never shares without it.
/// </summary>
public sealed class HoneygainBandwidthProvider : IBandwidthProvider
{
    /// <summary>After asking the SDK to start, how long "not running yet" still counts as starting.</summary>
    private static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(45);

    private readonly IHoneygainSdk? _sdk;
    private readonly string? _apiKey;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;
    private DateTimeOffset? _startRequestedAt;

    public HoneygainBandwidthProvider(string libraryDirectory, string? apiKey, TimeProvider? time = null)
        : this(apiKey is null ? null : HoneygainNative.TryLoad(libraryDirectory), apiKey, time)
    {
    }

    internal HoneygainBandwidthProvider(IHoneygainSdk? sdk, string? apiKey, TimeProvider? time = null)
    {
        _sdk = sdk;
        _apiKey = apiKey;
        _time = time ?? TimeProvider.System;
    }

    public string Id => "honeygain";

    public string DisplayName => "Honeygain";

    public string? UnavailableReason =>
        _sdk is null || _apiKey is null ? "Internet sharing isn't available in this version yet." : null;

    /// <summary>The SDK ships with the app; there is nothing to download.</summary>
    public bool IsInstalled => _sdk is not null;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

    public async Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sdk = EnsureInitialized();
            Check(sdk.Start(out var started), "start");
            if (!started)
                throw new InvalidOperationException("Internet sharing needs your consent first. Turn it on again in Advanced settings.");
            _startRequestedAt = _time.GetUtcNow();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _startRequestedAt = null;
            if (_initialized)
                _sdk!.Stop();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BandwidthStatus> GetStatusAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_initialized || _startRequestedAt is not { } requested)
                return new BandwidthStatus(BandwidthProviderState.Stopped, 0);
            if (_sdk!.IsRunning(out var running) != 0)
                return new BandwidthStatus(BandwidthProviderState.Error, 0, "The sharing component isn't responding. Retrying…");
            if (running)
                return new BandwidthStatus(BandwidthProviderState.Online, 0);
            return _time.GetUtcNow() - requested < StartGrace
                ? new BandwidthStatus(BandwidthProviderState.Starting, 0)
                : new BandwidthStatus(BandwidthProviderState.Error, 0, "The sharing component stopped. Retrying…");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task OptInAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (UnavailableReason is null)
                Check(EnsureInitialized().OptIn(), "record consent");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task OptOutAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _startRequestedAt = null;
            if (UnavailableReason is null)
                Check(EnsureInitialized().OptOut(), "withdraw consent"); // also stops sharing
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Honeygain's id for this computer, for matching its publisher reports.</summary>
    public string? PartnerDeviceId => _initialized ? _sdk!.Identify() : null;

    private IHoneygainSdk EnsureInitialized()
    {
        if (UnavailableReason is { } reason)
            throw new InvalidOperationException(reason);
        if (!_initialized)
        {
            Check(_sdk!.Init(_apiKey!), "initialize");
            _sdk.Mute(); // no log files in the user's working directory
            _initialized = true;
        }
        return _sdk!;
    }

    private static void Check(int result, string action)
    {
        if (result != 0)
            throw new InvalidOperationException($"The sharing component couldn't {action} (error {result}).");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _sdk?.Dispose();
        _lock.Dispose();
    }
}

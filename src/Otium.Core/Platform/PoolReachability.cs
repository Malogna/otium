using System.Net.Sockets;

namespace Otium.Core.Platform;

/// <summary>Opens a TCP connection to see whether a host can be reached from this computer.</summary>
public interface IConnectionProbe
{
    Task<bool> CanConnectAsync(string host, int port, CancellationToken ct);
}

public sealed class TcpProbe : IConnectionProbe
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    public async Task<bool> CanConnectAsync(string host, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }
}

/// <summary>
/// When a miner can't reach its pool, tells apart "the internet is down" from "something on this computer blocks the
/// pool" (antivirus web protection such as Malwarebytes flags mining pools as threats), and says which security product
/// is active so the user knows where to allow it. Answers are kept for a few minutes per pool.
/// </summary>
public sealed class PoolReachability(IConnectionProbe probe, Func<CancellationToken, Task<IReadOnlyList<string>>> activeSecurityProducts, TimeProvider? time = null)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    /// <summary>A host that's practically always reachable, to check the internet itself works.</summary>
    public const string ControlHost = "api.github.com";

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Dictionary<string, (DateTimeOffset At, string? Reason)> _cache = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Why the pool can't be reached, in plain words with what to do, or null when it can be reached (or the whole
    /// internet is down, which the miner already reports as connecting).
    /// </summary>
    public async Task<string?> BlockedReasonAsync(string host, int port, CancellationToken ct)
    {
        var key = $"{host}:{port}";
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out var cached) && _time.GetUtcNow() - cached.At < CacheFor)
                return cached.Reason;
            var reason = await CheckAsync(host, port, ct);
            _cache[key] = (_time.GetUtcNow(), reason);
            return reason;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> CheckAsync(string host, int port, CancellationToken ct)
    {
        if (await probe.CanConnectAsync(host, port, ct) || !await probe.CanConnectAsync(ControlHost, 443, ct))
            return null;
        var site = SiteOf(host);
        var products = await activeSecurityProducts(ct);
        var where = products.Count switch
        {
            0 => "That's usually antivirus or firewall web protection, or a network filter",
            1 => $"That's usually antivirus web protection: {products[0]} is active here",
            _ => $"That's usually antivirus web protection: {string.Join(" and ", products)} are active here",
        };
        return $"something on this computer blocks the mining pool ({site}). {where}. Add {site} to its allowed websites (its exclusions or allow list), and Otium carries on by itself";
    }

    /// <summary>"pearlpow.unmineable.com" → "unmineable.com": the site the user allows.</summary>
    public static string SiteOf(string host)
    {
        var labels = host.Trim('.').Split('.');
        return labels.Length <= 2 ? host : string.Join('.', labels[^2..]);
    }
}

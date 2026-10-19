using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Otium.Server;

/// <summary>
/// One key per internet connection: the IPv4 address, or the IPv6 /64 network. A home or a phone gets a whole /64 and
/// can pick a new address in it at will, so a limit per IPv6 address would be no limit at all.
/// </summary>
public static class ClientNetwork
{
    public static string? Of(IPAddress? address)
    {
        if (address is null)
            return null;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? Convert.ToHexStringLower(address.GetAddressBytes()[..8]) + "/64"
            : address.ToString();
    }
}

/// <summary>
/// Allowances per internet connection for what anyone can ask for and costs Otium something: emails sent through SES,
/// new accounts and problem reports on disk. Devices are free to create, so limits per device alone don't hold. Counted
/// in memory only: no address is written down, and a restart starts the counts over.
/// </summary>
public sealed class ConnectionQuotas(TimeProvider time)
{
    public const int NewDevicesPerDay = 30;
    public const int CodeEmailsPerHour = 10;
    public const int CodeEmailsPerDay = 30;
    public const int ProblemReportsPerDay = 20;

    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private DateTimeOffset _nextSweep = DateTimeOffset.MinValue;

    private sealed class Window(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; set; } = start;
        public int Count { get; set; }
    }

    /// <summary>
    /// Takes one from <paramref name="what"/>'s allowance for this connection, or returns false when it's used up for the
    /// current window. An unknown address (no connection information, as in tests) isn't limited.
    /// </summary>
    public bool TryTake(string what, IPAddress? address, int limit, TimeSpan window)
    {
        if (ClientNetwork.Of(address) is not { } network)
            return true;
        var now = time.GetUtcNow();
        Sweep(now);
        var w = _windows.GetOrAdd($"{what}|{network}", _ => new Window(now));
        lock (w)
        {
            if (now - w.Start >= window)
            {
                w.Start = now;
                w.Count = 0;
            }
            if (w.Count >= limit)
                return false;
            w.Count++;
            return true;
        }
    }

    /// <summary>Forgets windows older than a day now and then, so the table doesn't grow forever.</summary>
    private void Sweep(DateTimeOffset now)
    {
        if (now < _nextSweep)
            return;
        _nextSweep = now + TimeSpan.FromHours(1);
        foreach (var (key, w) in _windows)
        {
            if (now - w.Start > TimeSpan.FromDays(1))
                _windows.TryRemove(key, out _);
        }
    }
}

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

    /// <summary>
    /// The address itself, for a partner that wants the user's IP (SideShift's x-user-ip): IPv4 as itself, also when it
    /// arrived IPv4-mapped, and IPv6 in full. (Mapping a real IPv6 address to IPv4 would make up an unrelated address.)
    /// </summary>
    public static string? Text(IPAddress? address) =>
        address is null ? null : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}

/// <summary>
/// Allowances per internet connection for what anyone can ask for and costs Otium something: emails sent through SES,
/// new accounts, problem reports on disk and partners' conversion quotas. Devices are free to create, so limits per device alone don't hold. Counted
/// in memory only: no address is written down, and a restart starts the counts over.
/// </summary>
public sealed class ConnectionQuotas(TimeProvider time)
{
    public const int NewDevicesPerDay = 30;
    public const int CodeEmailsPerHour = 10;
    public const int CodeEmailsPerDay = 30;
    public const int ProblemReportsPerDay = 20;

    /// <summary>
    /// New sharing payout routes (each costs conversion quotes, and for LTC or XMR a real shift or exchange at a partner).
    /// An app asks at most every 10 minutes while something waits to be paid, about 144 times a day, so this leaves room
    /// for two computers asking all day on one connection.
    /// </summary>
    public const int SharingRoutesPerDay = 300;

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

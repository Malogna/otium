using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Otium.Server.Data;

namespace Otium.Server.Accounts;

/// <summary>
/// Invite links that apply themselves. When someone opens a friend's otium.lol/r/CODE page, the code is remembered for
/// their internet connection for a week; Otium installed on that connection asks for it on its first start and fills it
/// in, so a friend who never types the code still joins through the invite. The connection is kept only as a keyed hash
/// (HMAC-SHA256 with a random key that never leaves the server), of the IPv4 address or the IPv6 /64 network (devices
/// change the rest of an IPv6 address every day or so). Only someone opening the page counts (not an image or script on
/// another site loading it), and the first link opened on a connection wins: otherwise any web page could stamp its
/// author's code on every visitor's connection, whole universities behind one address included. The app shows the code
/// on the welcome screen to keep or clear; it's never used unseen.
/// </summary>
public sealed class InviteLinks(OtiumStore store, TimeProvider time)
{
    /// <summary>How long a click is remembered: as long as a new install can still use an invite code.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private readonly Lazy<byte[]> _key = new(() => Convert.FromBase64String(store.ServerSecret("invite-links")));

    /// <summary>Someone on <paramref name="address"/> opened the invite page for <paramref name="code"/> (an existing code).</summary>
    public void Clicked(IPAddress? address, string code)
    {
        if (Network(address) is not { } network)
            return;
        var now = time.GetUtcNow();
        store.DeleteInviteClicksBefore(now - KeepFor);
        store.RecordInviteClick(network, code, now); // unless another link is still pending there
    }

    /// <summary>
    /// Whether the request is someone opening the page in their browser: browsers say what a request is for
    /// (Sec-Fetch-Dest, Sec-Fetch-Mode), and an &lt;img&gt; or fetch on another site isn't a "document" "navigate".
    /// Requests without those headers (older browsers, tools) still count.
    /// </summary>
    public static bool IsPageVisit(IHeaderDictionary headers) =>
        (!headers.TryGetValue("Sec-Fetch-Dest", out var dest) || string.Equals(dest.ToString(), "document", StringComparison.OrdinalIgnoreCase))
        && (!headers.TryGetValue("Sec-Fetch-Mode", out var mode) || string.Equals(mode.ToString(), "navigate", StringComparison.OrdinalIgnoreCase));

    /// <summary>The code of the invite link opened on this connection in the last week, while it still belongs to someone.</summary>
    public string? PendingFor(IPAddress? address) =>
        Network(address) is { } network && store.InviteClickCode(network, time.GetUtcNow() - KeepFor) is { } code && store.CodeExists(code)
            ? code
            : null;

    private string? Network(IPAddress? address)
    {
        if (address is null)
            return null;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            bytes = bytes[..8];
        return Convert.ToHexString(HMACSHA256.HashData(_key.Value, bytes));
    }
}

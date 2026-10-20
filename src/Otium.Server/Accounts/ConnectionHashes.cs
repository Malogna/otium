using System.Net;
using System.Security.Cryptography;
using System.Text;
using Otium.Server.Data;

namespace Otium.Server.Accounts;

/// <summary>
/// An internet connection (an IPv4 address, or an IPv6 /64; see <see cref="ClientNetwork"/>) as a keyed one-way hash,
/// so the server can tell that two computers share a connection without keeping the address. The key is random, made
/// once and kept in the database.
/// </summary>
public sealed class ConnectionHashes(OtiumStore store)
{
    private readonly Lazy<byte[]> _key = new(() => Convert.FromBase64String(store.ServerSecret("connections")));

    public string? Of(IPAddress? address) =>
        ClientNetwork.Of(address) is { } network ? Convert.ToHexString(HMACSHA256.HashData(_key.Value, Encoding.UTF8.GetBytes(network))) : null;
}

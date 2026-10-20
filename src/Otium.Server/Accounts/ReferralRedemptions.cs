using System.Security.Cryptography;
using System.Text;
using Otium.Core.Wallets;
using Otium.Server.Data;

namespace Otium.Server.Accounts;

/// <summary>
/// The payout addresses and computers that already joined through an invite, as keyed one-way hashes (HMAC-SHA256 with
/// a random key that never leaves the server). They're kept after the account is deleted: otherwise deleting an account
/// and joining again with any code would give the 30 fee-free days (and the inviter theirs) over and over.
/// </summary>
public sealed class ReferralRedemptions(OtiumStore store)
{
    private readonly Lazy<byte[]> _key = new(() => Convert.FromBase64String(store.ServerSecret("referral-redemptions")));

    /// <summary>
    /// A stored wallet by its address alone: the same address under another coin (TRX and USDT on TRON, BNB and ETH on
    /// 0x addresses) is the same wallet, and its owner the same person.
    /// </summary>
    public static string Address(string storedWallet) => PayoutTarget.ParseOrNull(storedWallet)?.Address ?? storedWallet.Trim();

    /// <summary>Whether two stored wallets pay the same address, whatever coin each names.</summary>
    public static bool SameAddress(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Address(a), Address(b), StringComparison.Ordinal);

    public string OfWallet(string storedWallet) => Hash("wallet:" + Address(storedWallet));

    /// <summary>The app's hardware id (a hash of the computer's machine id, see DeviceIdentity.HardwareId).</summary>
    public string OfHardware(string hardwareId) => Hash("hardware:" + hardwareId.Trim().ToLowerInvariant());

    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(_key.Value, Encoding.UTF8.GetBytes(value)));
}

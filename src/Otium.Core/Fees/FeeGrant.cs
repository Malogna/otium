using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Otium.Core.Fees;

/// <summary>
/// What the Otium server says about one computer's fee, signed by the server: the fee, fee-free days, who invited the
/// user (whose slice the app mines), and for custodial balance accounts the pool wallet. Release builds take these only
/// from a valid grant for their own device that hasn't expired, so editing settings.json, pointing the app at another
/// server or cutting it off from otium.lol leads to the full fee, not none.
/// </summary>
/// <param name="ReferrerWallet">The inviter's payout target, or null.</param>
/// <param name="PoolWallet">Custodial balance accounts only: the wallet the server credits from; null otherwise.</param>
/// <param name="ExpiresAt">After this the grant is ignored: a computer cut off from otium.lol for long pays the full fee.</param>
/// <param name="CpuMiningFloor">
/// Seconds of processor mining this computer has reported to the server so far (the most it ever reported). The fee
/// slice comes at the end of each cycle of mining time, so release builds never count less than this: deleting or
/// editing the mining record on the computer doesn't move it back before Otium's slice.
/// </param>
/// <param name="GpuMiningFloor">The same for the graphics card.</param>
public sealed record FeeGrant(
    string DeviceId,
    double FeePercent,
    double ReferrerShareOfFee,
    string? ReferrerWallet,
    DateTimeOffset? ReferredAt,
    DateTimeOffset? FeeFreeUntil,
    bool Custodial,
    string? PoolWallet,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    double CpuMiningFloor = 0,
    double GpuMiningFloor = 0)
{
    public const string Header = "otium-fee-grant 2";

    /// <summary>How long a grant counts without a fresh one from the server.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>The signed text: one field per line, in a fixed order and format.</summary>
    public string Payload() => string.Join('\n',
        Header,
        DeviceId,
        FeePercent.ToString("R", CultureInfo.InvariantCulture),
        ReferrerShareOfFee.ToString("R", CultureInfo.InvariantCulture),
        ReferrerWallet ?? "-",
        Date(ReferredAt),
        Date(FeeFreeUntil),
        Custodial ? "custodial" : "own-wallet",
        PoolWallet ?? "-",
        Date(IssuedAt),
        Date(ExpiresAt),
        Seconds(CpuMiningFloor),
        Seconds(GpuMiningFloor));

    public static FeeGrant? Parse(string? payload)
    {
        var l = (payload ?? "").Split('\n');
        if (l.Length != 13 || l[0] != Header || l[1].Length == 0)
            return null;
        if (!double.TryParse(l[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fee)
            || !double.TryParse(l[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var share)
            || l[7] is not ("custodial" or "own-wallet")
            || ParseDate(l[9]) is not { } issued || ParseDate(l[10]) is not { } expires
            || ParseSeconds(l[11]) is not { } cpuFloor || ParseSeconds(l[12]) is not { } gpuFloor)
            return null;
        return new FeeGrant(l[1], fee, share, Text(l[4]), ParseDate(l[5]), ParseDate(l[6]), l[7] == "custodial", Text(l[8]), issued, expires,
            cpuFloor, gpuFloor);
    }

    private static string Seconds(double s) => s.ToString("R", CultureInfo.InvariantCulture);

    private static double? ParseSeconds(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) && v >= 0 ? v : null;

    private static string Date(DateTimeOffset? d) => d?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "-";

    private static DateTimeOffset? ParseDate(string s) =>
        DateTimeOffset.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;

    private static string? Text(string s) => s == "-" ? null : s;
}

/// <summary>Signs fee grants (the server) and checks them (the app), with ECDSA P-256 over the grant's payload.</summary>
public static class FeeGrants
{
    public static string Sign(FeeGrant grant, ECDsa key) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(grant.Payload()), HashAlgorithmName.SHA256));

    /// <summary>
    /// The grant in <paramref name="payload"/> when one of <paramref name="trustedKeys"/> (base64 SubjectPublicKeyInfo)
    /// signed it, it's for <paramref name="deviceId"/> and it hasn't expired (nor claims to come from the future); else null.
    /// </summary>
    public static FeeGrant? Verify(string? payload, string? signature, IEnumerable<string> trustedKeys, string deviceId, DateTimeOffset now)
    {
        if (FeeGrant.Parse(payload) is not { } grant || grant.DeviceId != deviceId)
            return null;
        if (grant.ExpiresAt <= now || grant.IssuedAt > now + TimeSpan.FromHours(1) || grant.ExpiresAt - grant.IssuedAt > FeeGrant.Lifetime + TimeSpan.FromHours(1))
            return null;
        byte[] signed;
        try
        {
            signed = Convert.FromBase64String(signature ?? "");
        }
        catch (FormatException)
        {
            return null;
        }
        var data = Encoding.UTF8.GetBytes(payload!);
        foreach (var trusted in trustedKeys)
        {
            using var key = ECDsa.Create();
            try
            {
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(trusted), out _);
            }
            catch (Exception e) when (e is FormatException or CryptographicException)
            {
                continue;
            }
            if (key.VerifyData(data, signed, HashAlgorithmName.SHA256))
                return grant;
        }
        return null;
    }
}

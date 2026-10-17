using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Gpu;

/// <summary>
/// Decides where each balance account's CPU mines: MoneroOcean (paid in XMR to the operator's wallet) or Kryptex's
/// XMR pool under the operator's Kryptex account (auto-exchanged to BTC). A stable share of accounts, set by
/// <see cref="CpuMiningOptions.KryptexPercent"/>, goes to Kryptex, so a move can start with a small canary and grow to
/// everyone without an app release. Own-wallet accounts always mine to their own address and get no route.
/// </summary>
public sealed class CpuPoolRouter(IOptions<CpuMiningOptions> cpuOptions, IOptions<KryptexOptions> kryptexOptions)
{
    /// <summary>MoneroOcean's TLS port, the same pool and port the app uses by default.</summary>
    public const string MoneroOceanHost = "gulf.moneroocean.stream";
    public const int MoneroOceanTlsPort = 20128;

    public CpuPoolRoute? For(AccountRecord account)
    {
        if (!account.IsBalanceAccount || string.IsNullOrEmpty(account.WorkerId))
            return null;

        var kryptex = kryptexOptions.Value;
        if (OnKryptex(account.WorkerId) && kryptex is { Enabled: true, Account: { Length: > 0 } kryptexAccount, Cpu.Host.Length: > 0 })
        {
            return new CpuPoolRoute(CpuPoolRoute.Kryptex, kryptex.Cpu.Host, kryptex.Cpu.Port, kryptex.Cpu.Tls,
                $"{kryptexAccount.Trim()}.{account.WorkerId}", "x");
        }

        return account.PoolWallet is { Length: > 0 } wallet
            ? new CpuPoolRoute(CpuPoolRoute.MoneroOcean, MoneroOceanHost, MoneroOceanTlsPort, true, wallet, account.WorkerId)
            : null;
    }

    /// <summary>Whether this worker falls in the share of accounts routed to Kryptex; stable for a given worker id.</summary>
    public bool OnKryptex(string workerId)
    {
        var percent = Math.Clamp(cpuOptions.Value.KryptexPercent, 0, 100);
        return percent > 0 && Bucket(workerId) < percent;
    }

    /// <summary>0–99, the same for a worker id on every server and every run.</summary>
    internal static int Bucket(string workerId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(workerId));
        return (int)(BitConverter.ToUInt32(hash, 0) % 100);
    }
}

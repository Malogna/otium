using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Bandwidth;

/// <summary>Sends MYST on Polygon from Otium's own small wallet.</summary>
public interface IMystWallet
{
    bool IsConfigured { get; }

    /// <summary>Sends <paramref name="amountMyst"/> to <paramref name="to"/> and returns the transaction hash once mined.</summary>
    Task<string> SendAsync(string to, decimal amountMyst, CancellationToken ct);

    /// <summary>The wallet's MYST balance, for the admin dashboard.</summary>
    Task<decimal> GetBalanceAsync(CancellationToken ct);

    /// <summary>The wallet's address, so the operator knows where to send MYST and POL; null without a key.</summary>
    string? Address => null;

    /// <summary>The wallet's POL balance (gas for each transfer), for the admin dashboard; null when unknown.</summary>
    Task<decimal?> GetGasBalanceAsync(CancellationToken ct) => Task.FromResult<decimal?>(null);
}

/// <summary>An ERC-20 transfer of MYST on Polygon, signed with the sponsor key from <see cref="MysteriumOptions"/>.</summary>
public sealed class PolygonMystWallet(IOptions<MysteriumOptions> options) : IMystWallet
{
    private static readonly BigInteger WeiPerMyst = BigInteger.Pow(10, 18);
    private readonly MysteriumOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SponsorPrivateKey) && !string.IsNullOrWhiteSpace(_options.PolygonRpcUrl);

    private Web3 Connect() => new(new Account(_options.SponsorPrivateKey!.Trim(), _options.ChainId), _options.PolygonRpcUrl);

    public string? Address => IsConfigured ? new Account(_options.SponsorPrivateKey!.Trim(), _options.ChainId).Address : null;

    public async Task<decimal?> GetGasBalanceAsync(CancellationToken ct)
    {
        var web3 = Connect();
        var wei = await web3.Eth.GetBalance.SendRequestAsync(web3.TransactionManager.Account.Address);
        return (decimal)(wei.Value * 1_000_000 / WeiPerMyst) / 1_000_000m; // POL has 18 decimals too
    }

    public async Task<string> SendAsync(string to, decimal amountMyst, CancellationToken ct)
    {
        var token = Connect().Eth.ERC20.GetContractService(_options.MystTokenAddress);
        var wei = new BigInteger(decimal.Round(amountMyst * 1_000_000m)) * WeiPerMyst / 1_000_000;
        var receipt = await token.TransferRequestAndWaitForReceiptAsync(to, wei, ct);
        if (receipt.Status?.Value != 1)
            throw new InvalidOperationException($"The MYST transfer {receipt.TransactionHash} failed on chain.");
        return receipt.TransactionHash;
    }

    public async Task<decimal> GetBalanceAsync(CancellationToken ct)
    {
        var web3 = Connect();
        var wei = await web3.Eth.ERC20.GetContractService(_options.MystTokenAddress)
            .BalanceOfQueryAsync(web3.TransactionManager.Account.Address);
        return (decimal)(wei * 1_000_000 / WeiPerMyst) / 1_000_000m;
    }
}

/// <summary>
/// Pays Mysterium's one-time registration fee for users' nodes: about 0.13 MYST (a cent or so) sent to the node's
/// channel address, after which the node registers itself with the user's own wallet as beneficiary. The money is
/// Otium's, not the user's, so this keeps Otium non-custodial. Limits keep a script from draining the wallet: each
/// node identity and computer once, a few per account and per internet connection a day, and a daily budget.
/// </summary>
public sealed partial class MystSponsorService(OtiumStore store, IMystWallet wallet, IOptions<MysteriumOptions> options, TimeProvider time, ILogger<MystSponsorService> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsAvailable => options.Value.Enabled && wallet.IsConfigured;

    public async Task<MystSponsorResult> SponsorAsync(Session session, MystSponsorRequest request, string ip, CancellationToken ct)
    {
        var o = options.Value;
        if (!IsAvailable)
            return Unavailable("Internet sharing through Mysterium isn't available from this Otium server yet.");
        if (!EvmAddress().IsMatch(request.Identity ?? "") || !EvmAddress().IsMatch(request.ChannelAddress ?? ""))
            throw new OtiumException(400, "That isn't a Mysterium node identity and channel address.");

        var identity = request.Identity!.ToLowerInvariant();
        var channel = request.ChannelAddress!.ToLowerInvariant();
        await _lock.WaitAsync(ct);
        try
        {
            if (store.FindMystSponsorship(identity) is { } existing)
                return new MystSponsorResult(MystSponsorStatuses.AlreadySent, existing.TransactionId, existing.AmountMyst);

            var now = time.GetUtcNow();
            var dayAgo = now - TimeSpan.FromDays(1);
            if (store.MystSponsorshipsForDevice(session.Device.DeviceId) > 0)
                return Unavailable("Otium already paid Mysterium's registration for this computer once.");
            if (store.MystSponsorshipsForAccount(session.Account.AccountId) >= o.MaxPerAccount)
                return Unavailable($"Otium pays Mysterium's registration for up to {o.MaxPerAccount} computers per account.");
            if (store.MystSponsorshipsFromIpSince(ip, dayAgo) >= o.MaxPerIpPerDay)
                return Unavailable("Too many new Mysterium nodes from this internet connection today. Retrying tomorrow.");
            if (store.MystSponsoredSince(dayAgo) + o.AmountMyst > o.DailyBudgetMyst)
            {
                logger.LogWarning("Mysterium sponsorship budget of {Budget} MYST for the last 24 hours is used up", o.DailyBudgetMyst);
                return Unavailable("Otium can't cover more Mysterium registrations today. Retrying tomorrow.");
            }

            string tx;
            try
            {
                tx = await wallet.SendAsync(channel, o.AmountMyst, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Sending the Mysterium registration fee to {Channel} failed", channel);
                return Unavailable("Otium couldn't send Mysterium's registration fee just now. Retrying later.");
            }
            store.AddMystSponsorship(new MystSponsorshipRecord(identity, channel, session.Account.AccountId, session.Device.DeviceId, ip, o.AmountMyst, tx, now));
            logger.LogInformation("Paid Mysterium registration for {Identity}: {Amount} MYST in {Tx}", identity, o.AmountMyst, tx);
            return new MystSponsorResult(MystSponsorStatuses.Sent, tx, o.AmountMyst);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static MystSponsorResult Unavailable(string message) => new(MystSponsorStatuses.Unavailable, null, 0, message);

    [GeneratedRegex("^0x[0-9a-fA-F]{40}$")]
    private static partial Regex EvmAddress();
}

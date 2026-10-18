using Otium.Core.Pools;
using Otium.Core.Wallets;
using Otium.Server.Data;

namespace Otium.Server;

/// <summary>
/// The admin summary when custody is off. There are no balances, ledger or treasury to show; instead it counts
/// accounts by payout coin and referrals, and reads what Otium itself earns on unMineable (fee slices mined to its own
/// target, plus the 0.25–0.50% unMineable pays it for every miner using its referral code).
/// </summary>
public static class NonCustodialAdmin
{
    public static async Task<IResult> SummaryAsync(
        OtiumStore store, IUnmineableApi unmineable, UnmineableOptions options, Bandwidth.IMystWallet myst, bool mystSponsoring,
        TimeProvider time, ILogger logger, CancellationToken ct, Gpu.PearlForkWatcher? pearlFork = null)
    {
        var now = time.GetUtcNow();
        var (referred, verified) = store.ReferralCounts();
        object? otium = null;
        string? otiumError = null;

        if (PayoutTarget.ParseOrNull(options.OtiumTarget) is { } target && target.Coin != PayoutCoins.Monero)
        {
            try
            {
                var address = await unmineable.GetAddressAsync(target, ct);
                if (address.Uuid is { } uuid)
                {
                    var stats = await unmineable.GetStatsAsync(uuid, ct);
                    var referrals = await unmineable.GetReferralsAsync(uuid, ct);
                    otium = new
                    {
                        target = target.ToString(),
                        coin = stats.Coin,
                        feeMiningBalance = stats.MiningBalance,
                        referralBalance = stats.ReferralBalance,
                        balance = stats.Balance,
                        rewardedPast24h = stats.Rewarded24h,
                        rewardedPast30d = stats.Rewarded30d,
                        paid = stats.Paid,
                        threshold = stats.Threshold,
                        autoPay = address.AutoPay,
                        problems = address.Problems,
                        referralCode = referrals.Code,
                        referredMiners = referrals.Miners,
                        dashboardUrl = UnmineableClient.DashboardUrl(target).ToString(),
                    };
                }
                else
                {
                    otiumError = "unMineable hasn't seen Otium's address yet (nothing mined to it).";
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "unMineable lookup for the admin summary failed");
                otiumError = "unMineable's API didn't answer.";
            }
        }
        else
        {
            otiumError = "Set Unmineable:OtiumTarget to Otium's fee address (e.g. USDT:T…) to see its earnings here.";
        }

        decimal? mystWalletBalance = null;
        string? mystError = null;
        if (myst.IsConfigured)
        {
            try
            {
                mystWalletBalance = await myst.GetBalanceAsync(ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "MYST sponsor wallet lookup failed");
                mystError = "Polygon RPC didn't answer.";
            }
        }

        return Results.Ok(new
        {
            custodial = false,
            mysterium = new
            {
                sponsoring = mystSponsoring,
                nodesSponsored = store.MystSponsorshipCount(),
                mystSpent = store.MystSponsoredSince(),
                mystSpentPast24h = store.MystSponsoredSince(now.AddDays(-1)),
                walletBalanceMyst = mystWalletBalance,
                walletError = mystError,
            },
            accountsByCoin = store.PayoutCoinCounts().Select(c => new { c.Coin, c.Accounts }),
            devicesSeenPast7Days = store.DevicesSeenSince(now.AddDays(-7)),
            referredAccounts = referred,
            verifiedReferrals = verified,
            otium,
            otiumError,
            // Pearl's FP8 fork (PIP-3): once active, only Hopper and Blackwell cards mine Pearl.
            pearlFork = pearlFork is null ? null : await pearlFork.GetAsync(ct),
        });
    }
}

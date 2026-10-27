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

        decimal? mystWalletBalance = null, mystWalletGas = null;
        string? mystError = null;
        if (myst.IsConfigured)
        {
            try
            {
                mystWalletBalance = await myst.GetBalanceAsync(ct);
                mystWalletGas = await myst.GetGasBalanceAsync(ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "MYST sponsor wallet lookup failed");
                mystError = "Polygon RPC didn't answer.";
            }
        }

        // What opted-in computers reported over the last week (share analytics), by device kind.
        var reported = store.EarningsSince(now.AddDays(-7));
        var analytics = reported.GroupBy(e => e.Kind).OrderBy(g => g.Key).Select(g => new
        {
            kind = g.Key,
            devices = g.Select(e => e.DeviceId).Distinct().Count(),
            hours = Math.Round(g.Sum(e => e.Seconds) / 3600, 1),
            userUsd = Math.Round(g.Sum(e => e.UserUsd), 2),
            electricityUsd = Math.Round(g.Sum(e => e.ElectricityUsd), 2),
        }).ToList();
        var (accounts, accountsNew7d) = store.AccountCounts(now.AddDays(-7));
        var payouts = store.PoolPayoutTotals();
        // "Mining now": the app reports its mining time with each check-in (every minute), so 15 minutes allows for gaps.
        var miningNow = store.DevicesMiningSince(now.AddMinutes(-15));
        var mining24h = store.DevicesMiningSince(now.AddDays(-1));
        var retention = store.Retention(now.AddDays(-7), now.AddDays(-7));
        var day = (DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        return Results.Ok(new
        {
            custodial = false,
            activity = new
            {
                accounts,
                accountsNew24h = store.AccountCounts(now.AddDays(-1)).New,
                accountsNew7d,
                // Accounts with a computer that checked in (the app was running) in the period.
                accountsActivePast24h = store.AccountsSeenSince(now.AddDays(-1)),
                accountsActivePast7d = store.AccountsSeenSince(now.AddDays(-7)),
                accountsActivePast30d = store.AccountsSeenSince(now.AddDays(-30)),
                devices = store.DeviceCount(),
                devicesActiveNow = store.DevicesSeenSince(now.AddMinutes(-15)),
                devicesSeenPast24h = store.DevicesSeenSince(now.AddDays(-1)),
                devicesSeenPast30d = store.DevicesSeenSince(now.AddDays(-30)),
                miningNow = new { cpu = miningNow.Cpu, gpu = miningNow.Gpu, any = miningNow.Any },
                miningPast24h = new { cpu = mining24h.Cpu, gpu = mining24h.Gpu, any = mining24h.Any },
                // Computers that joined over a week ago, and how many of them still checked in during the last week.
                retention = new { joinedOverAWeekAgo = retention.Joined, seenPast7d = retention.StillSeen },
                newDevicesPerDay = store.NewDevicesPerDay(now, 14).Select(d => new { day = day(d.Day), d.Devices }),
                // The last 30 UTC days; active and mining counts start on the day the server began counting them.
                daily = store.DailyActivity(now, 30).Select(d => new
                {
                    day = day(d.Day), d.Devices, d.Accounts, d.CpuMining, d.GpuMining, d.NewDevices, d.NewAccounts,
                }),
                appVersions = store.AppVersionsSince(now.AddDays(-7)).Select(v => new { version = v.Version ?? "0.1.0 or older", devices = v.Devices }),
                // What processors and graphics cards were doing at their last check-in (computers seen in the last 24 h).
                engineStates = store.EngineStatesSince(now.AddDays(-1)).Select(e => new { device = e.Device, state = e.State, computers = e.Computers }),
            },
            // What the pools have paid users' own addresses (checked by the real-earnings job).
            poolPayouts = new { paidUsd = Math.Round(payouts.PaidUsd, 2), rewarded30dUsd = Math.Round(payouts.Rewarded30dUsd, 2), paidAccounts = payouts.PaidAccounts },
            analytics = new
            {
                devices = reported.Select(e => e.DeviceId).Distinct().Count(),
                byKind = analytics,
            },
            mysterium = new
            {
                sponsoring = mystSponsoring,
                nodesSponsored = store.MystSponsorshipCount(),
                mystSpent = store.MystSponsoredSince(),
                mystSpentPast24h = store.MystSponsoredSince(now.AddDays(-1)),
                walletBalanceMyst = mystWalletBalance,
                walletBalancePol = mystWalletGas,
                walletAddress = myst.Address,
                walletError = mystError,
            },
            accountsByCoin = store.PayoutCoinCounts().Select(c => new { c.Coin, c.Accounts }),
            devicesSeenPast7Days = store.DevicesSeenSince(now.AddDays(-7)),
            referredAccounts = referred,
            verifiedReferrals = verified,
            otium,
            otiumError,
            // Computers the pools showed mining for days without a single fee slice on Otium's address (a modified app?).
            feeSlicesMissing = store.FeeSliceGaps(now, TimeSpan.FromDays(7), TimeSpan.FromDays(2))
                .Select(g => new { g.DeviceId, g.AccountId, miningSeen = g.MiningSeen, feeSliceSeen = g.FeeSeen }),
            // Pearl's FP8 fork (PIP-3): once active, only Hopper and Blackwell cards mine Pearl.
            pearlFork = pearlFork is null ? null : await pearlFork.GetAsync(ct),
        });
    }
}

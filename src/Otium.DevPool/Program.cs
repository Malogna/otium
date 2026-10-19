namespace Otium.DevPool;

/// <summary>
/// A local stand-in for the MoneroOcean API. Start it with:  dotnet run --project src/Otium.DevPool
/// Then point the app and server at it with OTIUM_POOL_API=http://localhost:5090 (build/dev.ps1 does all of this).
/// An explicit entry point (rather than top-level statements) keeps its Program class from clashing with the server's
/// when both are referenced by the test project.
/// </summary>
public static class DevPoolHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5090");
        var speedup = builder.Configuration.GetValue("Speedup", 500.0);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(sp => new SimulatedPool(sp.GetRequiredService<TimeProvider>(), speedup));

        var app = builder.Build();

        // ---------- the subset of MoneroOcean's API that Otium uses ----------

        app.MapGet("/network/stats", (SimulatedPool pool) => Results.Ok(new
        {
            difficulty = pool.Difficulty,
            hash = "simulated",
            height = 3_784_827,
            value = (long)(SimulatedPool.BlockRewardXmr * 1e12),
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        }));

        app.MapGet("/pool/stats", (SimulatedPool pool) =>
        {
            // Per-algorithm payouts relative to RandomX, roughly in MoneroOcean's proportions, so GPU profit switching
            // has something realistic to choose between.
            var rx = pool.XmrPerHashPerSecondDay;
            var algos = new Dictionary<string, (string Algo, double Factor)>
            {
                ["18081"] = ("rx/0", 1),
                ["8766"] = ("kawpow", 8.7e-5),
                ["9053"] = ("autolykos2", 3.2e-5),
                ["8645"] = ("etchash", 1.25e-5),
                ["12211"] = ("cn/gpu", 2.4),
            };
            return Results.Ok(new
            {
                pool_statistics = new
                {
                    price = new { usd = SimulatedPool.PriceUsd },
                    activePorts = algos.Keys.Select(int.Parse).ToArray(),
                    portCoinAlgo = algos.ToDictionary(a => a.Key, a => a.Value.Algo),
                    coinProfit = algos.ToDictionary(a => a.Key, a => rx * a.Value.Factor),
                },
            });
        });

        app.MapGet("/miner/{wallet}/stats", (string wallet, SimulatedPool pool) => Results.Ok(pool.MinerStats(wallet)));

        app.MapGet("/miner/{wallet}/chart/hashrate/allWorkers", (string wallet, SimulatedPool pool) =>
            Results.Ok(pool.WorkerCharts(wallet).ToDictionary(w => w.Key, w => w.Value.Select(p => new { ts = p.Ts, hs = p.Hs, hs2 = p.Hs2 }))));

        // ---------- Kryptex Pool (Pearl, Quantus, and Monero for CPUs routed there) and WhatToMine ----------

        // Simulated miners report Kryptex work with the wallet "kryptex:{coin}:{account}" and no XMR value.
        app.MapGet("/kryptex/{coin}/api/v1/pool/info", (string coin) => Results.Ok(new { fee = coin == "xmr" ? 0.01 : 0.03, fee_type = "PPS+", ticker = coin }));

        app.MapGet("/kryptex/{coin}/api/v3/miner/workers/{account}", (string coin, string account, SimulatedPool pool, TimeProvider time) =>
        {
            var nowMs = time.GetUtcNow().ToUnixTimeMilliseconds();
            // Shaped like the live API: 30-minute, 3-hour and 24-hour averages as strings, no "hashrate" field.
            string Average(IReadOnlyList<ChartPoint> points, TimeSpan window) =>
                (points.Where(p => p.Ts > nowMs - (long)window.TotalMilliseconds).Sum(p => p.Hs) * SimulatedPool.PointSpacing.TotalSeconds
                 / window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var workers = pool.WorkerCharts(KryptexWallet(coin, account))
                .Select(w => new
                {
                    worker = w.Key,
                    avg_hashrate_30m = Average(w.Value, TimeSpan.FromMinutes(30)),
                    avg_hashrate_3h = Average(w.Value, TimeSpan.FromHours(3)),
                    avg_hashrate_24h = Average(w.Value, TimeSpan.FromHours(24)),
                    scheme = "pps",
                    status = "online",
                })
                .ToList();
            return Results.Ok(new { count = workers.Count, results = workers });
        });

        // Like the real pool, per-worker history is private for "krx" accounts (October 2026).
        app.MapGet("/kryptex/{coin}/api/v1/miner/chart/{account}/{worker}", () =>
            Results.Json(new { detail = "You do not have permission to perform this action." }, statusCode: 403));

        // WhatToMine coin JSON shaped so that revenue per H/s per day comes out at roughly October 2026's real rate,
        // sped up like everything else: revenue = block_reward × 86400 / block_time / nethash × price.
        app.MapGet("/wtm/coins/{file}", (string file, SimulatedPool pool) =>
        {
            var id = file.Replace(".json", "", StringComparison.Ordinal);
            var usdPerHashDay = id switch
            {
                "469" => 3.4e-14, // Pearl: an RTX 4090 at ~310 TH/s grosses about $10.5 a day
                "473" => 1.6e-9,  // Quantus
                "459" => 6e-6,    // Xelis: an RTX 3080 at ~50 kH/s grosses about $0.30 a day
                _ => 0,
            } * pool.Speedup;
            if (usdPerHashDay <= 0)
                return Results.NotFound();
            const double nethash = 1e15, blockTime = 100, btcUsd = 100_000, priceBtc = 1e-5; // a coin worth $1
            return Results.Ok(new
            {
                id = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture),
                block_time = blockTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
                block_reward24 = usdPerHashDay * nethash * blockTime / 86_400 / (priceBtc * btcUsd),
                nethash,
                exchange_rate24 = priceBtc,
                revenue = "$1.00",
                btc_revenue = (1.0 / btcUsd).ToString("0.########", System.Globalization.CultureInfo.InvariantCulture),
                lagging = false,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
        });

        // ---------- unMineable (OTIUM_UNMINEABLE_API=<this pool>/unmineable) ----------

        // Simulated miners report unMineable work under the payout target ("USDT:T…"), valued in XMR like everything
        // else here; these endpoints convert it into the target's coin at fixed dev prices, minus unMineable's 0.75%.
        // Like a real new TRC-20 address, USDT addresses have auto-pay off, so the app's warning can be seen.
        app.MapGet("/unmineable/v5/address/{address}", (string address, string coin, SimulatedPool pool) =>
        {
            var target = $"{coin.ToUpperInvariant()}:{address}";
            var balance = UnmineableAmount(target, pool);
            return Results.Ok(new
            {
                success = true,
                msg = "Ok",
                data = new
                {
                    balance = balance.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
                    balance_payable = balance.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
                    payment_threshold = DevCoins.GetValueOrDefault(coin.ToUpperInvariant()).Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    mining_fee = "0.75",
                    auto = !coin.Equals("USDT", StringComparison.OrdinalIgnoreCase),
                    enabled = true,
                    maintenance_message = (string?)null,
                    uuid = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(target)),
                    network = coin.ToUpperInvariant() == "USDT" ? "TRX" : coin.ToUpperInvariant(),
                    err_flags = new { payment_error = false, missing_memo = false, not_activated = false, restricted = false },
                },
            });
        });

        app.MapGet("/unmineable/v5/account/{uuid}/stats", (string uuid, SimulatedPool pool) =>
        {
            var target = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(uuid));
            var coin = target.Split(':')[0];
            var balance = UnmineableAmount(target, pool).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            return Results.Ok(new
            {
                success = true,
                msg = "Ok",
                data = new
                {
                    balance_mining = balance, balance_referral = "0", balance,
                    rewarded = new { past_24h = balance, past_7d = balance, past_30d = balance },
                    paid = "0", last_payment = (long?)null,
                    payment_threshold = DevCoins.GetValueOrDefault(coin).Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    mining_fee = "0.75", coin,
                },
            });
        });

        app.MapGet("/unmineable/v5/account/{uuid}/workers", (string uuid, SimulatedPool pool) =>
        {
            var target = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(uuid));
            var workers = pool.OnlineWorkers(target).Select(w => new { name = w.Worker, online = true, chr = w.Hs.ToString(System.Globalization.CultureInfo.InvariantCulture), rhr = w.Hs.ToString(System.Globalization.CultureInfo.InvariantCulture), referral = "ztjq-jr7s" });
            return Results.Ok(new { success = true, msg = "Ok", data = new { randomx = new { workers, chart = new { } } } });
        });

        app.MapGet("/unmineable/v5/account/{uuid}/payments", () =>
            Results.Ok(new { success = true, message = "Ok", data = new { list = Array.Empty<object>(), has_next = false, count = 0 } }));

        app.MapGet("/unmineable/v5/account/{uuid}/referrals", () =>
            Results.Ok(new { success = true, msg = "Ok", data = new { code = "ztjq-jr7s", custom_code = (string?)null, list = Array.Empty<object>(), count = 0 } }));

        // ---------- simulation controls ----------

        app.MapPost("/dev/report", (MinerReport report, SimulatedPool pool) =>
        {
            pool.Report(report);
            return Results.NoContent();
        });

        app.MapGet("/", (SimulatedPool pool) => Results.Ok(pool.Summary()));

        app.Run();
    }

    public static string KryptexWallet(string coin, string account) => $"kryptex:{coin}:{account}";

    /// <summary>Dev prices (USD) and unMineable's payout thresholds for the coins Otium offers.</summary>
    private static readonly Dictionary<string, (double PriceUsd, decimal Threshold)> DevCoins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USDT"] = (1, 1.5m),
        ["BTC"] = (100_000, 0.00075m),
        ["LTC"] = (80, 0.00075m),
        ["BNB"] = (600, 0.01m),
        ["SOL"] = (150, 0.05m),
        ["TRX"] = (0.25, 15m),
        ["ETH"] = (3_000, 0.025m),
        ["XMR"] = (SimulatedPool.PriceUsd, 0.03m),
    };

    /// <summary>A target's simulated earnings in its own coin, after unMineable's 0.75% fee.</summary>
    private static double UnmineableAmount(string target, SimulatedPool pool)
    {
        var coin = target.Split(':')[0];
        var price = DevCoins.TryGetValue(coin, out var c) ? c.PriceUsd : 1;
        return pool.EarnedXmr(target) * SimulatedPool.PriceUsd / price * 0.9925;
    }
}

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

        // ---------- Kryptex Pool (Pearl, Quantus) and WhatToMine, for the GPU policy ----------

        // Simulated miners report Kryptex work with the wallet "kryptex:{coin}:{account}" and no XMR value.
        app.MapGet("/kryptex/{coin}/api/v1/pool/info", (string coin) => Results.Ok(new { fee = 0.03, fee_type = "PPS+", ticker = coin }));

        app.MapGet("/kryptex/{coin}/api/v3/miner/workers/{account}", (string coin, string account, SimulatedPool pool) =>
        {
            var workers = pool.WorkerCharts(KryptexWallet(coin, account))
                // Shaped like the live API: averages as strings, no "hashrate" field.
                .Select(w => new
                {
                    worker = w.Key,
                    avg_hashrate_30m = (w.Value.LastOrDefault()?.Hs ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    scheme = "pps",
                    status = "online",
                })
                .ToList();
            return Results.Ok(new { count = workers.Count, results = workers });
        });

        app.MapGet("/kryptex/{coin}/api/v1/miner/chart/{account}/{worker}", (string coin, string account, string worker, SimulatedPool pool) =>
            Results.Ok(pool.WorkerCharts(KryptexWallet(coin, account)).GetValueOrDefault(worker, [])
                .Select(p => new { timestamp = p.Ts / 1000, hashrate = p.Hs, shares = 0 })));

        // WhatToMine coin JSON shaped so that revenue per H/s per day comes out at roughly October 2026's real rate,
        // sped up like everything else: revenue = block_reward × 86400 / block_time / nethash × price.
        app.MapGet("/wtm/coins/{file}", (string file, SimulatedPool pool) =>
        {
            var id = file.Replace(".json", "", StringComparison.Ordinal);
            var usdPerHashDay = id switch
            {
                "469" => 3.4e-14, // Pearl: an RTX 4090 at ~310 TH/s grosses about $10.5 a day
                "473" => 1.6e-9,  // Quantus
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
}

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

        // ---------- simulation controls ----------

        app.MapPost("/dev/report", (MinerReport report, SimulatedPool pool) =>
        {
            pool.Report(report);
            return Results.NoContent();
        });

        app.MapGet("/", (SimulatedPool pool) => Results.Ok(pool.Summary()));

        app.Run();
    }
}

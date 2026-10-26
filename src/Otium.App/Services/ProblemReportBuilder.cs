using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Otium.Core.Accounts;
using Otium.Core.Platform;
using Otium.Core.Support;

namespace Otium.App.Services;

/// <summary>
/// Puts together what support needs to see why a computer isn't earning: settings switches, each engine's state and
/// problem, the graphics card's tests and set-aside coins, the miners' recent output, internet sharing and the
/// antivirus in use. Payout addresses, keys and the device secret are removed (<see cref="ReportScrubber"/>).
/// </summary>
public static class ProblemReportBuilder
{
    public static ProblemReport Build(AppHost host, string? note)
    {
        var s = host.Settings;
        var b = new StringBuilder();
        void Line(string text) => b.AppendLine(text);
        string Invariant(FormattableString f) => f.ToString(CultureInfo.InvariantCulture);

        Line(Invariant($"Sent: {DateTimeOffset.UtcNow:u}"));
        Line($"Processor: {CpuInfo.Name} ({Environment.ProcessorCount} threads)");
        Line($"Graphics cards: {(host.Gpus.Count == 0 ? "none found" : string.Join("; ", host.Gpus.Select(g => g.Describe())))}");
        Line($"Simulated: {host.IsSimulated}");
        Line("");
        Line("Settings:");
        Line(Invariant($"  earning {s.EarningEnabled}, processor {s.CpuEnabled} ({s.CpuUsagePercent}%), graphics card {s.GpuEnabled}, only when away {s.OnlyWhenIdle} ({s.IdleMinutes} min)"));
        Line($"  keep awake {s.KeepAwakeWhileEarning}, keep miner ready {s.KeepMinerReadyWhenBack}, efficiency mode {s.GpuEfficiencyMode}, earn while browsing {s.EarnWhileBrowsing}");
        Line(Invariant($"  pay for electricity {s.PayForElectricity} ({s.ElectricityPricePerKwh:0.###}/kWh), never at a loss {s.NeverRunAtLoss}"));
        Line($"  payout coin {Core.Wallets.PayoutTarget.ParseOrNull(s.PayoutAddress)?.Coin.Ticker ?? "none"}, mode {s.PayoutMode}, United States {AppHost.InUnitedStates}");
        Line($"  internet sharing {s.BandwidthEnabled} (offered {host.BandwidthOffered}), share earnings {s.ShareEarningsStats}");
        Line($"  server {Core.AppIdentity.ResolveServerUrl()}, last server error: {host.LastServerError ?? "none"}");
        Line($"  large pages in effect {LargePages.InEffect()}");

        foreach (var (name, engine) in host.Engines.Select((e, i) => (i == 0 ? "Processor" : "Graphics card", e)))
        {
            var snap = engine.Snapshot;
            Line("");
            Line($"{name}: {snap.State}, \"{snap.StatusText}\"");
            Line(Invariant($"  hashrate {snap.HashrateHps:0.##} H/s, user ${snap.UserUsdPerDay:0.####}/day, electricity ${snap.ElectricityUsdPerDay:0.####}/day, {snap.ExtraWatts:0} W ({snap.PowerSource})"));
            Line($"  detail: {snap.Detail ?? "-"}; problem: {snap.Problem ?? "none"}; calibrating {snap.IsCalibrating}; engine blocked {snap.EngineBlocked}");
            Line(Invariant($"  total mined {snap.TotalMiningTime.TotalHours:0.#} h, earned ${snap.TotalUserUsd:0.####}"));
        }

        if (host.GpuProvider is { } gpu)
        {
            Line("");
            Line($"Graphics card coin now: {gpu.CurrentAlgorithm ?? "none"}");
            foreach (var bench in gpu.Benchmarks)
                Line(Invariant($"  tested {bench.Algorithm}: {bench.HashrateHps:0.##} H/s, {bench.ExtraWatts?.ToString("0", CultureInfo.InvariantCulture) ?? "?"} W{(bench.Failure is { } f ? $", failed: {f}" : "")}{(gpu.SetAsideReason(bench.Algorithm) is { } why ? $", set aside: {why}" : "")}"));
        }

        var sharing = host.Bandwidth.Snapshot;
        Line("");
        Line($"Internet sharing: {sharing.State}, \"{sharing.StatusText}\"{(sharing.Earnings is { } e ? $", {e}" : "")}");
        // The node's own MYST figures only; its payout (Polygon) address is left out like every other address.
        if (host.Mysterium?.Snapshot is { } myst)
            Line($"Mysterium sharing: on {s.MysteriumEnabled}, {myst.State}, \"{myst.StatusText}\"{(myst.Earnings is { } me ? $", {me}" : "")}");
        if (host.Aioz?.Snapshot is { } aioz)
            Line($"AIOZ sharing: on {s.AiozEnabled}, {aioz.State}, \"{aioz.StatusText}\"{(aioz.Earnings is { } ae ? $", {ae}" : "")}");

        if (host.PoolBalance is { } pool)
            Line(Invariant($"Pool: {pool.PoolName}, pending {pool.Pending} {pool.Coin}, paid {pool.Paid}, auto pay {pool.AutoPay}, problems: {string.Join("; ", pool.Problems ?? [])}"));
        else if (host.PoolBalanceError is { } poolError)
            Line($"Pool: {poolError}");

        if (host.AntivirusReport is { } av)
            Line($"Antivirus: {string.Join(", ", av.Products.Select(p => $"{p.Name}{(p.Enabled ? "" : " (off)")}"))}; {av.Describe(host.Paths.EnginesDir)}");

        AppendLog(b, "Processor miner (XMRig), recent output", host.Provider.RecentLog);
        if (host.GpuProvider is { } g)
            AppendLog(b, "Graphics card miner (SRBMiner), recent output", g.RecentLog);

        string?[] known =
        [
            s.PayoutAddress, Core.Wallets.PayoutTarget.ParseOrNull(s.PayoutAddress)?.Address, s.BandwidthPayoutAddress, s.QuantusAddress,
            s.DeviceSecret, s.Referral.ReferrerWallet, s.Account.Email, s.Account.LastPayPalEmail, .. s.Account.LastAddresses.Values,
            Environment.MachineName, Environment.UserName,
        ];
        var details = ReportScrubber.Scrub(b.ToString(), known);
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : ReportScrubber.Scrub(note.Trim(), known);
        return new ProblemReport(host.Updates.CurrentVersion, RuntimeInformation.OSDescription + " " + RuntimeInformation.OSArchitecture, cleanNote, details);
    }

    private static void AppendLog(StringBuilder b, string title, IReadOnlyList<string> lines)
    {
        b.AppendLine();
        b.AppendLine($"{title}:");
        foreach (var line in lines.TakeLast(80))
            b.AppendLine($"  {line}");
    }
}

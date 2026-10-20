using Otium.Core.Accounts;
using Otium.Core.Engine;
using Otium.Core.Settings;

namespace Otium.Core.Projections;

/// <summary>A device's running totals, which only grow (mining time, the user's share, electricity).</summary>
/// <param name="ReferrerUsd">Estimated, mined for the friend who invited the user (their share of Otium's fee).</param>
public readonly record struct EarningsTotals(TimeSpan MiningTime, double UserUsd, double ElectricityUsd, double ReferrerUsd = 0);

/// <summary>One device as the reports see it: "cpu" or "gpu", its model, and its totals now.</summary>
public sealed record ReportedDevice(string Kind, string? Model, EarningsTotals Totals);

/// <summary>A report ready to send, and the baselines to keep once the server has it.</summary>
public sealed record PendingEarningsReport(EarningsReport Report, Dictionary<string, double[]> Baselines);

/// <summary>
/// The opt-in earnings reports behind otium.lol's real-earnings table: every few hours, what each device earned since
/// the last report. Nothing from before the user agreed is sent (the baselines start at that moment).
/// </summary>
public static class EarningsReports
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Less mining than this in a period isn't worth a report; it's carried into the next one.</summary>
    public static readonly TimeSpan MinimumMining = TimeSpan.FromMinutes(5);

    /// <summary>Starts counting from now (when the user turns sharing on).</summary>
    public static void Reset(EarningsReportState state, IEnumerable<ReportedDevice> devices, DateTimeOffset now)
    {
        state.Baselines = devices.ToDictionary(d => d.Kind, d => Values(d.Totals));
        state.LastAt = now;
    }

    /// <summary>The next report when one is due and something was mined, otherwise null.</summary>
    public static PendingEarningsReport? Next(EarningsReportState state, IReadOnlyList<ReportedDevice> devices, DateTimeOffset now)
    {
        if (state.LastAt is not { } last)
        {
            Reset(state, devices, now);
            return null;
        }
        if (now - last < Interval)
            return null;

        var entries = new List<DeviceEarnings>();
        var baselines = new Dictionary<string, double[]>();
        foreach (var device in devices)
        {
            var current = Values(device.Totals);
            var before = state.Baselines.TryGetValue(device.Kind, out var b) && b.Length == 3 ? b : current;
            // Totals only grow; smaller ones mean the local ledger was reset, so everything in it is new.
            var delta = current[0] < before[0] ? current : current.Zip(before, (c, p) => Math.Max(0, c - p)).ToArray();
            baselines[device.Kind] = current;
            if (delta[0] > 0)
                entries.Add(new DeviceEarnings(device.Kind, device.Model, delta[0], delta[1], delta[2]));
        }

        if (entries.Sum(e => e.EarningSeconds) < MinimumMining.TotalSeconds)
            return null; // keep the old baselines so these minutes go into the next report
        return new PendingEarningsReport(new EarningsReport(state.Sequence + 1, last, now, entries), baselines);
    }

    /// <summary>The server has the report: the next one starts from here.</summary>
    public static void Commit(EarningsReportState state, PendingEarningsReport sent)
    {
        state.Sequence = sent.Report.Sequence;
        state.LastAt = sent.Report.PeriodEnd;
        state.Baselines = sent.Baselines;
    }

    private static double[] Values(EarningsTotals t) => [t.MiningTime.TotalSeconds, t.UserUsd, t.ElectricityUsd];
}

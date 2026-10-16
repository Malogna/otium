namespace Otium.Core.Projections;

/// <summary>What the user can expect, in plain numbers, from the rate while earning and how long it earns each day.</summary>
public sealed record Projection(
    double EarningRatePerDay,
    double ElectricityPerDay,
    double HoursPerDay,
    bool HoursAreMeasured,
    double PerDay,
    double PerWeek,
    double PerMonth,
    TimeSpan? TimeToCashout)
{
    /// <summary>
    /// <paramref name="earningRatePerDay"/> and <paramref name="electricityPerDay"/> are what 24 hours of continuous
    /// earning would bring and cost; projections scale them by the hours per day Otium actually runs.
    /// </summary>
    public static Projection Create(
        double earningRatePerDay,
        double electricityPerDay,
        double? measuredHoursPerDay,
        bool onlyWhenIdle,
        decimal towardsCashout,
        decimal cashoutMinimum)
    {
        // Without a day of history: idle-only computers are typically away ~8 h a day (nights, workday);
        // "always on" still sleeps or shuts down some of the time.
        var hours = Math.Clamp(measuredHoursPerDay ?? (onlyWhenIdle ? 8 : 16), 0, 24);
        var perDay = Math.Max(0, earningRatePerDay - electricityPerDay) * hours / 24;

        TimeSpan? toCashout = null;
        var missing = (double)(cashoutMinimum - towardsCashout);
        if (missing <= 0)
            toCashout = TimeSpan.Zero;
        else if (perDay > 0)
            toCashout = TimeSpan.FromDays(Math.Min(missing / perDay, 3650));

        return new Projection(earningRatePerDay, electricityPerDay, hours, measuredHoursPerDay is not null,
            perDay, perDay * 7, perDay * 30, toCashout);
    }
}

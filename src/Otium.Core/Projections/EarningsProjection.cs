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
    double PerYear,
    TimeSpan? TimeToCashout)
{
    /// <summary>Hours a day assumed for a computer that earns only while its user is away, before it has a pattern.</summary>
    public const double IdleOnlyHours = 8;

    /// <summary>Hours a day assumed for a computer that earns whenever it is on (it still sleeps or shuts down some of the day).</summary>
    public const double AlwaysOnHours = 16;

    public const int DaysPerMonth = 30;

    /// <summary>
    /// The hours a day to project at the current pace. History says how many hours the computer earned, but a computer
    /// that never earned much (paused, or only just set to earn all the time) and is mining right now would project
    /// almost nothing for days. So when it earns whenever it is on and is earning at this moment, a history below
    /// <see cref="AlwaysOnHours"/> is set aside for the always-on assumption; a long history above it, and a computer that
    /// earns only while its user is away, keep their measured hours. Null means "no measurement: use the assumption".
    /// </summary>
    public static double? HoursToProject(double? measuredHoursPerDay, bool onlyWhenIdle, bool earningNow) =>
        !onlyWhenIdle && earningNow && (measuredHoursPerDay ?? 0) < AlwaysOnHours ? null : measuredHoursPerDay;

    public const int DaysPerYear = 365;

    /// <summary>The same projection at <paramref name="perDay"/> a day, with the dependent amounts and the time to cash out redone.</summary>
    public Projection AtPerDay(double perDay, decimal towardsCashout, decimal cashoutMinimum)
    {
        var missing = (double)(cashoutMinimum - towardsCashout);
        TimeSpan? toCashout = missing <= 0 ? TimeSpan.Zero : perDay > 0 ? TimeSpan.FromDays(Math.Min(missing / perDay, 3650)) : null;
        return this with
        {
            PerDay = perDay,
            PerWeek = perDay * 7,
            PerMonth = perDay * DaysPerMonth,
            PerYear = perDay * DaysPerYear,
            TimeToCashout = toCashout,
        };
    }

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
        var hours = Math.Clamp(measuredHoursPerDay ?? (onlyWhenIdle ? IdleOnlyHours : AlwaysOnHours), 0, 24);
        var perDay = Math.Max(0, earningRatePerDay - electricityPerDay) * hours / 24;

        TimeSpan? toCashout = null;
        var missing = (double)(cashoutMinimum - towardsCashout);
        if (missing <= 0)
            toCashout = TimeSpan.Zero;
        else if (perDay > 0)
            toCashout = TimeSpan.FromDays(Math.Min(missing / perDay, 3650));

        return new Projection(earningRatePerDay, electricityPerDay, hours, measuredHoursPerDay is not null,
            perDay, perDay * 7, perDay * DaysPerMonth, perDay * DaysPerYear, toCashout);
    }
}

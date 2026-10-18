namespace Otium.Core.Projections;

/// <summary>What one stretch of earning brought in, shown when the user comes back ("While you were away…").</summary>
/// <param name="EarnedUsd">The user's share, after Otium's fee (estimated from the measured hash rate and that hour's prices).</param>
/// <param name="Earning">Time actually spent earning, which is less than the time away when the engines were starting.</param>
public sealed record AwaySummary(DateTimeOffset Started, DateTimeOffset Ended, TimeSpan Earning, double EarnedUsd, double ElectricityUsd)
{
    public double NetUsd => EarnedUsd - ElectricityUsd;
}

/// <summary>
/// Notices when a stretch of earning ends and adds up what it brought in. Fed with the running totals of every engine
/// (they only grow) every time an engine reports, a few times a minute.
/// </summary>
public sealed class AwaySessionTracker
{
    /// <summary>Shorter stretches (a quick test, a coffee) aren't worth a summary.</summary>
    public static readonly TimeSpan MinimumEarning = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The longest gap between two reports that still counts as earning. Longer gaps mean the computer slept or Otium
    /// wasn't running, and that time isn't counted.
    /// </summary>
    public static readonly TimeSpan MaximumGap = TimeSpan.FromMinutes(2);

    private DateTimeOffset? _started;
    private DateTimeOffset _last;
    private TimeSpan _earning;
    private double _earnedAtStart;
    private double _electricityAtStart;
    private double _earnedNow;
    private double _electricityNow;

    /// <summary>Whether a stretch of earning is under way.</summary>
    public bool InSession => _started is not null;

    /// <summary>
    /// Records the latest state. Returns the summary when a stretch of earning has just ended and lasted at least
    /// <see cref="MinimumEarning"/>, otherwise null.
    /// </summary>
    /// <param name="earning">Any engine is earning (or starting up between fee slices).</param>
    public AwaySummary? Observe(DateTimeOffset now, bool earning, double cumulativeEarnedUsd, double cumulativeElectricityUsd)
    {
        if (earning)
        {
            if (_started is null)
            {
                _started = now;
                _earning = TimeSpan.Zero;
                _earnedAtStart = cumulativeEarnedUsd;
                _electricityAtStart = cumulativeElectricityUsd;
            }
            else
            {
                var gap = now - _last;
                if (gap > TimeSpan.Zero && gap <= MaximumGap)
                    _earning += gap;
            }
            _last = now;
            _earnedNow = cumulativeEarnedUsd;
            _electricityNow = cumulativeElectricityUsd;
            return null;
        }

        if (_started is not { } started)
            return null;
        _started = null;
        var earned = Math.Max(0, Math.Max(_earnedNow, cumulativeEarnedUsd) - _earnedAtStart);
        var electricity = Math.Max(0, Math.Max(_electricityNow, cumulativeElectricityUsd) - _electricityAtStart);
        return _earning >= MinimumEarning && earned > 0
            ? new AwaySummary(started, _last, _earning, earned, electricity)
            : null;
    }
}

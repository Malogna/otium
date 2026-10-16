using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Earnings;

namespace Otium.Server.Bandwidth;

/// <summary>
/// Credits balance accounts for bandwidth their computers shared. Usage is self-reported every few minutes, so it is
/// bounded before anything is paid: each report is credited once, periods are clipped to a sane length, average speed
/// is capped, each computer has a daily limit, and only one computer per internet connection earns at a time (bandwidth
/// partners pay per residential connection, not per device). The operator reconciles credited totals against the
/// partner's publisher statements via the admin summary.
/// </summary>
public sealed class BandwidthService(
    OtiumStore store,
    IOptions<BandwidthOptions> bandwidthOptions,
    IOptions<EarningsOptions> earningsOptions,
    TimeProvider time,
    ILogger<BandwidthService> logger)
{
    public const double BytesPerGb = 1024d * 1024 * 1024;

    private readonly BandwidthOptions _options = bandwidthOptions.Value;
    private readonly EarningsOptions _earnings = earningsOptions.Value;

    /// <summary>The terms the app shows, net of Otium's fee. Null for accounts that can't use bandwidth sharing.</summary>
    public BandwidthInfo Terms(AccountRecord account)
    {
        var keep = 1 - _earnings.FeePercent / 100.0;
        return new BandwidthInfo(
            _options.Enabled && account.IsBalanceAccount,
            _options.UsdPerGb * keep,
            _options.UsdPerSharingHour * keep,
            _options.DailyLimitBytes,
            _options.PartnerName);
    }

    public BandwidthReportResult Report(Session session, BandwidthReport report, string ip)
    {
        if (!_options.Enabled)
            throw new OtiumException(403, "Internet sharing isn't available yet.");
        if (!session.Account.IsBalanceAccount)
            throw new OtiumException(400, "Internet sharing pays into an Otium balance. Switch back from your own wallet to use it.");

        var device = session.Device.DeviceId;
        if (store.FindBandwidthReport(device, report.Sequence) is { } existing)
            return ToResult(existing); // a retry of a report we already handled

        var now = time.GetUtcNow();
        if (report.Sequence < 0 || report.Bytes < 0 || report.SharingSeconds < 0 || report.PeriodEnd <= report.PeriodStart)
            throw new OtiumException(400, "Invalid usage report.");
        if (report.PeriodEnd > now + TimeSpan.FromMinutes(2) || report.PeriodEnd < now - TimeSpan.FromDays(2))
            throw new OtiumException(400, "Usage report is out of date. Check your computer's clock.");

        // Never pay for more time than the report could cover, or faster than a home connection plausibly shares.
        var period = report.PeriodEnd - report.PeriodStart;
        if (period > _options.MaxReportPeriod)
            period = _options.MaxReportPeriod;
        var seconds = Math.Min(report.SharingSeconds, period.TotalSeconds);
        var bytes = Math.Min(report.Bytes, (long)(Math.Max(seconds, 1) * _options.MaxBytesPerSecond));

        string status;
        string? note = null;
        long creditedBytes = 0;
        double creditedSeconds = 0;

        if (store.OtherSharerOnIp(ip, device, now - _options.SharedConnectionWindow) is not null)
        {
            status = BandwidthReportStatuses.SharedConnection;
            note = "Another computer on your internet connection is already sharing. Only one computer per connection earns from sharing.";
        }
        else
        {
            var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var left = Math.Max(0, _options.DailyLimitBytes - store.CreditedBandwidthBytesSince(device, dayStart));
            creditedBytes = Math.Min(bytes, left);
            creditedSeconds = seconds;
            status = creditedBytes < bytes ? BandwidthReportStatuses.DailyLimit : BandwidthReportStatuses.Credited;
            if (status == BandwidthReportStatuses.DailyLimit)
                note = "Today's sharing limit is reached. Sharing pays again from midnight (UTC).";
        }

        var gross = creditedBytes / BytesPerGb * _options.UsdPerGb + creditedSeconds / 3600 * _options.UsdPerSharingHour;
        var (userMicros, _) = gross > 0
            ? EarningsCreditor.Credit(store, _earnings, session.Account, gross, $"bw:{device}:{report.Sequence}", now)
            : (0L, 0L);

        // Recorded even when nothing is paid: a computer whose share was refused must not grab the connection next time.
        var record = new BandwidthReportRecord(device, report.Sequence, session.Account.AccountId, ip, report.PeriodStart,
            report.PeriodEnd, report.Bytes, report.SharingSeconds, creditedBytes, userMicros, status, now);
        store.AddBandwidthReport(record);
        if (status != BandwidthReportStatuses.Credited)
            logger.LogInformation("Bandwidth report {Device}#{Seq}: {Status}", device, report.Sequence, status);
        return new BandwidthReportResult(status, creditedBytes, ToUsd(userMicros), note);
    }

    /// <summary>Unrounded: a few minutes of sharing earns fractions of a cent.</summary>
    private static decimal ToUsd(long micros) => micros / (decimal)OtiumStore.MicrosPerDollar;

    private static BandwidthReportResult ToResult(BandwidthReportRecord r) =>
        new(r.Status, r.CreditedBytes, ToUsd(r.CreditedMicros),
            r.Status == BandwidthReportStatuses.SharedConnection
                ? "Another computer on your internet connection is already sharing. Only one computer per connection earns from sharing."
                : null);
}

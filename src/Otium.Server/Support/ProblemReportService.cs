using System.Security.Cryptography;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Support;

/// <summary>Stores problem reports from the app, at most a few per computer and account a day, and hands back a reference.</summary>
public sealed class ProblemReportService(OtiumStore store, TimeProvider time, ILogger<ProblemReportService> logger)
{
    public const int MaxPerDevicePerDay = 5;

    /// <summary>Also per account, since one account can sign in on any number of computers.</summary>
    public const int MaxPerAccountPerDay = 5;

    public const int MaxDetailsLength = 300_000;
    public const int MaxNoteLength = 2_000;

    /// <summary>
    /// All problem reports together may hold this many characters (about 200 MB), so they can never fill the server's
    /// disk; past it, the oldest reports are deleted to make room, so a flood of reports can't stop new ones coming in.
    /// </summary>
    public const long DefaultMaxStoredCharacters = 200_000_000;

    internal long MaxStoredCharacters { get; init; } = DefaultMaxStoredCharacters;

    /// <summary>Letters and digits that can't be mistaken for each other when read out (no 0/O, 1/I/L).</summary>
    private const string ReferenceAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Refuses a computer or account that already sent its reports for the day.</summary>
    public void CheckAllowance(Session session)
    {
        var dayAgo = time.GetUtcNow() - TimeSpan.FromDays(1);
        if (store.ProblemReportsSince(session.Device.DeviceId, dayAgo) >= MaxPerDevicePerDay)
            throw new OtiumException(StatusCodes.Status429TooManyRequests, "This computer already sent several problem reports today. Please try again tomorrow.");
        if (store.AccountProblemReportsSince(session.Account.AccountId, dayAgo) >= MaxPerAccountPerDay)
            throw new OtiumException(StatusCodes.Status429TooManyRequests, "This account already sent several problem reports today. Please try again tomorrow.");
    }

    public ProblemReportResult Report(Session session, ProblemReport report)
    {
        var now = time.GetUtcNow();
        if (report.Details.Length > MaxDetailsLength || report.Note?.Length > MaxNoteLength || report.AppVersion.Length > 50 || report.Platform.Length > 200)
            throw new OtiumException(StatusCodes.Status400BadRequest, "This problem report is too long.");
        CheckAllowance(session);
        var size = report.Details.Length + (report.Note?.Length ?? 0);
        var over = store.ProblemReportCharacters() + size - MaxStoredCharacters;
        if (over > 0)
            logger.LogWarning("Problem reports are full; deleted the oldest {Count} to make room", store.DeleteOldestProblemReports(over));

        var reference = new string(RandomNumberGenerator.GetItems(ReferenceAlphabet.AsSpan(), 8));
        store.AddProblemReport(new OtiumStore.ProblemReportRecord(reference, session.Account.AccountId, session.Device.DeviceId,
            report.AppVersion, report.Platform, string.IsNullOrWhiteSpace(report.Note) ? null : report.Note.Trim(), report.Details, now));
        return new ProblemReportResult(reference);
    }
}

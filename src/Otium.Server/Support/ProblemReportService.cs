using System.Security.Cryptography;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Support;

/// <summary>Stores problem reports from the app, at most a few per computer a day, and hands back a reference.</summary>
public sealed class ProblemReportService(OtiumStore store, TimeProvider time)
{
    public const int MaxPerDevicePerDay = 5;
    public const int MaxDetailsLength = 300_000;
    public const int MaxNoteLength = 2_000;

    /// <summary>Letters and digits that can't be mistaken for each other when read out (no 0/O, 1/I/L).</summary>
    private const string ReferenceAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public ProblemReportResult Report(Session session, ProblemReport report)
    {
        var now = time.GetUtcNow();
        if (report.Details.Length > MaxDetailsLength || report.Note?.Length > MaxNoteLength || report.AppVersion.Length > 50 || report.Platform.Length > 200)
            throw new OtiumException(StatusCodes.Status400BadRequest, "This problem report is too long.");
        if (store.ProblemReportsSince(session.Device.DeviceId, now - TimeSpan.FromDays(1)) >= MaxPerDevicePerDay)
            throw new OtiumException(StatusCodes.Status429TooManyRequests, "This computer already sent several problem reports today. Please try again tomorrow.");

        var reference = new string(RandomNumberGenerator.GetItems(ReferenceAlphabet.AsSpan(), 8));
        store.AddProblemReport(new OtiumStore.ProblemReportRecord(reference, session.Account.AccountId, session.Device.DeviceId,
            report.AppVersion, report.Platform, string.IsNullOrWhiteSpace(report.Note) ? null : report.Note.Trim(), report.Details, now));
        return new ProblemReportResult(reference);
    }
}

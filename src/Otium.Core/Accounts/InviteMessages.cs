using System.Globalization;

namespace Otium.Core.Accounts;

/// <summary>A ready-made invite message in one tone. <c>{code}</c>, <c>{link}</c> and <c>{days}</c> are filled in.</summary>
public sealed record ShareStyle(string Name, string Subject, string Template);

/// <summary>Ready-made invite messages, and the same message shaped for each app it's shared through.</summary>
public static class InviteMessages
{
    public static IReadOnlyList<ShareStyle> Styles { get; } =
    [
        new("Short",
            "{days} days fee-free on Otium",
            "Join Otium with my code {code} and get {days} days fee-free: {link}"),
        new("Explain it",
            "Get paid for your PC's spare power",
            "I'm getting paid for my PC's spare power with Otium. It only runs while I'm away from the computer and stops the moment I'm back. " +
            "Join with my code {code} and mine fee-free for {days} days: {link}"),
        new("For gamers",
            "Your gaming PC can earn while you're away",
            "Your gaming PC can earn money while you're not playing. Otium uses the graphics card only when you're away and stops as soon as you're back. " +
            "Use my code {code} for {days} days fee-free: {link}"),
    ];

    public static string Fill(string template, string code, string link, TimeSpan friendFeeHoliday) => template
        .Replace("{code}", code, StringComparison.Ordinal)
        .Replace("{link}", link, StringComparison.Ordinal)
        .Replace("{days}", friendFeeHoliday.TotalDays.ToString("0", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// Discord renders markdown: the code stands out in bold and the link unfurls into a preview card. The code inside
    /// the link is left alone, or the link would break.
    /// </summary>
    public static string ForDiscord(string message, string code, string link)
    {
        if (code.Length == 0)
            return message;
        const string placeholder = "\uE000link\uE000";
        var masked = link.Length > 0 ? message.Replace(link, placeholder, StringComparison.Ordinal) : message;
        return masked.Replace(code, $"**{code}**", StringComparison.Ordinal).Replace(placeholder, link, StringComparison.Ordinal);
    }

    /// <summary>A short email body. If the sender edited the code or link out of the message, they're added below it.</summary>
    public static string ForEmail(string message, string code, string link)
    {
        var body = $"Hi,\n\n{message}\n";
        if (code.Length > 0 && !message.Contains(code, StringComparison.Ordinal))
            body += $"\nMy invite code: {code}";
        if (link.Length > 0 && !message.Contains(link, StringComparison.Ordinal))
            body += $"\nDownload: {link}";
        return body;
    }

    public static string WhatsAppLink(string message) => $"https://wa.me/?text={Uri.EscapeDataString(message)}";

    public static string EmailLink(string subject, string body) =>
        $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
}

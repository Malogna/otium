using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Otium.Server.Accounts;

public sealed class EmailOptions
{
    public const string Section = "Email";

    public string SmtpHost { get; set; } = "";

    public int Port { get; set; } = 587;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public bool EnableSsl { get; set; } = true;

    public string From { get; set; } = "Otium <no-reply@otium.example>";

    public bool IsConfigured => SmtpHost.Length > 0;
}

public sealed record SentEmail(string To, string Subject, string Body, DateTimeOffset SentAt);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>Sends through any SMTP service (Postmark, SES, Mailgun, Gmail, ...).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.SmtpHost, o.Port) { EnableSsl = o.EnableSsl };
        if (o.Username.Length > 0)
            client.Credentials = new NetworkCredential(o.Username, o.Password);
        using var message = new MailMessage(MailAddress.TryCreate(o.From, out var from) ? from : new MailAddress("no-reply@otium.example"), new MailAddress(to))
        {
            Subject = subject,
            Body = body,
        };
        await client.SendMailAsync(message, ct);
    }
}

/// <summary>
/// Used when no SMTP server is configured (development): writes emails to the log and keeps the latest ones
/// in memory so the dev tools and tests can read sign-in codes.
/// </summary>
public sealed class OutboxEmailSender(ILogger<OutboxEmailSender> logger) : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        _sent.Enqueue(new SentEmail(to, subject, body, DateTimeOffset.UtcNow));
        while (_sent.Count > 100 && _sent.TryDequeue(out _))
        {
        }
        logger.LogWarning("DEV EMAIL (no SMTP configured) to {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

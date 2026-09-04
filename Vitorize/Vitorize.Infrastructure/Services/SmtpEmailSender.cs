using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Email;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services;

/// <summary>The SMTP password is deliberately supplied by server configuration, never the database
/// settings screen. Other non-secret SMTP controls remain manageable from the admin panel.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly VitorizeDbContext _dbContext;
    private readonly EmailSecretOptions _secrets;

    public SmtpEmailSender(VitorizeDbContext dbContext, IOptions<EmailSecretOptions> secrets)
    {
        _dbContext = dbContext;
        _secrets = secrets.Value;
    }

    public async Task<EmailSendResult> SendAsync(EmailOutboxPayload message, CancellationToken cancellationToken = default)
    {
        if (!TryEmail(message.Recipient, out var recipient))
            return EmailSendResult.Failed("Invalid email recipient.", retryable: false);

        var keys = new[] { "SmtpHost", "SmtpPort", "SmtpUsername", "SmtpFromEmail", "SmtpFromName", "SmtpEnableSsl" };
        var settings = await _dbContext.Settings.AsNoTracking()
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value ?? string.Empty, cancellationToken);
        var host = settings.GetValueOrDefault("SmtpHost")?.Trim();
        var username = settings.GetValueOrDefault("SmtpUsername")?.Trim();
        var fromEmail = settings.GetValueOrDefault("SmtpFromEmail")?.Trim();
        var fromName = settings.GetValueOrDefault("SmtpFromName")?.Trim();
        var port = int.TryParse(settings.GetValueOrDefault("SmtpPort"), out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(settings.GetValueOrDefault("SmtpEnableSsl"), out var parsedSsl) || parsedSsl;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(_secrets.SmtpPassword) || !TryEmail(fromEmail, out var from) ||
            port is < 1 or > 65535)
            return EmailSendResult.Skipped();

        using var mail = new MailMessage
        {
            From = new MailAddress(from.Address, string.IsNullOrWhiteSpace(fromName) ? "ویتورایز" : fromName, Encoding.UTF8),
            Subject = message.Subject.Trim(),
            SubjectEncoding = Encoding.UTF8,
            Body = message.Body.Trim(),
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        mail.To.Add(recipient);

        try
        {
            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, _secrets.SmtpPassword),
                Timeout = 20_000
            };
            await smtp.SendMailAsync(mail);
            return EmailSendResult.Sent();
        }
        catch (SmtpException ex)
        {
            var retryable = ex.StatusCode is SmtpStatusCode.GeneralFailure or SmtpStatusCode.TransactionFailed or
                SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy or SmtpStatusCode.InsufficientStorage;
            return EmailSendResult.Failed($"SMTP {ex.StatusCode}.", retryable);
        }
        catch (Exception ex) when (ex is IOException or WebException)
        {
            return EmailSendResult.Failed($"SMTP transport failure ({ex.GetType().Name}).", retryable: true);
        }
    }

    private static bool TryEmail(string? value, out MailAddress address)
    {
        try
        {
            address = new MailAddress(value?.Trim() ?? string.Empty);
            return true;
        }
        catch (FormatException)
        {
            address = null!;
            return false;
        }
    }
}

public sealed class EmailSecretOptions
{
    public const string SectionName = "Email";
    public string SmtpPassword { get; init; } = string.Empty;
}

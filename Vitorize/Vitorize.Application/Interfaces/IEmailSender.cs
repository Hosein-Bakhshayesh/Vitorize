using Vitorize.Application.Models.Email;

namespace Vitorize.Application.Interfaces;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailOutboxPayload message, CancellationToken cancellationToken = default);
}

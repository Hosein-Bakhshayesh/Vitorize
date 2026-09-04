using Vitorize.Application.Models.Email;

namespace Vitorize.Application.Interfaces;

public interface IOrderEmailOutboxEnqueuer
{
    Task EnqueuePaidOrderEmailsAsync(PaidOrderEmailRequest request, CancellationToken cancellationToken = default);
}

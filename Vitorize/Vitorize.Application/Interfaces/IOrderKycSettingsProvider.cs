using Vitorize.Application.Common;

namespace Vitorize.Application.Interfaces;

public interface IOrderKycSettingsProvider
{
    Task<OrderKycSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

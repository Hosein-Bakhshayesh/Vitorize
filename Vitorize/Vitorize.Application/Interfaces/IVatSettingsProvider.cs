using Vitorize.Application.Common;

namespace Vitorize.Application.Interfaces;

/// <summary>
/// Reads the effective VAT configuration as one immutable snapshot. Implementations must parse with
/// invariant culture and fall back to <see cref="VatSettingsSnapshot.Disabled"/> on any bad value.
/// </summary>
public interface IVatSettingsProvider
{
    Task<VatSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

namespace Vitorize.Application.Models.Payments;

/// <summary>
/// Provider data returned by Zarinpal's verification endpoint. CardPan is already
/// masked by the provider and must never contain a full card number.
/// </summary>
public sealed record ZarinpalVerificationResult(bool Success, long RefId, string? CardPan = null)
{
    // Preserves the compact deconstruction style used by existing callers that
    // only need the verification state and provider reference.
    public void Deconstruct(out bool success, out long refId)
    {
        success = Success;
        refId = RefId;
    }
}

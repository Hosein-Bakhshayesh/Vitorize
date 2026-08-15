namespace Vitorize.Shared.Enums;

/// <summary>How a purchase-snapshot KYC document may be redacted before upload.</summary>
public enum KycDocumentRedactionMode : byte
{
    None = 0,
    Optional = 1,
    Required = 2
}

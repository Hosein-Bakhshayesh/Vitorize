namespace Vitorize.Application.DTOs.Checkout;

/// <summary>Customer-safe order-total KYC configuration used only as a pre-payment notice.</summary>
public sealed class OrderKycSettingsDto
{
    public decimal ThresholdToman { get; set; }
    public string CustomerNotice { get; set; } = string.Empty;
}

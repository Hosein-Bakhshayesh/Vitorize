namespace Vitorize.Domain.Entities;

public partial class OrderItemInputValue
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? ProductInputFieldId { get; set; }
    public string FieldKey { get; set; } = null!;
    public string FieldLabel { get; set; } = null!;
    public byte FieldType { get; set; }
    public string? Value { get; set; }
    public string? EncryptedValue { get; set; }
    public bool IsSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual ProductInputField? ProductInputField { get; set; }
}

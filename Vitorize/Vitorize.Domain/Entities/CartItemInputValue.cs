namespace Vitorize.Domain.Entities;

public partial class CartItemInputValue
{
    public Guid Id { get; set; }
    public Guid CartItemId { get; set; }
    public Guid? ProductInputFieldId { get; set; }
    public string FieldKey { get; set; } = null!;
    public string FieldLabel { get; set; } = null!;
    public byte FieldType { get; set; }
    public string? Value { get; set; }
    public string? EncryptedValue { get; set; }
    public bool IsSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual CartItem CartItem { get; set; } = null!;
    public virtual ProductInputField? ProductInputField { get; set; }
}

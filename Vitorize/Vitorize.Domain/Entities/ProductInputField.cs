namespace Vitorize.Domain.Entities;

public partial class ProductInputField
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string? Description { get; set; }
    public string? Placeholder { get; set; }
    public byte FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? OptionsJson { get; set; }
    public string? DefaultValue { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? ValidationPattern { get; set; }
    public string? ValidationMessage { get; set; }
    public bool IsSensitive { get; set; }
    public bool RequiresConfirmation { get; set; }
    public byte DisplayStage { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual Product Product { get; set; } = null!;
    public virtual ICollection<CartItemInputValue> CartValues { get; set; } = new List<CartItemInputValue>();
    public virtual ICollection<OrderItemInputValue> OrderValues { get; set; } = new List<OrderItemInputValue>();
}

namespace Vitorize.Application.DTOs.Products;

public sealed class ProductImageMetadataDto
{
    public string ImagePath { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProductFeatureDto
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductInputFieldDto
{
    public Guid? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Placeholder { get; set; }
    public byte FieldType { get; set; }
    public bool IsRequired { get; set; }
    public List<string> Options { get; set; } = new();
    public string? DefaultValue { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? ValidationPattern { get; set; }
    public string? ValidationMessage { get; set; }
    public bool IsSensitive { get; set; }
    public bool RequiresConfirmation { get; set; }
    public byte DisplayStage { get; set; } = 1;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductInputValueDto
{
    public Guid? Id { get; set; }
    public Guid? ProductInputFieldId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public byte FieldType { get; set; }
    public string? Value { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsMasked { get; set; }
}

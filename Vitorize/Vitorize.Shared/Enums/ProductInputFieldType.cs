namespace Vitorize.Shared.Enums;

public enum ProductInputFieldType : byte
{
    Text = 1,
    Email = 2,
    Mobile = 3,
    Number = 4,
    Textarea = 5,
    Select = 6,
    Radio = 7,
    Checkbox = 8,
    TelegramUsername = 9,
    Url = 10,
    Date = 11,
    Secret = 12
}

public enum ProductInputStage : byte
{
    ProductPage = 1,
    Checkout = 2
}

public enum FontApplicationScope : byte
{
    Storefront = 1,
    Admin = 2,
    EntireApplication = 3
}

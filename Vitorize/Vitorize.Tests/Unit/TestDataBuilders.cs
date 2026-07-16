using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Tests.Unit;

internal sealed class UserBuilder
{
    private readonly User _user = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        FullName = "Test User",
        Mobile = "09123456789",
        PasswordHash = "not-used",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    public UserBuilder WithRole(string role)
    {
        _user.Roles.Add(new Role
        {
            Id = Guid.Parse($"{_user.Roles.Count + 1:D8}-0000-0000-0000-000000000000"),
            Name = role,
            DisplayName = role,
            CreatedAt = _user.CreatedAt
        });
        return this;
    }

    public User Build() => _user;
}

internal sealed class ProductInputFieldBuilder
{
    private readonly ProductInputFieldDto _field = new()
    {
        Key = "account_id",
        Label = "شناسه حساب",
        FieldType = (byte)ProductInputFieldType.Text,
        DisplayStage = (byte)ProductInputStage.ProductPage,
        IsActive = true
    };

    public ProductInputFieldBuilder WithType(ProductInputFieldType type)
    {
        _field.FieldType = (byte)type;
        return this;
    }

    public ProductInputFieldBuilder Required()
    {
        _field.IsRequired = true;
        return this;
    }

    public ProductInputFieldBuilder WithOptions(params string[] options)
    {
        _field.Options = options.ToList();
        return this;
    }

    public ProductInputFieldBuilder Sensitive()
    {
        _field.IsSensitive = true;
        return this;
    }

    public ProductInputFieldDto Build() => _field;
}

internal static class UnitFixtures
{
    public static JwtSettings JwtSettings => new()
    {
        SecretKey = "unit-test-signing-key-that-is-at-least-32-bytes-long",
        Issuer = "Vitorize.UnitTests",
        Audience = "Vitorize.UnitTests.Client",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    public static SmsOptions SmsOptions(bool enabled = true) => new()
    {
        IsEnabled = enabled,
        ApiKey = "unit-test-api-key",
        DefaultLineNumber = 30001234,
        MaxRetryCount = 0,
        TemplateIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SmsTemplateKeys.GenericOtp] = 101,
            [SmsTemplateKeys.LoginOtp] = 101,
            [SmsTemplateKeys.RegisterOtp] = 101,
            [SmsTemplateKeys.ForgotPassword] = 101,
            [SmsTemplateKeys.UniversalNotification] = 202,
            [SmsTemplateKeys.OrderPaid] = 202
        }
    };
}

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Infrastructure.Services.Testing;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class WholeCurrencyPaymentTests
{
    [Theory]
    [InlineData(198782.10, 198782)]
    [InlineData(198782.50, 198783)]
    [InlineData(2000000, 2000000)]
    public void Whole_units_round_half_up(decimal value, decimal expected) =>
        Assert.Equal(expected, OrderPricingCalculator.RoundMoney(value));

    [Fact]
    public async Task Manual_product_and_variant_prices_are_rounded_after_original_value_validation()
    {
        await using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var category = new Category { Id=Guid.NewGuid(), Title="money", Slug="money", IsActive=true };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var service = new AdminProductService(db, Substitute.For<IHtmlContentSanitizer>(),
            Substitute.For<IAuditService>(), Substitute.For<ICurrentUserService>());
        var created = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId=category.Id, Title="price", Slug="price", BasePrice=198782.90m,
            DiscountPrice=198782.80m, CurrencyType=2, DeliveryType=(byte)DeliveryType.Manual,
            ProductType=(byte)ProductType.Other
        });
        var product = await db.Products.SingleAsync(x=>x.Id==created.Id);
        Assert.Equal(198783m, product.BasePrice);
        Assert.Equal(198783m, product.DiscountPrice);
        var variants = new AdminProductVariantService(db, Substitute.For<IAuditService>(), Substitute.For<ICurrentUserService>());
        await variants.CreateAsync(created.Id, new CreateProductVariantRequestDto
            { Title="variant", Price=200.40m, DiscountPrice=199.50m, StockMode=(byte)ProductVariantStockMode.Manual });
        var variant = await db.ProductVariants.SingleAsync(x=>x.Title=="variant");
        Assert.Equal(200m, variant.Price);
        Assert.Equal(200m, variant.DiscountPrice);
    }

    [Fact]
    public async Task Incident_bulk_sequence_cannot_create_fractional_prices()
    {
        await using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var product = new Product
        {
            Id = Guid.NewGuid(), Title = "Stars", Slug = "stars", BasePrice = 180000m,
            CurrencyType = 2, IsActive = true,
            ProductVariants = [new ProductVariant
            {
                Id = Guid.NewGuid(), Title = "50", Price = 180000m, IsActive = true
            }]
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var service = new AdminProductService(db, Substitute.For<IHtmlContentSanitizer>(),
            Substitute.For<IAuditService>(), Substitute.For<ICurrentUserService>());
        foreach (var (operation, value) in new[] { ("increase-percent", 15m), ("decrease-percent", 3m), ("decrease-percent", 1m) })
            await service.BulkUpdateAsync(new BulkProductUpdateRequestDto
                { Ids = [product.Id], Operation = operation, Value = value });

        Assert.Equal(198782m, product.BasePrice);
        Assert.Equal(198782m, product.ProductVariants.Single().Price);
        var price = OrderPricingCalculator.Calculate(product.BasePrice, 0,
            new VatSettingsSnapshot(true, 1, VatCalculationMode.BeforeDiscount));
        Assert.Equal(1988m, price.VatAmount);
        Assert.Equal(200770m, price.FinalAmount);
    }

    [Theory]
    [InlineData(CurrencyType.Toman, "IRT")]
    [InlineData(CurrencyType.Rial, "IRR")]
    public async Task Request_and_verify_send_identical_integer_json_without_currency_conversion(CurrencyType currency, string wireCurrency)
    {
        using var handler = new CaptureHandler();
        var gateway = Gateway(handler);
        Assert.True((await gateway.CreatePaymentAsync(2000000.00m, currency, "test")).Success);
        Assert.True((await gateway.VerifyPaymentAsync("test-authority", 2000000.00m)).Success);
        Assert.Equal(2, handler.Bodies.Count);
        foreach (var body in handler.Bodies)
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("2000000", json.RootElement.GetProperty("amount").GetRawText());
        }
        using var requestJson = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal(wireCurrency, requestJson.RootElement.GetProperty("currency").GetString());
    }

    [Theory]
    [InlineData(200769.92)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9999999999999999999)]
    public async Task Invalid_amounts_never_reach_gateway_and_are_not_silently_rounded(decimal amount)
    {
        using var handler = new CaptureHandler();
        var gateway = Gateway(handler);
        Assert.False((await gateway.CreatePaymentAsync(amount, CurrencyType.Toman, "test")).Success);
        Assert.False((await gateway.VerifyPaymentAsync("test-authority", amount)).Success);
        Assert.Empty(handler.Bodies);
    }

    private static ZarinpalGatewayService Gateway(CaptureHandler handler)
    {
        var config = Substitute.For<IZarinpalPaymentConfigurationProvider>();
        config.GetAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalPaymentConfiguration(
            Guid.NewGuid().ToString(), false, new Uri("https://payment.zarinpal.com/pg/v4/payment"),
            new Uri("https://payment.zarinpal.com/pg/StartPay"), new Uri("https://store.example/api/payments/zarinpal/callback")));
        config.ValidateAsync(Arg.Any<CancellationToken>()).Returns(ZarinpalConfigurationValidation.Valid);
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = "Production";
        var faults = Substitute.For<IOptionsMonitor<TestingFaultInjectionOptions>>();
        faults.CurrentValue.Returns(new TestingFaultInjectionOptions());
        return new ZarinpalGatewayService(new HttpClient(handler), config, environment, faults,
            NullLogger<ZarinpalGatewayService>.Instance);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"code":100,"authority":"test-authority","ref_id":123}}""", Encoding.UTF8, "application/json")
            };
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.Application.DTOs.Admin.Brands;
using Vitorize.Application.DTOs.Admin.Categories;
using Vitorize.Application.DTOs.Admin.ProductImages;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.DTOs.Products;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class AdminCatalogCrudIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public AdminCatalogCrudIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Admin_can_manage_category_brand_product_metadata_variant_tag_and_images_end_to_end()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);
        var suffix = Guid.NewGuid().ToString("N");

        var category = await PostDataAsync<AdminCategoryDto>(admin, "/api/admin/categories", new CreateCategoryRequestDto
        {
            Title = "Integration Category", Slug = $"integration-category-{suffix}",
            Icon = "folder", ImageAltText = "Category image", FocusKeyword = "category keyword",
            IsActive = true
        });
        var brand = await PostDataAsync<AdminBrandDto>(admin, "/api/admin/brands", new CreateBrandRequestDto
        {
            Title = "Integration Brand", Slug = $"integration-brand-{suffix}",
            ImageAltText = "Brand image", FocusKeyword = "brand keyword", IsActive = true
        });
        var tag = await PostDataAsync<AdminProductTagDto>(admin, "/api/admin/product-tags", new SaveProductTagRequestDto
        {
            Title = "Integration Tag", Slug = $"integration-tag-{suffix}", Aliases = "alias-one,alias-two"
        });

        var productRequest = new CreateProductRequestDto
        {
            CategoryId = category.Id, BrandId = brand.Id, Title = "Integration Product",
            Slug = $"integration-product-{suffix}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 250m, DiscountPrice = 200m,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, MaxOrderQuantity = 5,
            IsActive = true, SeoTitle = "Integration SEO", FocusKeyword = "product keyword",
            ThumbnailAltText = "Product thumbnail", RedirectUrl = "/blog/integration-target", TagIds = new() { tag.Id },
            Features = new()
            {
                new ProductFeatureDto { Title = "Platform", Value = "PC", IconKey = "monitor", SortOrder = 1, IsActive = true }
            },
            InputFields = new()
            {
                new ProductInputFieldDto
                {
                    Key = "account_email", Label = "Account Email", FieldType = (byte)ProductInputFieldType.Email,
                    IsRequired = true, MaxLength = 200, DisplayStage = (byte)ProductInputStage.ProductPage,
                    SortOrder = 1, IsActive = true
                }
            }
        };
        var product = await PostDataAsync<AdminProductDto>(admin, "/api/admin/products", productRequest);
        product.Features.Should().ContainSingle(x => x.Title == "Platform" && x.IconKey == "monitor");
        product.InputFields.Should().ContainSingle(x => x.Key == "account_email");
        product.TagIds.Should().Contain(tag.Id);
        product.RedirectUrl.Should().Be("/blog/integration-target");

        var pagedResponse = await admin.GetFromJsonAsync<ApiResult<PagedResult<AdminProductDto>>>(
            $"/api/admin/products/paged?search={Uri.EscapeDataString(product.Title)}&page=1&pageSize=1");
        pagedResponse!.IsSuccess.Should().BeTrue();
        pagedResponse.Data!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Data.Items.Should().ContainSingle(x => x.Id == product.Id);

        using (var publicClient = _fixture.CreateClient())
        {
            var publicResponse = await publicClient.GetAsync($"/api/products/slug/{product.Slug}");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await publicResponse.Content.ReadFromJsonAsync<ApiResult<ProductDetailDto>>();
            payload!.Data!.RedirectUrl.Should().Be("/blog/integration-target");
            var body = await publicResponse.Content.ReadAsStringAsync();
            body.Should().Contain("Platform").And.Contain("account_email").And.Contain("Integration SEO");
        }

        var variant = await PostDataAsync<AdminProductVariantDto>(admin,
            $"/api/admin/products/{product.Id}/variants", new CreateProductVariantRequestDto
            {
                Title = "Standard", Sku = $"SKU-{suffix}", Price = 300m, DiscountPrice = 275m,
                StockMode = (byte)ProductVariantStockMode.Manual, IsDefault = true, IsActive = true
            });
        var updatedVariantResponse = await admin.PutAsJsonAsync($"/api/admin/product-variants/{variant.Id}",
            new UpdateProductVariantRequestDto
            {
                Title = "Standard Updated", Sku = $"SKU-{suffix}", Price = 325m,
                StockMode = (byte)ProductVariantStockMode.Manual, IsDefault = true, IsActive = true
            });
        updatedVariantResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var image = await PostDataAsync<AdminProductImageDto>(admin, $"/api/admin/products/{product.Id}/images",
            new CreateProductImageRequestDto
            {
                ImagePath = $"/uploads/products/{suffix}.png", AltText = "Integration product image",
                SortOrder = 2, SetAsThumbnail = true
            });
        (await admin.PutAsJsonAsync($"/api/admin/product-images/{image.Id}",
            new UpdateProductImageRequestDto { AltText = "Updated alt", SortOrder = 1 })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/admin/product-images/{image.Id}/set-thumbnail", null)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        productRequest.Title = "Integration Product Updated";
        productRequest.Features[0].Value = "Windows";
        productRequest.InputFields[0].Label = "Updated Account Email";
        (await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", productRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = _fixture.CreateDbContext())
        {
            var stored = await db.Products.Include(x => x.ProductFeatures).Include(x => x.ProductInputFields)
                .Include(x => x.Tags).Include(x => x.ProductImages).Include(x => x.ProductVariants)
                .SingleAsync(x => x.Id == product.Id);
            stored.Title.Should().Be("Integration Product Updated");
            stored.ProductFeatures.Should().ContainSingle(x => x.Value == "Windows");
            stored.ProductInputFields.Should().ContainSingle(x => x.Label == "Updated Account Email");
            stored.Tags.Should().ContainSingle(x => x.Id == tag.Id);
            stored.ProductImages.Should().ContainSingle(x => x.Id == image.Id && x.AltText == "Updated alt");
            stored.ProductVariants.Should().ContainSingle(x => x.Id == variant.Id && x.Title == "Standard Updated");
        }

        productRequest.TagIds.Clear();
        (await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", productRequest)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/product-images/{image.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/product-variants/{variant.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/products/{product.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/product-tags/{tag.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/brands/{brand.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/categories/{category.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Storefront_catalog_applies_type_delivery_verification_and_discount_filters_before_paging()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = $"Catalog {suffix}", Slug = $"catalog-filter-{suffix}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var matching = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = $"Matching {suffix}",
            Slug = $"matching-filter-{suffix}", ProductType = (byte)ProductType.GameAccount,
            DeliveryType = (byte)DeliveryType.Instant, RequiresVerification = true,
            BasePrice = 100m, DiscountPrice = 70m, CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var excluded = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = $"Excluded {suffix}",
            Slug = $"excluded-filter-{suffix}", ProductType = (byte)ProductType.GameAccount,
            DeliveryType = (byte)DeliveryType.Manual, RequiresVerification = true,
            BasePrice = 100m, DiscountPrice = 70m, CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        await using (var db = _fixture.CreateDbContext())
        {
            db.AddRange(category, matching, excluded);
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var response = await client.GetFromJsonAsync<ApiResult<PagedResult<ProductListItemDto>>>(
            "/api/products?productTypes=2&deliveryType=1&requiresVerification=true&minDiscountPercent=25&page=1&pageSize=1");

        response!.IsSuccess.Should().BeTrue();
        response.Data!.TotalCount.Should().Be(1);
        response.Data.Items.Should().ContainSingle(x => x.Id == matching.Id);
    }

    [Fact]
    public async Task Duplicate_slugs_and_invalid_Lucide_icons_are_rejected()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);
        var slug = $"duplicate-{Guid.NewGuid():N}";
        var request = new CreateCategoryRequestDto { Title = "First", Slug = slug, IsActive = true };
        (await admin.PostAsJsonAsync("/api/admin/categories", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        request.Title = "Second";
        (await admin.PostAsJsonAsync("/api/admin/categories", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        request.Title = "Bad Icon"; request.Slug = $"bad-icon-{Guid.NewGuid():N}"; request.Icon = "<script>alert(1)</script>";
        (await admin.PostAsJsonAsync("/api/admin/categories", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Product_variant_and_media_detail_lists_are_paged_and_authorized()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var suffix = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Paged details", Slug = $"paged-details-{suffix}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Paged product details",
            Slug = $"paged-product-details-{suffix}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 10m, CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        await using (var db = _fixture.CreateDbContext())
        {
            db.Categories.Add(category);
            db.Products.Add(product);
            db.ProductVariants.AddRange(Enumerable.Range(1, 105).Select(index => new ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Title = $"Variant {index:000}",
                Price = 10m, StockMode = (byte)ProductVariantStockMode.Manual, IsActive = true,
                SortOrder = index, CreatedAt = product.CreatedAt
            }));
            db.ProductImages.AddRange(Enumerable.Range(1, 55).Select(index => new ProductImage
            {
                Id = Guid.NewGuid(), ProductId = product.Id, ImagePath = $"/uploads/paged-{suffix}-{index:000}.png",
                SortOrder = index, CreatedAt = product.CreatedAt.AddMinutes(index)
            }));
            await db.SaveChangesAsync();
        }

        using var admin = _fixture.CreateClient(adminToken);
        using var customer = _fixture.CreateClient(customerToken);
        (await customer.GetAsync($"/api/admin/products/{product.Id}/variants/paged")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var variants = await admin.GetFromJsonAsync<ApiResult<PagedResult<AdminProductVariantDto>>>(
            $"/api/admin/products/{product.Id}/variants/paged?page=1&pageSize=20");
        var variantLast = await admin.GetFromJsonAsync<ApiResult<PagedResult<AdminProductVariantDto>>>(
            $"/api/admin/products/{product.Id}/variants/paged?page=6&pageSize=20");
        var images = await admin.GetFromJsonAsync<ApiResult<PagedResult<AdminProductImageDto>>>(
            $"/api/admin/products/{product.Id}/images/paged?page=1&pageSize=20");
        var imageLast = await admin.GetFromJsonAsync<ApiResult<PagedResult<AdminProductImageDto>>>(
            $"/api/admin/products/{product.Id}/images/paged?page=3&pageSize=20");

        variants!.Data!.TotalCount.Should().Be(105); variants.Data.Items.Should().HaveCount(20);
        variantLast!.Data!.Items.Should().HaveCount(5);
        images!.Data!.TotalCount.Should().Be(55); images.Data.Items.Should().HaveCount(20);
        imageLast!.Data!.Items.Should().HaveCount(15);

        var lookup = await admin.GetFromJsonAsync<ApiResult<List<AdminProductVariantLookupDto>>>(
            $"/api/admin/products/{product.Id}/variants/lookup?search=Variant");
        lookup!.Data.Should().HaveCount(100);
        lookup.Data.Should().OnlyContain(x => x.Title.StartsWith("Variant") && x.Sku == null);
        var selectedVariantId = variantLast.Data.Items.First().Id;
        var hydratedSelection = await admin.GetFromJsonAsync<ApiResult<List<AdminProductVariantLookupDto>>>(
            $"/api/admin/products/{product.Id}/variants/lookup?search=no-match&selectedId={selectedVariantId}");
        hydratedSelection!.Data!.Select(x => x.Id).Should().ContainSingle().Which.Should().Be(selectedVariantId);
    }

    [Fact]
    public async Task Selected_product_export_validates_the_entire_request_and_returns_a_safe_deterministic_projection()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var suffix = Guid.NewGuid().ToString("N");
        var category = new Category { Id = Guid.NewGuid(), Title = "Export category", Slug = $"export-category-{suffix}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var first = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "=HYPERLINK(\"http://example.test\",\"open\")", Slug = $"export-a-{suffix}", ProductType = 1, DeliveryType = 2, BasePrice = 10m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        var second = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Zulu", Slug = $"export-z-{suffix}", ProductType = 1, DeliveryType = 2, BasePrice = 10m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        await using (var db = _fixture.CreateDbContext()) { db.AddRange(category, first, second); await db.SaveChangesAsync(); }
        using var admin = _fixture.CreateClient(adminToken);
        using var customer = _fixture.CreateClient(customerToken);

        var valid = await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = new[] { second.Id, first.Id } });
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await valid.Content.ReadFromJsonAsync<ApiResult<List<AdminProductDto>>>();
        body!.Data!.Select(x => x.Id).Should().Equal(first.Id, second.Id);
        (await customer.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = new[] { first.Id } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = new[] { first.Id, first.Id } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = new[] { first.Id, Guid.NewGuid() } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = new[] { first.Id, Guid.Empty } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/products/export-selection", new { Ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Selected_order_export_validates_the_entire_request_and_returns_a_safe_deterministic_projection()
    {
        var (adminUser, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var older = new Order
        {
            Id = Guid.NewGuid(), UserId = adminUser.Id, OrderNumber = $"VT-EXPORT-OLD-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            SubtotalAmount = 10, FinalAmount = 10, CurrencyType = (byte)CurrencyType.Toman,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var newer = new Order
        {
            Id = Guid.NewGuid(), UserId = adminUser.Id, OrderNumber = $"VT-EXPORT-NEW-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
            SubtotalAmount = 20, FinalAmount = 20, CurrencyType = (byte)CurrencyType.Toman,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        await using (var db = _fixture.CreateDbContext()) { db.Orders.AddRange(older, newer); await db.SaveChangesAsync(); }
        using var admin = _fixture.CreateClient(adminToken);
        using var customer = _fixture.CreateClient(customerToken);

        var valid = await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = new[] { older.Id, newer.Id } });
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await valid.Content.ReadFromJsonAsync<ApiResult<List<OrderDto>>>();
        body!.Data!.Select(x => x.Id).Should().Equal(newer.Id, older.Id);
        // The export projection stays lightweight (no order items), but since FIX-13 it carries the
        // financial summary the Admin CSV exports: subtotal, discount and the VAT snapshot.
        body.Data.Should().OnlyContain(x => x.Items.Count == 0);
        body.Data!.Select(x => x.SubtotalAmount).Should().Equal(20m, 10m);
        body.Data.Should().OnlyContain(x => x.DiscountAmount == 0m);
        body.Data.Should().OnlyContain(x => !x.VatEnabled && x.VatAmount == 0m);

        (await customer.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = new[] { older.Id } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = new[] { older.Id, older.Id } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = new[] { older.Id, Guid.Empty } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = new[] { older.Id, Guid.NewGuid() } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/orders/export-selection", new { Ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<T> PostDataAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return (System.Text.Json.JsonSerializer.Deserialize<ApiResult<T>>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!.Data!;
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Brands;
using Vitorize.Application.DTOs.Admin.Categories;
using Vitorize.Application.DTOs.Admin.ProductImages;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Admin.ProductVariants;
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
            ThumbnailAltText = "Product thumbnail", TagIds = new() { tag.Id },
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

        using (var publicClient = _fixture.CreateClient())
        {
            var publicResponse = await publicClient.GetAsync($"/api/products/slug/{product.Slug}");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private static async Task<T> PostDataAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return (System.Text.Json.JsonSerializer.Deserialize<ApiResult<T>>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!.Data!;
    }
}

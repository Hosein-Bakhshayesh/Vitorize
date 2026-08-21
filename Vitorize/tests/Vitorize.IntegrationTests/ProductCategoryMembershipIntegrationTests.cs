using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// A product may belong to several categories. Membership lives in ProductCategories and is the only
/// thing category filtering reads; Product.CategoryId stays as the primary category for the
/// breadcrumb and canonical URL and is always a member, so the two can never disagree.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class ProductCategoryMembershipIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public ProductCategoryMembershipIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_product_assigned_to_three_categories_appears_in_each_exactly_once()
    {
        var (a, b, c) = await SeedCategoriesAsync();
        var product = await CreateProductAsync("multi", a, new[] { b, c });
        var other = await CreateProductAsync("single", b, Array.Empty<Guid>());

        (await ListAsync(a)).Should().ContainSingle(x => x == product, "the primary category lists it once");
        (await ListAsync(b)).Should().Contain(product).And.HaveCount(2, "both products belong to B");
        (await ListAsync(c)).Should().ContainSingle(x => x == product);

        // The single-category product must not leak into a category it was never assigned to.
        (await ListAsync(a)).Should().NotContain(other);
        (await ListAsync(c)).Should().NotContain(other);
    }

    [Fact]
    public async Task Removing_one_category_only_affects_that_listing()
    {
        var (a, b, c) = await SeedCategoriesAsync();
        var product = await CreateProductAsync("removal", a, new[] { b, c });

        await UpdateCategoriesAsync(product, primary: a, additional: new[] { c });

        (await ListAsync(b)).Should().NotContain(product, "category B was deselected");
        (await ListAsync(a)).Should().Contain(product, "the primary is untouched");
        (await ListAsync(c)).Should().Contain(product, "an unrelated membership is untouched");
    }

    [Fact]
    public async Task The_primary_category_is_always_a_member_even_when_the_caller_omits_it()
    {
        var (a, b, _) = await SeedCategoriesAsync();

        // The request lists only B, but A is the primary, so A must still be a membership.
        var product = await CreateProductAsync("implicit-primary", a, new[] { b });

        await using var db = _fixture.CreateDbContext();
        var memberships = await db.ProductCategories.Where(x => x.ProductId == product)
            .Select(x => x.CategoryId).ToListAsync();
        memberships.Should().Contain(a).And.Contain(b);
    }

    [Fact]
    public async Task Changing_the_primary_category_does_not_leave_the_old_one_behind_as_a_membership()
    {
        var (a, b, c) = await SeedCategoriesAsync();
        var product = await CreateProductAsync("reprimary", a, Array.Empty<Guid>());

        // Move the product's primary from A to B without listing A anywhere.
        await UpdateCategoriesAsync(product, primary: b, additional: new[] { c });

        await using var db = _fixture.CreateDbContext();
        var memberships = await db.ProductCategories.Where(x => x.ProductId == product)
            .Select(x => x.CategoryId).ToListAsync();
        memberships.Should().BeEquivalentTo(new[] { b, c });
        memberships.Should().NotContain(a, "the previous primary was not re-selected, so it is gone");
    }

    [Fact]
    public async Task A_category_that_is_inactive_cannot_be_attached()
    {
        var (a, _, _) = await SeedCategoriesAsync();

        Guid disabled;
        await using (var db = _fixture.CreateDbContext())
        {
            var category = new Category
            {
                Id = Guid.NewGuid(), Title = "disabled", Slug = $"disabled-{Guid.NewGuid():N}",
                IsActive = false, IsDeleted = false, CreatedAt = DateTime.UtcNow
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            disabled = category.Id;
        }

        var act = () => CreateProductAsync("bad-category", a, new[] { disabled });
        await act.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(Guid A, Guid B, Guid C)> SeedCategoriesAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var category = new Category
            {
                Id = Guid.NewGuid(), Title = $"cat-{i}-{Guid.NewGuid():N}"[..12],
                Slug = $"cat-{Guid.NewGuid():N}", IsActive = true, IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
            db.Categories.Add(category);
            ids.Add(category.Id);
        }
        await db.SaveChangesAsync();
        return (ids[0], ids[1], ids[2]);
    }

    private async Task<Guid> CreateProductAsync(string label, Guid primary, IReadOnlyCollection<Guid> additional)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminProductService>();
        var created = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = primary,
            CategoryIds = additional.ToList(),
            Title = $"{label} product",
            Slug = $"{label}-{Guid.NewGuid():N}",
            ProductType = 1,
            DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = 1_000m,
            CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1,
            IsActive = true
        });
        return created.Id;
    }

    private async Task UpdateCategoriesAsync(Guid productId, Guid primary, IReadOnlyCollection<Guid> additional)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminProductService>();
        var current = await service.GetByIdAsync(productId);

        await service.UpdateAsync(productId, new UpdateProductRequestDto
        {
            CategoryId = primary,
            CategoryIds = additional.ToList(),
            Title = current.Title,
            Slug = current.Slug,
            ProductType = current.ProductType,
            DeliveryType = current.DeliveryType,
            BasePrice = current.BasePrice,
            CurrencyType = current.CurrencyType,
            MinOrderQuantity = current.MinOrderQuantity,
            IsActive = current.IsActive
        });
    }

    /// <summary>Product ids the storefront lists for a category, through the real filter.</summary>
    private async Task<List<Guid>> ListAsync(Guid categoryId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var page = await products.GetProductsAsync(new ProductFilterDto
        {
            CategoryId = categoryId, Page = 1, PageSize = 50
        });
        return page.Items.Select(x => x.Id).ToList();
    }
}

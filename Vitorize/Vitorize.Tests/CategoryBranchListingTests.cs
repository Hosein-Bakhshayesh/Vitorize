using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Products;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Filtering the storefront by a category used to match that id exactly, so a category that only
/// groups its children listed nothing — and the redesigned menus link straight to those categories.
/// A listing now covers the whole branch, without pulling in categories that merely sit alongside it.
/// </summary>
public sealed class CategoryBranchListingTests
{
    [Fact]
    public async Task A_parent_lists_the_products_of_its_children()
    {
        await using var db = NewContext();

        var parent = Category("والد");
        var childA = Category("فرزند الف", parent.Id);
        var childB = Category("فرزند ب", parent.Id);
        db.Categories.AddRange(parent, childA, childB);
        db.Products.AddRange(Product("زیر الف", childA.Id), Product("زیر ب", childB.Id));
        await db.SaveChangesAsync();

        var titles = await ListAsync(db, parent.Id);

        titles.Should().BeEquivalentTo("زیر الف", "زیر ب");
    }

    [Fact]
    public async Task A_grandparent_reaches_three_levels_down()
    {
        await using var db = NewContext();

        var root = Category("ریشه");
        var branch = Category("شاخه", root.Id);
        var leaf = Category("برگ", branch.Id);
        db.Categories.AddRange(root, branch, leaf);
        db.Products.Add(Product("عمیق", leaf.Id));
        await db.SaveChangesAsync();

        (await ListAsync(db, root.Id)).Should().ContainSingle().Which.Should().Be("عمیق");
    }

    [Fact]
    public async Task A_sibling_branch_does_not_leak_in()
    {
        await using var db = NewContext();

        var left = Category("چپ");
        var leftChild = Category("فرزند چپ", left.Id);
        var right = Category("راست");
        db.Categories.AddRange(left, leftChild, right);
        db.Products.AddRange(Product("متعلق به چپ", leftChild.Id), Product("متعلق به راست", right.Id));
        await db.SaveChangesAsync();

        (await ListAsync(db, left.Id)).Should().BeEquivalentTo("متعلق به چپ");
        (await ListAsync(db, right.Id)).Should().BeEquivalentTo("متعلق به راست");
    }

    [Fact]
    public async Task A_leaf_still_lists_only_its_own_products()
    {
        await using var db = NewContext();

        var parent = Category("والد");
        var child = Category("فرزند", parent.Id);
        db.Categories.AddRange(parent, child);
        db.Products.AddRange(Product("مالِ والد", parent.Id), Product("مالِ فرزند", child.Id));
        await db.SaveChangesAsync();

        (await ListAsync(db, child.Id)).Should().BeEquivalentTo("مالِ فرزند");
        (await ListAsync(db, parent.Id)).Should().BeEquivalentTo("مالِ والد", "مالِ فرزند");
    }

    [Fact]
    public async Task A_product_in_two_categories_of_one_branch_is_listed_once()
    {
        await using var db = NewContext();

        var parent = Category("والد");
        var childA = Category("فرزند الف", parent.Id);
        var childB = Category("فرزند ب", parent.Id);
        db.Categories.AddRange(parent, childA, childB);

        var shared = Product("مشترک", childA.Id);
        db.Products.Add(shared);
        db.ProductCategories.Add(new ProductCategory
        {
            ProductId = shared.Id,
            CategoryId = childB.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Widening the filter must not turn one product into two rows or inflate the total.
        var result = await Service(db).GetProductsAsync(new ProductFilterDto { CategoryId = parent.Id });
        result.Items.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task A_branch_with_no_products_still_lists_nothing()
    {
        await using var db = NewContext();

        var root = Category("ریشه بی‌محصول");
        db.Categories.AddRange(root, Category("زیرشاخه بی‌محصول", root.Id));
        await db.SaveChangesAsync();

        (await ListAsync(db, root.Id)).Should().BeEmpty();
    }

    private static async Task<List<string>> ListAsync(VitorizeDbContext db, Guid categoryId)
    {
        var result = await Service(db).GetProductsAsync(new ProductFilterDto { CategoryId = categoryId });
        return result.Items.Select(x => x.Title).ToList();
    }

    private static ProductService Service(VitorizeDbContext db) => new(db, new PassThroughSanitizer());

    private static VitorizeDbContext NewContext() =>
        new(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"category-branch-{Guid.NewGuid():N}").Options);

    private static Category Category(string title, Guid? parentId = null) => new()
    {
        Id = Guid.NewGuid(),
        ParentId = parentId,
        Title = title,
        Slug = Guid.NewGuid().ToString("N"),
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static Product Product(string title, Guid categoryId) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Slug = Guid.NewGuid().ToString("N"),
        CategoryId = categoryId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class PassThroughSanitizer : Vitorize.Application.Interfaces.IHtmlContentSanitizer
    {
        public string Sanitize(string? html) => html ?? string.Empty;
    }
}

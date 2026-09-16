using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The category list showed a product and a sub-category column that were never populated, so every
/// row read zero however full the catalogue was. These cover the counts an administrator relies on to
/// tell a genuinely empty branch from one whose products merely sit a level deeper.
/// </summary>
public sealed class AdminCategoryCountTests
{
    [Fact]
    public async Task Counts_direct_products_children_and_the_whole_subtree()
    {
        await using var db = NewContext();

        var root = Category("ریشه");
        var branch = Category("شاخه", root.Id);
        var leafA = Category("برگ الف", branch.Id);
        var leafB = Category("برگ ب", branch.Id);
        db.Categories.AddRange(root, branch, leafA, leafB);

        db.Products.AddRange(
            Product("مستقیمِ ریشه", root.Id),
            Product("زیر برگ الف", leafA.Id),
            Product("زیر برگ ب", leafB.Id));
        await db.SaveChangesAsync();

        var rows = await new AdminCategoryService(db).GetAllAsync();

        Row(rows, "ریشه").ProductCount.Should().Be(1);
        Row(rows, "ریشه").ChildrenCount.Should().Be(1, "only the direct child counts, not the leaves below it");
        Row(rows, "ریشه").SubtreeProductCount.Should().Be(3);

        // The case the column exists for: nothing of its own, yet its pages are not empty.
        Row(rows, "شاخه").ProductCount.Should().Be(0);
        Row(rows, "شاخه").ChildrenCount.Should().Be(2);
        Row(rows, "شاخه").SubtreeProductCount.Should().Be(2);

        Row(rows, "برگ الف").SubtreeProductCount.Should().Be(1);
    }

    [Fact]
    public async Task A_product_filed_under_two_categories_of_one_branch_is_counted_once()
    {
        await using var db = NewContext();

        var root = Category("ریشه");
        var leafA = Category("برگ الف", root.Id);
        var leafB = Category("برگ ب", root.Id);
        db.Categories.AddRange(root, leafA, leafB);

        // Primary category on one leaf, an extra membership on its sibling.
        var shared = Product("محصول مشترک", leafA.Id);
        db.Products.Add(shared);
        db.ProductCategories.Add(new ProductCategory
        {
            ProductId = shared.Id,
            CategoryId = leafB.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var rows = await new AdminCategoryService(db).GetAllAsync();

        Row(rows, "برگ الف").ProductCount.Should().Be(1);
        Row(rows, "برگ ب").ProductCount.Should().Be(1, "the many-to-many membership counts as a direct link");
        Row(rows, "ریشه").SubtreeProductCount.Should().Be(1, "summing the branches would have reported two");
    }

    [Fact]
    public async Task Inactive_and_deleted_products_are_left_out()
    {
        await using var db = NewContext();

        var root = Category("ریشه");
        db.Categories.Add(root);

        var live = Product("فعال", root.Id);
        var hidden = Product("غیرفعال", root.Id);
        hidden.IsActive = false;
        var removed = Product("حذف‌شده", root.Id);
        removed.IsDeleted = true;
        db.Products.AddRange(live, hidden, removed);
        await db.SaveChangesAsync();

        var rows = await new AdminCategoryService(db).GetAllAsync();

        // Neither one keeps a customer from landing on an empty page, so neither is counted.
        Row(rows, "ریشه").ProductCount.Should().Be(1);
        Row(rows, "ریشه").SubtreeProductCount.Should().Be(1);
    }

    [Fact]
    public async Task A_branch_with_no_products_anywhere_reports_zero()
    {
        await using var db = NewContext();

        var root = Category("ریشه بی‌محصول");
        db.Categories.AddRange(root, Category("زیرشاخه بی‌محصول", root.Id));
        await db.SaveChangesAsync();

        var rows = await new AdminCategoryService(db).GetAllAsync();

        Row(rows, "ریشه بی‌محصول").ChildrenCount.Should().Be(1);
        Row(rows, "ریشه بی‌محصول").SubtreeProductCount.Should().Be(0);
    }

    private static VitorizeDbContext NewContext() =>
        new(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"admin-category-counts-{Guid.NewGuid():N}").Options);

    private static Vitorize.Application.DTOs.Admin.Categories.AdminCategoryDto Row(
        IEnumerable<Vitorize.Application.DTOs.Admin.Categories.AdminCategoryDto> rows, string title) =>
        rows.Single(x => x.Title == title);

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
}

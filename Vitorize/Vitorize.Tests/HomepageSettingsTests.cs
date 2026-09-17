using FluentAssertions;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Section headings and counts were literals in the page, so an administrator could change the hero
/// copy but not the words directly beneath it. Counts are clamped rather than trusted: a stray value
/// in the settings table must not be able to empty a section or flood it.
/// </summary>
public sealed class HomepageSettingsTests
{
    [Fact]
    public void Section_headings_fall_back_to_the_shipped_copy()
    {
        var branding = Branding();

        branding.HomeCategoriesTitle.Should().Be("دسته‌بندی‌های محبوب ویتورایز");
        branding.HomeProductsTitle.Should().Be("محصولات پرفروش ویتورایز");
        branding.HomeBlogTitle.Should().Be("جدیدترین بلاگ‌ها");
        branding.HomeReviewsTitle.Should().Be("نظرات شما");
        branding.HomeFaqTitle.Should().Be("سوالات پرتکرار شما");
    }

    [Fact]
    public void A_configured_heading_wins()
    {
        Branding(("HomeBlogTitle", "تازه‌ها")).HomeBlogTitle.Should().Be("تازه‌ها");
    }

    [Theory]
    [InlineData("", 8)]
    [InlineData("not a number", 8)]
    [InlineData("0", 8)]
    [InlineData("-3", 8)]
    [InlineData("5", 5)]
    [InlineData("999", 24)]
    public void The_category_count_is_clamped(string stored, int expected)
    {
        // Zero or nonsense means "unset", not "show nothing"; an absurd number is capped rather than
        // allowed to print the whole catalogue across the homepage.
        Branding(("HomeCategoryCount", stored)).HomeCategoryCount.Should().Be(expected);
    }

    [Theory]
    [InlineData("", 3)]
    [InlineData("0", 3)]
    [InlineData("2", 2)]
    [InlineData("500", 12)]
    public void The_blog_count_is_clamped(string stored, int expected)
    {
        Branding(("HomeBlogCount", stored)).HomeBlogCount.Should().Be(expected);
    }

    private static StoreBranding Branding(params (string Key, string Value)[] settings)
    {
        var map = new Dictionary<string, string>();
        foreach (var (key, value) in settings) map[key] = value;
        return new StoreBranding(map);
    }
}

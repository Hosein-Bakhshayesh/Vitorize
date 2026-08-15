using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-14 (Client Issue #1). Slug contract for CMS pages: custom slugs may never collide with a
/// real storefront/admin route, and the four system slugs are a protected identity.
/// </summary>
public sealed class Fix14PageSlugRulesTests
{
    [Theory]
    [InlineData("company-story", "company-story")]
    [InlineData("  Company-Story  ", "company-story")]
    [InlineData("RETURNS", "returns")]
    public void A_valid_slug_is_trimmed_and_lower_cased(string input, string expected) =>
        PageSlugRules.NormalizeForCustomPage(input).Should().Be(expected);

    [Fact]
    public void Casing_never_produces_two_distinct_slugs()
    {
        PageSlugRules.NormalizeForCustomPage("About-Us")
            .Should().Be(PageSlugRules.NormalizeForCustomPage("about-us"));
    }

    [Fact]
    public void A_persian_slug_is_preserved()
    {
        // The storefront route accepts Persian and the caller URL-escapes it.
        PageSlugRules.NormalizeForCustomPage(" راهنمای-خرید ").Should().Be("راهنمای-خرید");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("customer")]
    [InlineData("api")]
    [InlineData("login")]
    [InlineData("cart")]
    [InlineData("checkout")]
    [InlineData("payment")]
    [InlineData("product")]
    [InlineData("products")]
    [InlineData("shop")]
    [InlineData("search")]
    [InlineData("category")]
    [InlineData("categories")]
    [InlineData("brand")]
    [InlineData("blog")]
    [InlineData("faq")]
    [InlineData("page")]
    [InlineData("sitemaps")]
    [InlineData("_blazor")]
    [InlineData("uploads")]
    public void A_reserved_route_cannot_be_used_as_a_custom_slug(string slug) =>
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage(slug))
            .Should().Throw<BusinessException>();

    [Theory]
    [InlineData("about")]
    [InlineData("terms")]
    [InlineData("privacy")]
    [InlineData("contact")]
    public void A_system_slug_cannot_be_claimed_by_a_new_custom_page(string slug)
    {
        PageSlugRules.IsSystemSlug(slug).Should().BeTrue();
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage(slug))
            .Should().Throw<BusinessException>("a second conflicting system identity must be impossible");
    }

    [Fact]
    public void Reserved_matching_ignores_casing_and_surrounding_space() =>
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage("  ADMIN "))
            .Should().Throw<BusinessException>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_slug_is_rejected(string? slug) =>
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage(slug))
            .Should().Throw<BusinessException>();

    [Theory]
    [InlineData("two words")]
    [InlineData("nested/path")]
    [InlineData("back\\slash")]
    [InlineData("query?x=1")]
    [InlineData("frag#ment")]
    public void Unsafe_url_characters_are_rejected(string slug) =>
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage(slug))
            .Should().Throw<BusinessException>();

    [Fact]
    public void An_over_long_slug_is_rejected() =>
        FluentActions.Invoking(() => PageSlugRules.NormalizeForCustomPage(new string('a', 251)))
            .Should().Throw<BusinessException>();

    [Fact]
    public void The_system_slug_set_is_exactly_the_four_seeded_pages()
    {
        PageSlugRules.System.All.Should().BeEquivalentTo("about", "terms", "privacy", "contact");
        PageSlugRules.IsSystemSlug("company-story").Should().BeFalse();
        PageSlugRules.IsSystemSlug(null).Should().BeFalse();
    }
}

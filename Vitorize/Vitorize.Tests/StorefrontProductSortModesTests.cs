using Vitorize.Shared.Storefront;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The contract the administrator's storefront-ordering choice is stored under. The important
/// property is that nothing a database can hold - a missing row, a blank value, a mode removed in a
/// later release - can produce an arbitrary order or an exception; it always resolves to a real,
/// deterministic mode.
/// </summary>
public class StorefrontProductSortModesTests
{
    [Fact]
    public void The_default_is_availability_first()
    {
        Assert.Equal("AvailabilityFirst", StorefrontProductSortModes.Default);
        Assert.Equal("availability", StorefrontProductSortModes.ToQueryKey(StorefrontProductSortModes.Default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotAMode")]
    [InlineData("Popular")]          // deliberately unsupported: Vitorize stores no popularity signal
    [InlineData("SortOrder")]
    [InlineData("'; DROP TABLE Settings--")]
    public void An_unusable_stored_value_falls_back_to_the_default(string? stored)
    {
        Assert.False(StorefrontProductSortModes.IsSupported(stored));
        Assert.Equal(StorefrontProductSortModes.Default, StorefrontProductSortModes.Normalize(stored));
        Assert.Equal("availability", StorefrontProductSortModes.ToQueryKey(stored));
    }

    [Theory]
    [InlineData("availabilityfirst", "AvailabilityFirst")]
    [InlineData("  Newest  ", "Newest")]
    [InlineData("PRICELOWTOHIGH", "PriceLowToHigh")]
    public void A_stored_value_is_matched_case_insensitively_and_trimmed(string stored, string expected) =>
        Assert.Equal(expected, StorefrontProductSortModes.Normalize(stored));

    [Theory]
    [InlineData("AvailabilityFirst", "availability")]
    [InlineData("BestSelling", "bestselling")]
    [InlineData("Newest", "newest")]
    [InlineData("Oldest", "oldest")]
    [InlineData("PriceLowToHigh", "cheapest")]
    [InlineData("PriceHighToLow", "expensive")]
    [InlineData("MostDiscounted", "discount")]
    public void Every_supported_mode_maps_to_the_query_key_the_listing_understands(string code, string queryKey)
    {
        Assert.True(StorefrontProductSortModes.IsSupported(code));
        Assert.Equal(queryKey, StorefrontProductSortModes.ToQueryKey(code));
        Assert.Equal(code, StorefrontProductSortModes.FromQueryKey(queryKey));
    }

    [Fact]
    public void Popularity_is_not_offered_because_no_popularity_signal_exists()
    {
        // Guards the judgement call: Vitorize records no view count, purchase count or ranking
        // score, so a "most popular" order would rank by nothing. Best-selling is offered instead
        // because paid-order quantity is real. If a popularity signal is ever added, this test is
        // the place that should fail and force the decision to be revisited.
        Assert.DoesNotContain(StorefrontProductSortModes.All, x => x.Key == "Popular");
        Assert.Contains(StorefrontProductSortModes.All, x => x.Key == "BestSelling");
    }

    [Fact]
    public void Every_mode_has_a_distinct_query_key_and_a_persian_label()
    {
        var modes = StorefrontProductSortModes.All.ToList();
        Assert.Equal(modes.Count, modes.Select(x => StorefrontProductSortModes.ToQueryKey(x.Key)).Distinct().Count());
        Assert.All(modes, mode =>
        {
            Assert.False(string.IsNullOrWhiteSpace(mode.Value));
            Assert.Equal(mode.Value, StorefrontProductSortModes.Label(mode.Key));
        });
    }

    [Fact]
    public void An_unknown_query_key_is_not_mistaken_for_a_mode() =>
        Assert.Null(StorefrontProductSortModes.FromQueryKey("random"));
}

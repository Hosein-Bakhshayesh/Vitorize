using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Banners;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Web.Models.Admin.Banners;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Which frame a banner lands in comes from its position and sort order together, and the homepage
/// draws only the first four hero tiles. The admin form now picks a slot rather than a raw number,
/// so the mapping between the two has to hold in both directions.
/// </summary>
public sealed class AdminBannerSlotTests
{
    [Theory]
    [InlineData(0, "hero-1")]
    [InlineData(1, "hero-2")]
    [InlineData(2, "hero-3")]
    [InlineData(3, "hero-4")]
    public void Each_hero_sort_order_resolves_to_its_tile(int sortOrder, string expectedKey)
    {
        AdminBannerSlot.Resolve("home-hero", sortOrder)!.Key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void A_hero_banner_past_the_fourth_tile_resolves_to_nothing(int sortOrder)
    {
        // The homepage reads four tiles, so such a row is configured but never drawn; the list has
        // to be able to say so instead of showing a tile number that does not exist.
        AdminBannerSlot.Resolve("home-hero", sortOrder).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void The_middle_banner_accepts_any_order_because_it_is_a_slider(int sortOrder)
    {
        var slot = AdminBannerSlot.Resolve("home-secondary", sortOrder);

        slot!.Key.Should().Be("slider");
        slot.IsHeroTile.Should().BeFalse("its order is the slide sequence, not a frame");
    }

    [Fact]
    public void Choosing_a_tile_pins_the_sort_order_that_puts_a_banner_there()
    {
        foreach (var slot in AdminBannerSlot.All.Where(x => x.IsHeroTile))
            AdminBannerSlot.Resolve(slot.Position, slot.FixedSortOrder!.Value)!.Key.Should().Be(slot.Key);
    }

    [Fact]
    public async Task Alt_text_survives_a_create_and_read_round_trip()
    {
        // The columns and the storefront always had alt text; the admin DTO and service did not carry
        // it, so whatever an administrator typed could never reach the page.
        await using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"banner-alt-{Guid.NewGuid():N}").Options);
        var service = new AdminBannerService(db);

        var created = await service.CreateAsync(new CreateBannerRequestDto
        {
            Title = "جشنواره",
            ImagePath = "/media/banner.webp",
            MobileImagePath = "/media/banner-m.webp",
            AltText = "  جشنواره گیفت کارت پلی استیشن  ",
            MobileAltText = "جشنواره گیفت کارت",
            Position = "home-hero",
            SortOrder = 2,
            IsActive = true
        });

        created.AltText.Should().Be("جشنواره گیفت کارت پلی استیشن", "surrounding whitespace is trimmed");
        created.MobileAltText.Should().Be("جشنواره گیفت کارت");

        var reread = await service.GetByIdAsync(created.Id);
        reread.AltText.Should().Be("جشنواره گیفت کارت پلی استیشن");

        var listed = (await service.GetAllAsync()).Single();
        listed.AltText.Should().Be("جشنواره گیفت کارت پلی استیشن");
        listed.MobileAltText.Should().Be("جشنواره گیفت کارت");
    }

    [Fact]
    public async Task Blank_alt_text_is_stored_as_nothing_so_the_title_can_stand_in()
    {
        await using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"banner-alt-blank-{Guid.NewGuid():N}").Options);
        var service = new AdminBannerService(db);

        var created = await service.CreateAsync(new CreateBannerRequestDto
        {
            Title = "بدون متن جایگزین",
            ImagePath = "/media/banner.webp",
            AltText = "   ",
            Position = "home-secondary",
            IsActive = true
        });

        created.AltText.Should().BeNull();
    }
}

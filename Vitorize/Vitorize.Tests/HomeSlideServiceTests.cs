using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.HomeSlides;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Storefront;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Slides carry copy as well as artwork, which is why they are their own entity rather than more
/// columns on a banner. These cover the rules an administrator meets in the panel.
/// </summary>
public sealed class HomeSlideServiceTests
{
    [Fact]
    public async Task A_slide_keeps_its_copy_artwork_and_alt_text()
    {
        var service = NewService();

        var created = await service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "  جشنواره پاییز  ",
            Subtitle = "  تخفیف روی گیفت کارت  ",
            ImagePath = "/media/slide.webp",
            MobileImagePath = "/media/slide-m.webp",
            AltText = "بنر جشنواره پاییز",
            MobileAltText = "جشنواره پاییز",
            LinkUrl = "https://example.test/campaign",
            LinkText = "مشاهده کمپین",
            SortOrder = 3
        });

        created.Title.Should().Be("جشنواره پاییز", "surrounding whitespace is trimmed");
        created.Subtitle.Should().Be("تخفیف روی گیفت کارت");
        created.AltText.Should().Be("بنر جشنواره پاییز");
        created.LinkText.Should().Be("مشاهده کمپین");

        var reread = await service.GetByIdAsync(created.Id);
        reread.MobileAltText.Should().Be("جشنواره پاییز");
        reread.SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task A_button_label_without_a_destination_is_refused()
    {
        var service = NewService();

        // Either both or neither: a labelled button with nowhere to go is a dead control.
        var act = () => service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "بدون مقصد",
            LinkText = "بزن بریم"
        });

        await act.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task An_end_before_its_start_is_refused()
    {
        var service = NewService();

        var act = () => service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "بازه وارونه",
            StartsAt = new DateTime(2026, 3, 1),
            EndsAt = new DateTime(2026, 2, 1)
        });

        await act.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task A_title_is_required()
    {
        var service = NewService();

        var act = () => service.CreateAsync(new CreateHomeSlideRequestDto { Title = "   " });

        await act.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task Slides_come_back_in_the_order_the_administrator_set()
    {
        var service = NewService();

        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "سوم", SortOrder = 2 });
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "اول", SortOrder = 0 });
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "دوم", SortOrder = 1 });

        var titles = (await service.GetAllAsync()).Select(x => x.Title).ToList();

        titles.Should().Equal("اول", "دوم", "سوم");
    }

    [Fact]
    public async Task Editing_a_slide_records_when_it_changed()
    {
        var service = NewService();
        var created = await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "پیش از ویرایش" });
        created.UpdatedAt.Should().BeNull();

        var updated = await service.UpdateAsync(created.Id, new UpdateHomeSlideRequestDto
        {
            Title = "پس از ویرایش",
            SortOrder = 5
        });

        updated.Title.Should().Be("پس از ویرایش");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_slide_lands_in_the_foot_slideshow_unless_told_otherwise()
    {
        // Every slide written before placements existed belongs to the foot one, and so does a
        // request that names a placement nobody recognises.
        var service = NewService();

        var defaulted = await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "بدون جایگاه" });
        var nonsense = await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "جایگاه نامعتبر", Placement = "somewhere-else" });

        defaulted.Placement.Should().Be(HomeSlidePlacements.Bottom);
        nonsense.Placement.Should().Be(HomeSlidePlacements.Bottom);
    }

    [Fact]
    public async Task The_middle_band_needs_artwork_but_not_a_heading()
    {
        // Its wording is normally part of the picture, so demanding a heading would force an
        // operator to invent copy that then prints on top of the artwork.
        var service = NewService();

        var created = await service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "",
            ImagePath = "/media/middle.webp",
            Placement = HomeSlidePlacements.Middle
        });

        created.Title.Should().BeEmpty();
        created.Placement.Should().Be(HomeSlidePlacements.Middle);

        var withoutArtwork = () => service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "",
            Placement = HomeSlidePlacements.Middle
        });

        await withoutArtwork.Should().ThrowAsync<BusinessException>()
            .WithMessage("*تصویر*");
    }

    [Fact]
    public async Task The_foot_slideshow_still_insists_on_a_heading()
    {
        var service = NewService();

        var act = () => service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "   ",
            ImagePath = "/media/bottom.webp",
            Placement = HomeSlidePlacements.Bottom
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*عنوان*");
    }

    [Fact]
    public async Task The_two_slideshows_are_listed_apart_and_ordered_within_themselves()
    {
        var service = NewService();
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "پایین دوم", SortOrder = 1, Placement = HomeSlidePlacements.Bottom });
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "میانی دوم", ImagePath = "/m2.webp", SortOrder = 1, Placement = HomeSlidePlacements.Middle });
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "پایین اول", SortOrder = 0, Placement = HomeSlidePlacements.Bottom });
        await service.CreateAsync(new CreateHomeSlideRequestDto { Title = "میانی اول", ImagePath = "/m1.webp", SortOrder = 0, Placement = HomeSlidePlacements.Middle });

        var all = await service.GetAllAsync();

        all.Where(x => x.Placement == HomeSlidePlacements.Middle).Select(x => x.Title)
            .Should().Equal("میانی اول", "میانی دوم");
        all.Where(x => x.Placement == HomeSlidePlacements.Bottom).Select(x => x.Title)
            .Should().Equal("پایین اول", "پایین دوم");
    }

    [Fact]
    public async Task Moving_a_slide_between_the_two_slideshows_sticks()
    {
        var service = NewService();
        var created = await service.CreateAsync(new CreateHomeSlideRequestDto
        {
            Title = "جابه‌جا",
            ImagePath = "/media/slide.webp"
        });
        created.Placement.Should().Be(HomeSlidePlacements.Bottom);

        var moved = await service.UpdateAsync(created.Id, new UpdateHomeSlideRequestDto
        {
            Title = "جابه‌جا",
            ImagePath = "/media/slide.webp",
            Placement = HomeSlidePlacements.Middle
        });

        moved.Placement.Should().Be(HomeSlidePlacements.Middle);
    }

    private static AdminHomeSlideService NewService() =>
        new(new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"home-slides-{Guid.NewGuid():N}").Options));
}

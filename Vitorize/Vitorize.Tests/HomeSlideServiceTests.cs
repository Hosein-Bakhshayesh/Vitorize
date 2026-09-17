using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.HomeSlides;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
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

    private static AdminHomeSlideService NewService() =>
        new(new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"home-slides-{Guid.NewGuid():N}").Options));
}

using FluentAssertions;
using Vitorize.Shared.Storefront;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.HomeSlides;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The middle band and the foot of the homepage were two mechanisms doing one job — one of them an
/// image-only banner that could never be captioned. They share an entity now, told apart by a
/// placement, so the rules that used to differ by table have to differ by that value instead.
/// </summary>
public sealed class HomeSlidePlacementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("home-secondary")]
    [InlineData("HOME-MIDDLE")]
    public void An_unknown_placement_resolves_to_the_foot_slideshow(string? stored)
    {
        // Rows written before placements existed are foot slides, and so is anything misspelt:
        // a slide must never vanish from the page because its placement was not recognised.
        HomeSlidePlacements.Normalize(stored).Should().Be(HomeSlidePlacements.Bottom);
    }

    [Fact]
    public void Both_placements_are_recognised_exactly()
    {
        HomeSlidePlacements.Normalize(HomeSlidePlacements.Middle).Should().Be(HomeSlidePlacements.Middle);
        HomeSlidePlacements.Normalize(HomeSlidePlacements.Bottom).Should().Be(HomeSlidePlacements.Bottom);
        HomeSlidePlacements.All.Should().Equal(HomeSlidePlacements.Middle, HomeSlidePlacements.Bottom);
    }

    [Fact]
    public void Each_placement_asks_for_its_own_proportions()
    {
        // One guidance for both would send the client a file that the other frame crops away.
        var middle = ImageSpecs.ForSlidePlacement(HomeSlidePlacements.Middle);
        var bottom = ImageSpecs.ForSlidePlacement(HomeSlidePlacements.Bottom);

        middle.Ratio.Should().Be("۱۶:۹");
        bottom.Ratio.Should().Be("۳.۵:۱");
        middle.Dimensions.Should().NotBe(bottom.Dimensions);
    }

    [Fact]
    public void An_unset_placement_is_still_given_the_foot_proportions()
    {
        ImageSpecs.ForSlidePlacement(null).Ratio.Should().Be("۳.۵:۱");
    }

    [Fact]
    public void A_middle_slide_may_go_without_a_heading_but_not_without_artwork()
    {
        Errors(new AdminHomeSlideInputModel
        {
            Title = "",
            ImagePath = "/media/middle.webp",
            Placement = HomeSlidePlacements.Middle
        }).Should().BeEmpty();

        Errors(new AdminHomeSlideInputModel
        {
            Title = "",
            Placement = HomeSlidePlacements.Middle
        }).Should().ContainSingle().Which.Should().Contain("تصویر");
    }

    [Fact]
    public void A_foot_slide_may_go_without_artwork_but_not_without_a_heading()
    {
        Errors(new AdminHomeSlideInputModel
        {
            Title = "",
            Placement = HomeSlidePlacements.Bottom
        }).Should().ContainSingle().Which.Should().Contain("عنوان");

        Errors(new AdminHomeSlideInputModel
        {
            Title = "جشنواره",
            Placement = HomeSlidePlacements.Bottom
        }).Should().BeEmpty();
    }

    [Fact]
    public void A_button_still_needs_both_halves_wherever_the_slide_sits()
    {
        foreach (var placement in HomeSlidePlacements.All)
        {
            Errors(new AdminHomeSlideInputModel
            {
                Title = "عنوان",
                ImagePath = "/media/slide.webp",
                LinkText = "ببینید",
                Placement = placement
            }).Should().ContainSingle().Which.Should().Contain("لینک");
        }
    }

    private static List<string> Errors(AdminHomeSlideInputModel model) =>
        model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model))
            .Select(x => x.ErrorMessage ?? string.Empty)
            .ToList();
}

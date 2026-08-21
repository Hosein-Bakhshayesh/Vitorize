using FluentAssertions;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The home page's popular-products row is settings-driven and off unless an administrator turns it
/// on. The important case is the absent setting: an environment whose settings row has not been
/// created yet must keep the section hidden, never show it by falling back to true.
/// </summary>
public sealed class HomePopularProductsSettingTests
{
    private const string Key = "HomePopularProductsEnabled";

    private static StoreBranding Branding(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void A_missing_setting_keeps_the_section_hidden()
    {
        Branding().HomePopularProductsEnabled.Should().BeFalse(
            "an environment that has not been seeded yet must not start showing the section");
    }

    [Fact]
    public void An_empty_value_keeps_the_section_hidden()
    {
        Branding((Key, "")).HomePopularProductsEnabled.Should().BeFalse();
    }

    [Fact]
    public void The_seeded_default_keeps_the_section_hidden()
    {
        Branding((Key, "false")).HomePopularProductsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Turning_it_on_shows_the_section()
    {
        Branding((Key, "true")).HomePopularProductsEnabled.Should().BeTrue();
        Branding((Key, "True")).HomePopularProductsEnabled.Should().BeTrue();
    }

    [Fact]
    public void An_unparseable_value_is_treated_as_off()
    {
        // Anything the shop did not clearly enable stays hidden.
        Branding((Key, "yes")).HomePopularProductsEnabled.Should().BeFalse();
        Branding((Key, "1")).HomePopularProductsEnabled.Should().BeFalse();
    }

    [Fact]
    public void The_toggle_does_not_disturb_other_home_settings()
    {
        var branding = Branding((Key, "false"), ("HeroTitle", "عنوان"), ("NewsletterTitle", "خبرنامه"));

        branding.HomePopularProductsEnabled.Should().BeFalse();
        branding.HeroTitle.Should().Be("عنوان");
        branding.NewsletterTitle.Should().Be("خبرنامه");
    }
}

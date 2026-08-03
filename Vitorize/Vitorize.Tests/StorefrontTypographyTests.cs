using FluentAssertions;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontTypographyTests
{
    [Fact]
    public void Uses_the_official_storefront_fonts_by_default()
    {
        var branding = new StoreBranding(new Dictionary<string, string>());

        branding.StorefrontPersianFont.Should().Be("Peyda");
        branding.StorefrontEnglishFont.Should().Be("Funnel Display");
    }

    [Fact]
    public void Reads_storefront_font_choices_from_public_settings()
    {
        var branding = new StoreBranding(new Dictionary<string, string>
        {
            ["StorefrontPersianFont"] = "Custom Persian",
            ["StorefrontEnglishFont"] = "Manrope"
        });

        branding.StorefrontPersianFont.Should().Be("Custom Persian");
        branding.StorefrontEnglishFont.Should().Be("Manrope");
    }
}

using FluentAssertions;
using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontIconVisualConsistencyTests
{
    [Fact]
    public void Header_and_mobile_navigation_use_the_reference_account_and_cart_glyphs()
    {
        var header = ReadComponent("Storefront", "StoreHeader.razor");
        var mobileNavigation = ReadComponent("Storefront", "StoreBottomNav.razor");

        header.Should().Contain("Name=\"circle-user-round\"");
        header.Should().Contain("Name=\"tabler:shopping-cart\"");
        header.Should().NotContain("Name=\"contact-round\"");
        mobileNavigation.Should().Contain("\"tabler:shopping-cart\"");
        mobileNavigation.Should().Contain("\"circle-user-round\"");
    }

    [Fact]
    public void Social_links_use_brand_glyphs_instead_of_generic_symbols()
    {
        var footer = ReadComponent("Storefront", "StoreFooter.razor");
        var contact = ReadComponent("Pages", "Store", "Contact.razor");

        foreach (var brand in new[]
                 {
                     "instagram", "telegram", "whatsapp", "x", "linkedin", "youtube", "discord", "facebook"
                 })
        {
            footer.Should().Contain($"tabler:brand-{brand}");
        }

        foreach (var brand in new[] { "instagram", "telegram", "whatsapp", "x", "linkedin", "youtube", "facebook" })
        {
            contact.Should().Contain($"tabler:brand-{brand}");
        }
    }

    private static string ReadComponent(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "Vitorize.Web", "Components", Path.Combine(parts));
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Component was not found: {Path.Combine(parts)}");
    }
}

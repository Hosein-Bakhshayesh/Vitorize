using Xunit;

namespace Vitorize.Tests;

public sealed class HomepageProductCardConsistencyTests
{
    [Fact]
    public void Home_page_reuses_the_catalogue_product_card()
    {
        var root = FindSolutionRoot();
        var home = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Pages", "Store", "Home.razor"));
        var homeCss = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));
        var dto = File.ReadAllText(Path.Combine(root, "Vitorize.Application", "DTOs", "Storefront", "StorefrontProductDto.cs"));
        var service = File.ReadAllText(Path.Combine(root, "Vitorize.Infrastructure", "Services", "StorefrontService.cs"));

        Assert.Contains("<StoreProductCard @key=\"product.Id\" Product=\"product\" />", home, StringComparison.Ordinal);
        Assert.DoesNotContain("<HomeProductCard", home, StringComparison.Ordinal);
        Assert.Contains(".hp-product-grid { grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 20px;", homeCss, StringComparison.Ordinal);
        Assert.Contains(".hp-product-grid .st-pcard:nth-child(n+5)", homeCss, StringComparison.Ordinal);
        Assert.Contains("public byte CurrencyType", dto, StringComparison.Ordinal);
        Assert.Contains("public string CategoryTitle", dto, StringComparison.Ordinal);
        Assert.Contains("public bool HasVariants", dto, StringComparison.Ordinal);
        Assert.Contains("public double AverageRating", dto, StringComparison.Ordinal);
        Assert.Contains("CategoryTitle = x.Category.Title", service, StringComparison.Ordinal);
        Assert.Contains("AverageRating = _dbContext.ProductReviews", service, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vitorize.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Vitorize solution root.");
    }
}

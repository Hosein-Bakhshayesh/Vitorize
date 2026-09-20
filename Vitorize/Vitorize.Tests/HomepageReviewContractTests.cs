using Xunit;

namespace Vitorize.Tests;

public sealed class HomepageReviewContractTests
{
    [Fact]
    public void Featured_products_include_the_public_review_count_used_by_the_homepage()
    {
        var root = FindSolutionRoot();
        var dto = File.ReadAllText(Path.Combine(root, "Vitorize.Application", "DTOs", "Storefront", "StorefrontProductDto.cs"));
        var service = File.ReadAllText(Path.Combine(root, "Vitorize.Infrastructure", "Services", "StorefrontService.cs"));
        var home = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Pages", "Store", "Home.razor"));

        Assert.Contains("public int ReviewCount", dto, StringComparison.Ordinal);
        Assert.Contains("ReviewCount = _dbContext.ProductReviews.Count", service, StringComparison.Ordinal);
        Assert.Contains("r.ParentId == null", service, StringComparison.Ordinal);
        Assert.Contains("r.IsApproved", service, StringComparison.Ordinal);
        Assert.Contains("!r.IsRejected", service, StringComparison.Ordinal);
        Assert.Contains("_products.Where(p => p.ReviewCount > 0)", home, StringComparison.Ordinal);
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

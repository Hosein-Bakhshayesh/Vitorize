using FluentAssertions;
using Vitorize.Shared.Common;
using Vitorize.Shared.Icons;
using Vitorize.Shared.Logging;
using Xunit;

namespace Vitorize.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SharedContractUnitTests
{
    [Fact]
    public void Api_result_factories_preserve_success_failure_data_and_errors()
    {
        var success = ApiResult.Success("done");
        success.IsSuccess.Should().BeTrue();
        success.Message.Should().Be("done");
        success.Errors.Should().BeEmpty();

        var failure = ApiResult.Failure("failed", ["first", "second"]);
        failure.IsSuccess.Should().BeFalse();
        failure.Message.Should().Be("failed");
        failure.Errors.Should().Equal("first", "second");

        var genericSuccess = ApiResult<int>.Success(42, "answer");
        genericSuccess.IsSuccess.Should().BeTrue();
        genericSuccess.Data.Should().Be(42);
        genericSuccess.Message.Should().Be("answer");

        var genericFailure = ApiResult<int>.Failure("bad", ["invalid"]);
        genericFailure.IsSuccess.Should().BeFalse();
        genericFailure.Data.Should().Be(default);
        genericFailure.Errors.Should().ContainSingle("invalid");
    }

    [Fact]
    public void Paged_result_is_a_passive_deterministic_page_contract()
    {
        var page = new PagedResult<string>
        {
            Items = ["a", "b"],
            Page = 2,
            PageSize = 2,
            TotalCount = 5
        };

        page.Items.Should().Equal("a", "b");
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
    }

    [Fact]
    public void Lucide_legacy_detection_find_and_record_metadata_are_consistent()
    {
        LucideIconCatalog.IsLegacyKey("cart", out var replacement).Should().BeTrue();
        replacement.Should().Be("shopping-cart");
        LucideIconCatalog.IsLegacyKey("shopping-cart", out replacement).Should().BeFalse();
        replacement.Should().BeNull();
        LucideIconCatalog.IsLegacyKey("unknown", out replacement).Should().BeFalse();

        var entry = LucideIconCatalog.Find("cart");
        entry.Should().NotBeNull();
        entry!.Key.Should().Be("shopping-cart");
        entry.EnglishName.Should().NotBeNullOrWhiteSpace();
        entry.Category.Should().NotBeNullOrWhiteSpace();
        entry.Tags.Should().NotBeEmpty();
        LucideIconCatalog.Find("unknown").Should().BeNull();

        var category = LucideIconCatalog.Categories[0];
        category.Key.Should().NotBeNullOrWhiteSpace();
        category.PersianTitle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Monitoring_seq_url_normalization_accepts_safe_path_and_rejects_credentials()
    {
        MonitoringOptions.TryGetSafeSeqUiUrl("https://seq.example/events/", out var normalized).Should().BeTrue();
        normalized.Should().Be("https://seq.example/events");
        MonitoringOptions.TryGetSafeSeqUiUrl("https://user:pass@seq.example", out normalized).Should().BeFalse();
        normalized.Should().BeNull();

        var disabled = new SeqOptions { Enabled = false, ServerUrl = "https://seq.example" };
        disabled.TryGetValidatedServer(out var server).Should().BeFalse();
        server.Should().BeNull();
    }
}

using FluentAssertions;
using Vitorize.Web.Helpers;
using Xunit;

namespace Vitorize.Tests.Unit;

public sealed class AdminCsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://example.test\",\"open\")")]
    [InlineData("+SUM(1,1)")]
    [InlineData("@cmd")]
    [InlineData("-1+2")]
    [InlineData("\tformula")]
    public void Field_prefixes_spreadsheet_formula_markers(string value)
    {
        AdminCsv.Field(value).Should().Contain("'" + value[0]);
    }

    [Fact]
    public void Field_preserves_numeric_and_persian_text_values()
    {
        AdminCsv.Field("12345").Should().Be("12345");
        AdminCsv.Field("متن فارسی").Should().Be("متن فارسی");
    }
}

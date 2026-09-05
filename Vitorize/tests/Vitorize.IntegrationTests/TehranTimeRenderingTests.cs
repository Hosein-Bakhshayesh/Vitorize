using FluentAssertions;
using Vitorize.Web.Helpers;

namespace Vitorize.IntegrationTests;

public sealed class TehranTimeRenderingTests
{
    [Fact]
    public void Utc_timestamp_is_rendered_in_tehran_time()
    {
        var utc = new DateTime(2026, 9, 5, 14, 29, 0, DateTimeKind.Utc);

        PersianDateHelper.ToDateTime(utc).Should().EndWith("۱۷:۵۹");
        PersianDateHelper.ToIranTime(utc).Hour.Should().Be(17);
        PersianDateHelper.ToIranTime(utc).Minute.Should().Be(59);
    }

    [Fact]
    public void Tehran_input_round_trips_to_utc()
    {
        var iranInput = new DateTime(2026, 9, 5, 17, 59, 0, DateTimeKind.Unspecified);

        PersianDateHelper.IranTimeToUtc(iranInput).Should().Be(new DateTime(2026, 9, 5, 14, 29, 0, DateTimeKind.Utc));
    }
}

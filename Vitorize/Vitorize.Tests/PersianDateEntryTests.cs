using Vitorize.Web.Helpers;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// How a Jalali birth date is typed. The customer types digits; the separators are the field's job.
///
/// These cover the rule itself - the same rule the browser-side mask mirrors per keystroke - and the
/// parsing that the server always applies to whatever finally arrives, so the field is correct even
/// with no script running at all.
/// </summary>
public sealed class PersianDateEntryTests
{
    // ---------------------------------------------------------------- the typing mask

    [Theory]
    [InlineData("1", "1")]
    [InlineData("14", "14")]
    [InlineData("140", "140")]
    [InlineData("1403", "1403")]
    [InlineData("14031", "1403/1")]
    [InlineData("140312", "1403/12")]
    [InlineData("1403122", "1403/12/2")]
    [InlineData("14031225", "1403/12/25")]
    public void Separators_appear_as_the_digits_are_typed(string typed, string expected) =>
        Assert.Equal(expected, PersianDateEntry.Mask(typed));

    [Fact]
    public void The_slash_after_the_year_is_inserted_without_being_typed()
    {
        // The moment a fifth digit exists, the year is complete and the separator belongs there.
        Assert.Equal("1403", PersianDateEntry.Mask("1403"));
        Assert.Equal("1403/0", PersianDateEntry.Mask("14030"));
    }

    [Fact]
    public void The_slash_after_the_month_is_inserted_without_being_typed()
    {
        Assert.Equal("1403/05", PersianDateEntry.Mask("140305"));
        Assert.Equal("1403/05/1", PersianDateEntry.Mask("1403051"));
    }

    [Fact]
    public void Separators_the_customer_types_themselves_are_harmless()
    {
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("1403/12/25"));
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("1403/1225"));
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("14031225"));
    }

    [Fact]
    public void Digits_beyond_a_complete_date_are_dropped_rather_than_shifting_the_value()
    {
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("140312259999"));
    }

    [Fact]
    public void Anything_that_is_not_a_digit_is_ignored()
    {
        Assert.Equal("1403/12/25", PersianDateEntry.Mask(" 1403 - 12 - 25 "));
        Assert.Equal("", PersianDateEntry.Mask("abc/--"));
        Assert.Equal("", PersianDateEntry.Mask(null));
    }

    // ---------------------------------------------------------------- digit scripts

    [Fact]
    public void Persian_digits_are_accepted()
    {
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("۱۴۰۳۱۲۲۵"));
        Assert.True(PersianDateEntry.TryParse("۱۴۰۳/۱۲/۲۵", out var date));
        Assert.Equal(new DateTime(2025, 3, 15), date);
    }

    [Fact]
    public void Arabic_indic_digits_are_accepted()
    {
        Assert.Equal("1403/12/25", PersianDateEntry.Mask("١٤٠٣١٢٢٥"));
        Assert.True(PersianDateEntry.TryParse("١٤٠٣/١٢/٢٥", out _));
    }

    [Fact]
    public void Mixed_scripts_in_one_entry_still_resolve()
    {
        Assert.True(PersianDateEntry.TryParse("۱۴۰۳/12/۲۵", out var mixed));
        Assert.True(PersianDateEntry.TryParse("1403/12/25", out var ascii));
        Assert.Equal(ascii, mixed);
    }

    // ---------------------------------------------------------------- paste

    [Fact]
    public void Pasting_with_or_without_separators_gives_the_same_date()
    {
        Assert.True(PersianDateEntry.TryParse("1403/12/25", out var withSlashes));
        Assert.True(PersianDateEntry.TryParse("14031225", out var without));
        Assert.Equal(withSlashes, without);
    }

    [Fact]
    public void An_incomplete_entry_is_not_treated_as_a_date()
    {
        Assert.False(PersianDateEntry.IsComplete("1403/12"));
        Assert.False(PersianDateEntry.TryParse("1403/12", out _));
        Assert.False(PersianDateEntry.TryParse("1403", out _));
        Assert.False(PersianDateEntry.TryParse("", out _));
    }

    // ---------------------------------------------------------------- real Jalali validity

    [Theory]
    [InlineData("1403/01/01")]
    [InlineData("1403/06/31")]      // the first six months have 31 days
    [InlineData("1375/06/15")]
    [InlineData("1403/12/30")]      // 1403 is a leap year, so 30 Esfand exists
    public void Valid_jalali_dates_are_accepted(string entry) =>
        Assert.True(PersianDateEntry.TryParse(entry, out _), entry + " is a real date");

    [Theory]
    [InlineData("1403/00/10")]      // month 0
    [InlineData("1403/13/01")]      // month 13
    [InlineData("1403/01/00")]      // day 0
    [InlineData("1403/07/31")]      // the second half of the year has 30-day months
    [InlineData("1404/12/30")]      // 1404 is a common year: no 30 Esfand
    public void Dates_the_persian_calendar_does_not_contain_are_rejected(string entry) =>
        Assert.False(PersianDateEntry.TryParse(entry, out _), entry + " is not a real date");

    [Fact]
    public void Esfand_length_is_taken_from_the_calendar_itself_for_every_year_in_range()
    {
        // Derived, not asserted from memory: for each year the picker can offer, ask PersianCalendar
        // how long Esfand is and require TryParse to agree. A hard-coded list of leap years would only
        // restate a belief about the calendar - and an earlier draft of this work got that belief
        // backwards - whereas this fails if parsing and the calendar ever diverge.
        var calendar = new System.Globalization.PersianCalendar();
        var leapYears = 0;
        var commonYears = 0;

        for (var year = 1330; year <= 1410; year++)
        {
            var daysInEsfand = calendar.GetDaysInMonth(year, 12);
            var thirtiethExists = PersianDateEntry.TryParse($"{year}/12/30", out _);

            Assert.Equal(daysInEsfand == 30, thirtiethExists);
            Assert.True(PersianDateEntry.TryParse($"{year}/12/29", out _), $"29 Esfand {year} always exists");

            if (daysInEsfand == 30) leapYears++; else commonYears++;
        }

        // Both branches were actually exercised, so the loop cannot pass by covering only one case.
        Assert.True(leapYears > 0, "the range must contain leap years");
        Assert.True(commonYears > 0, "the range must contain common years");
    }

    [Fact]
    public void Leap_and_common_esfand_are_told_apart_by_the_real_calendar()
    {
        // Not a regex check: the distinction only exists in the calendar itself.
        Assert.True(PersianDateEntry.TryParse("1403/12/30", out _), "1403 is a leap year");
        Assert.False(PersianDateEntry.TryParse("1404/12/30", out _), "1404 is not");
        Assert.True(PersianDateEntry.TryParse("1404/12/29", out _), "29 Esfand always exists");
    }

    // ---------------------------------------------------------------- round trip

    [Theory]
    [InlineData("1403/01/01")]
    [InlineData("1375/06/15")]
    [InlineData("1403/12/30")]
    [InlineData("1360/11/22")]
    public void A_date_survives_the_round_trip_through_storage_unchanged(string entry)
    {
        Assert.True(PersianDateEntry.TryParse(entry, out var gregorian));

        // What is stored is a date, not a moment: the displayed value must come back identical with
        // no timezone shifting a day either way.
        var stored = DateOnly.FromDateTime(gregorian);
        var redisplayed = PersianDateHelper.ToShortDate(stored.ToDateTime(TimeOnly.MinValue));

        Assert.Equal(entry, ToAscii(redisplayed));
    }

    [Fact]
    public void Storage_keeps_the_date_at_midnight_so_no_timezone_can_move_it()
    {
        Assert.True(PersianDateEntry.TryParse("1403/01/01", out var date));

        Assert.Equal(TimeSpan.Zero, date.TimeOfDay);
        Assert.Equal(new DateTime(2024, 3, 20), date);
    }

    [Fact]
    public void The_mask_and_the_parser_agree_on_every_length()
    {
        // Whatever the mask produces from a complete entry must be parseable, and vice versa.
        const string digits = "13750615";
        var masked = PersianDateEntry.Mask(digits);

        Assert.Equal("1375/06/15", masked);
        Assert.True(PersianDateEntry.IsComplete(masked));
        Assert.True(PersianDateEntry.TryParse(masked, out var fromMask));
        Assert.True(PersianDateEntry.TryParse(digits, out var fromDigits));
        Assert.Equal(fromDigits, fromMask);
    }

    private static string ToAscii(string value) => new(value.Select(ch =>
        ch is >= '۰' and <= '۹' ? (char)('0' + (ch - '۰')) : ch).ToArray());
}

using Vitorize.Application.Common;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FAQ answers are plain text that the storefront renders HTML-encoded, with line breaks preserved
/// by styling (white-space: pre-line) rather than by markup. These tests pin the storage half of
/// that contract: whatever newline convention a browser posts, one convention is stored, and nothing
/// strips or escapes the author's characters beyond trimming the ends.
/// </summary>
public sealed class FaqAnswerFormattingTests
{
    [Fact]
    public void Windows_newlines_are_normalised_to_a_single_convention()
    {
        var stored = FaqAnswerText.Normalize("خط اول\r\nخط دوم\r\nخط سوم");

        Assert.Equal("خط اول\nخط دوم\nخط سوم", stored);
        Assert.DoesNotContain("\r", stored);
    }

    [Fact]
    public void Lone_carriage_returns_are_normalised_too()
    {
        Assert.Equal("a\nb", FaqAnswerText.Normalize("a\rb"));
    }

    [Fact]
    public void Existing_unix_newlines_are_left_alone()
    {
        Assert.Equal("a\nb\nc", FaqAnswerText.Normalize("a\nb\nc"));
    }

    [Fact]
    public void Blank_lines_between_paragraphs_survive()
    {
        // A deliberate empty line is meaningful to the author, so it must not be collapsed.
        Assert.Equal("پاراگراف اول\n\nپاراگراف دوم",
            FaqAnswerText.Normalize("پاراگراف اول\r\n\r\nپاراگراف دوم"));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_but_inner_structure_is_not()
    {
        Assert.Equal("خط اول\nخط دوم", FaqAnswerText.Normalize("  \r\n خط اول\nخط دوم \r\n "));
    }

    [Fact]
    public void Markup_is_stored_verbatim_because_rendering_encodes_it()
    {
        // Storing it untouched is safe precisely because the storefront never treats it as markup;
        // escaping here would double-encode and show the entities to the customer.
        const string hostile = "قبل\n<script>alert(1)</script>\nبعد";
        Assert.Equal(hostile, FaqAnswerText.Normalize(hostile));
    }

    [Fact]
    public void Null_or_whitespace_becomes_empty_rather_than_throwing()
    {
        Assert.Equal(string.Empty, FaqAnswerText.Normalize(null));
        Assert.Equal(string.Empty, FaqAnswerText.Normalize("   \r\n  "));
    }
}

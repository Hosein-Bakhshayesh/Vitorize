namespace Vitorize.Application.Common;

/// <summary>
/// How a FAQ answer is stored.
///
/// Answers are plain text. The storefront renders them HTML-encoded and preserves the author's line
/// breaks through styling (white-space: pre-line) rather than by turning newlines into markup, which
/// is why nothing here escapes or rewrites the text: escaping would double-encode and show entities
/// to the customer, and converting to &lt;br&gt; would require rendering the answer as raw markup.
///
/// The one thing that does need normalising is the newline convention. A browser textarea posts
/// CRLF, so storing it verbatim would mean the same answer round-tripping through the editor
/// accumulates carriage returns and no longer matches what is rendered.
/// </summary>
public static class FaqAnswerText
{
    /// <summary>
    /// Returns the answer with one newline convention (LF) and no surrounding whitespace. Inner
    /// structure, including deliberate blank lines between paragraphs, is preserved.
    /// </summary>
    public static string Normalize(string? answer) =>
        string.IsNullOrWhiteSpace(answer)
            ? string.Empty
            : answer.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
}

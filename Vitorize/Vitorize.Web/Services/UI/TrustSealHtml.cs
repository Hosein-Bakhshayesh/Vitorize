using System.Text.RegularExpressions;

namespace Vitorize.Web.Services.UI;

/// <summary>
/// Splits the administrator's raw trust-seal HTML (TrustSeal.FooterHtml) into what each surface can
/// actually use. Providers hand out a mix of shapes: eNamad/Emalls give anchor+image markup that
/// belongs in the footer's seal box, Zarinpal gives a document.write script that can never run
/// inside Blazor's render pipeline, and administrators also paste unrelated widget scripts (live
/// chat) into the same box. Rendering the whole blob verbatim produced duplicated, unstyled seals
/// and dead scripts - so the badges go to the footer, the scripts go to the end of the body, and
/// the Zarinpal script is swapped for its official static badge.
/// </summary>
public static partial class TrustSealHtml
{
    [GeneratedRegex(@"<script\b[^>]*>[\s\S]*?</script>|<script\b[^>]*/\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlocks();

    private const string ZarinpalTrustCodeMarker = "zarinpal.com/webservice/TrustCode";

    /// <summary>Anchor/image markup only, for the footer's seal box. When the pasted blob carried
    /// the Zarinpal TrustCode script, the official static badge takes its place.</summary>
    public static string Badges(string? raw, string host)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var hadZarinpal = raw.Contains(ZarinpalTrustCodeMarker, StringComparison.OrdinalIgnoreCase);
        var badges = ScriptBlocks().Replace(raw, string.Empty).Trim();

        if (hadZarinpal)
        {
            badges +=
                $"<a referrerpolicy=\"origin\" target=\"_blank\" href=\"https://www.zarinpal.com/trustPage/{host}\">" +
                "<img referrerpolicy=\"origin\" src=\"https://cdn.zarinpal.com/badges/trustLogo/1.svg\" " +
                "alt=\"درگاه پرداخت زرین‌پال\" loading=\"lazy\"></a>";
        }

        return badges;
    }

    /// <summary>The pasted script tags (live chat and similar), minus the Zarinpal TrustCode script,
    /// which is represented by the static badge instead. Emitted once, at the end of the body, on
    /// ordinary storefront pages only.</summary>
    public static string Scripts(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        return string.Concat(ScriptBlocks().Matches(raw)
            .Select(m => m.Value)
            .Where(s => !s.Contains(ZarinpalTrustCodeMarker, StringComparison.OrdinalIgnoreCase)));
    }
}

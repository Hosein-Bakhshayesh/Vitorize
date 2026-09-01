using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The administrator pastes provider-issued markup verbatim; the split decides what each surface
/// renders. Shapes pinned here are the real ones providers hand out: eNamad/Emalls anchors, the
/// Zarinpal document.write script (which can never run under Blazor and becomes the official static
/// badge), and unrelated widget scripts pasted into the same box.
/// </summary>
public sealed class TrustSealHtmlTests
{
    private const string Pasted =
        "<a href='https://trustseal.enamad.ir/?id=1'><img src='https://trustseal.enamad.ir/logo.aspx?id=1'></a>\n" +
        "<a href='https://emalls.ir/Shop/1/'><img width='75' height='112' src='https://service.emalls.ir/neshan?id=1'></a>\n" +
        "<script src=\"https://www.zarinpal.com/webservice/TrustCode\" type=\"text/javascript\"></script>\n" +
        "<script>(function(){window.__chat=1;})();</script>";

    [Fact]
    public void Badges_keep_anchors_strip_scripts_and_substitute_the_static_zarinpal_badge()
    {
        var badges = TrustSealHtml.Badges(Pasted, "vitorize.com");

        Assert.Contains("trustseal.enamad.ir", badges);
        Assert.Contains("service.emalls.ir", badges);
        Assert.DoesNotContain("<script", badges, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://www.zarinpal.com/trustPage/vitorize.com", badges);
        Assert.Contains("cdn.zarinpal.com/badges/trustLogo", badges);
    }

    [Fact]
    public void Badges_without_a_zarinpal_script_add_no_zarinpal_badge()
    {
        var badges = TrustSealHtml.Badges("<a href='https://x'><img src='https://x/l.png'></a>", "vitorize.com");
        Assert.DoesNotContain("zarinpal", badges, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scripts_return_only_the_non_zarinpal_scripts()
    {
        var scripts = TrustSealHtml.Scripts(Pasted);

        Assert.Contains("window.__chat=1", scripts);
        Assert.DoesNotContain("TrustCode", scripts);
        Assert.DoesNotContain("enamad", scripts, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_yields_empty_surfaces(string? raw)
    {
        Assert.Equal(string.Empty, TrustSealHtml.Badges(raw, "vitorize.com"));
        Assert.Equal(string.Empty, TrustSealHtml.Scripts(raw));
    }
}

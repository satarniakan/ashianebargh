using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Ashianebargh.Application.Helpers;

/// <summary>
/// پالایش HTML توضیحات محصول/صفحه پیش از رندر با <c>MarkupString</c>.
/// <para>
/// چرا: توضیحات می‌تواند از ورود ادمین یا وارد کردن از سایت بیرونی بیاید؛ رندر خام
/// یعنی XSS ذخیره‌شده. به‌جای تکیه بر «شروع با &lt;table» (که مرز امنیتی نیست)،
/// اینجا فقط عناصر/صفاتِ مجازِ فهرست‌سفید نگه داشته می‌شوند و بقیه حذف می‌گردند.
/// تجزیه‌گر AngleSharp مطابقِ استانداردِ مرورگر است، پس درختِ ساخته‌شده همان چیزی است که
/// مرورگر می‌سازد و «جهشِ تجزیه» (mutation XSS) رخ نمی‌دهد.
/// </para>
/// <para>
/// عناصر خطرناک (script/style/iframe/svg/math/…) به‌همراه زیردرختشان حذف می‌شوند؛
/// عناصر ناشناختهٔ بی‌خطر «پوست‌کنده» می‌شوند تا متنشان بماند. صفات رویداد (on*) و
/// <c>class</c> همیشه حذف؛ <c>style</c> فقط با ویژگی‌های امنِ فهرست‌سفید (رنگ/قلم/اندازه/
/// تراز/تورفتگی/عرض — قالب‌بندی درون‌خطی Jodit)؛ لینک/تصویر فقط با URL امنِ <see cref="SafeUrl"/>.
/// </para>
/// </summary>
public static class HtmlContentSanitizer
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        // جدولِ مشخصات
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "colgroup", "col",
        // متن و قالب‌بندی پایه
        "p", "br", "hr", "span", "div", "b", "strong", "i", "em", "u", "s", "sub", "sup",
        "small", "mark", "blockquote", "pre", "code", "h1", "h2", "h3", "h4", "h5", "h6",
        // فهرست‌ها
        "ul", "ol", "li", "dl", "dt", "dd",
        // لینک و تصویر
        "a", "img"
    };

    // عناصری که محتوایشان متنِ خام/خطرناک یا فضای‌نام بیگانه است؛ حذفِ کامل زیردرخت.
    private static readonly HashSet<string> DropElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "applet", "svg", "math", "template",
        "noscript", "noembed", "noframes", "frame", "frameset", "textarea", "title", "xmp",
        "plaintext", "base", "link", "meta", "head", "html", "form", "input", "button",
        "select", "option", "optgroup", "datalist", "keygen"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new(StringComparer.OrdinalIgnoreCase) { "href", "title" },
            ["img"] = new(StringComparer.OrdinalIgnoreCase) { "src", "alt", "title", "width", "height" },
            ["td"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan" },
            ["th"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "scope" },
            ["col"] = new(StringComparer.OrdinalIgnoreCase) { "span" },
            ["colgroup"] = new(StringComparer.OrdinalIgnoreCase) { "span" }
        };

    // صفات عمومیِ بی‌خطر (جهت/زبان برای متن راست‌چین).
    private static readonly HashSet<string> GlobalAttributes =
        new(StringComparer.OrdinalIgnoreCase) { "dir", "lang" };

    // قالب‌بندی Jodit استایل درون‌خطی است؛ فقط این ویژگی‌ها با مقدارِ منطبق بر الگو می‌مانند.
    // هر ویژگی الگوی سخت‌گیرانه دارد تا url(/expression/گریزِ CSS از آن رد نشود.
    private static readonly Dictionary<string, Regex> AllowedStyleProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["color"] = new Regex(@"^(#[0-9a-f]{3,8}|[a-z]{2,30}|(rgb|rgba|hsl|hsla)\([\d\s.,%+-]+\))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["background-color"] = new Regex(@"^(#[0-9a-f]{3,8}|[a-z]{2,30}|(rgb|rgba|hsl|hsla)\([\d\s.,%+-]+\))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["font-size"] = new Regex(@"^(\d{1,4}(\.\d+)?(px|em|rem|pt|%)|(xx-|x-)?(small|large)|medium|smaller|larger)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["font-family"] = new Regex(@"^[a-z0-9\s""',_-]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["text-align"] = new Regex("^(left|right|center|justify|start|end)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["margin-left"] = new Regex(@"^\d{1,4}(\.\d+)?px$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["margin-right"] = new Regex(@"^\d{1,4}(\.\d+)?px$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ["width"] = new Regex(@"^\d{1,4}(\.\d+)?(px|%)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        };

    /// <summary>
    /// آیا متن شامل نشانه‌گذاری HTML است؟ مسیر رندر را بین «پاراگراف‌بندی خطی» (داده‌های
    /// قدیمی متنی) و «رندر HTML پالایش‌شده» انتخاب می‌کند. مقایسهٔ ساده با <c>&lt;</c>
    /// برای متن فارسی کافی است چون نویسهٔ عادیِ فارسی/لاتین هرگز با «&lt;+حرف» شروع نمی‌شود.
    /// </summary>
    public static bool ContainsMarkup(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(html, "<[a-zA-Z!/]");
    }

    /// <summary>HTML پالایش‌شده و امن برای رندر؛ ورودی خالی → رشتهٔ خالی.</summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        // فقط بدنه را نگه می‌داریم؛ هرچه در head باشد (meta/style/script) خودبه‌خود حذف است.
        var body = document.Body;
        if (body is null)
            return string.Empty;

        SanitizeChildren(body);
        return body.InnerHtml;
    }

    private static void SanitizeChildren(INode node)
    {
        // عکس فوری از فرزندان، چون حین پیمایش درخت را تغییر می‌دهیم.
        foreach (var child in node.ChildNodes.ToArray())
        {
            if (child is IComment)
            {
                node.RemoveChild(child);
                continue;
            }

            if (child is not IElement element)
                continue; // گره متنی: هنگام سریال‌سازی encode می‌شود و بی‌خطر است.

            var tag = element.LocalName;

            if (DropElements.Contains(tag))
            {
                node.RemoveChild(element);
                continue;
            }

            if (!AllowedElements.Contains(tag))
            {
                // عنصر ناشناختهٔ بی‌خطر: اول زیردرختش را پاک کن، بعد پوست بکن تا متن بماند.
                SanitizeChildren(element);
                foreach (var inner in element.ChildNodes.ToArray())
                    node.InsertBefore(inner, element);
                node.RemoveChild(element);
                continue;
            }

            StripAttributes(element);
            SanitizeChildren(element);
        }
    }

    private static void StripAttributes(IElement element)
    {
        AllowedAttributes.TryGetValue(element.LocalName, out var perTag);

        foreach (var attribute in element.Attributes.ToArray())
        {
            var name = attribute.Name;

            if (name is "style")
            {
                var safeStyle = SanitizeStyle(attribute.Value);
                if (safeStyle.Length == 0)
                    element.RemoveAttribute(name);
                else
                    element.SetAttribute(name, safeStyle);
                continue;
            }

            var allowed = GlobalAttributes.Contains(name) || (perTag?.Contains(name) ?? false);
            if (!allowed)
            {
                element.RemoveAttribute(name);
                continue;
            }

            if (name is "href" or "src")
            {
                var safe = SafeAttributeUrl(attribute.Value);
                if (safe is null)
                    element.RemoveAttribute(name);
                else
                    element.SetAttribute(name, safe);
            }
        }
    }

    private static string SanitizeStyle(string value)
    {
        var sb = new StringBuilder();
        foreach (var declaration in value.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
                continue;

            var property = declaration[..colon].Trim();
            if (!AllowedStyleProperties.TryGetValue(property, out var pattern))
                continue;

            var val = declaration[(colon + 1)..].Trim();
            if (!pattern.IsMatch(val))
                continue;

            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(property.ToLowerInvariant()).Append(": ").Append(val).Append(';');
        }
        return sb.ToString();
    }

    private static string? SafeAttributeUrl(string? value)
    {
        if (value is null)
            return null;

        // مرورگر tab/newline/CR را از URL حذف می‌کند؛ ما هم پیش از اعتبارسنجی حذف می‌کنیم
        // تا «java&#9;script:» به javascript: تبدیل و سپس توسط SafeUrl رد شود.
        var cleaned = value.Replace("\t", string.Empty)
                           .Replace("\n", string.Empty)
                           .Replace("\r", string.Empty);
        return SafeUrl.Normalize(cleaned);
    }
}
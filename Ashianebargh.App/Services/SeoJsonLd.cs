using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ashianebargh.Application.Content;

namespace Ashianebargh.App.Services;

/// <summary>
/// سازندهٔ داده‌ساختاریافتهٔ JSON-LD برای صفحات عمومی. همهٔ مقادیر با
/// <see cref="JsonSerializer"/> سریال می‌شوند تا کاراکترهای حساس HTML (&lt;، &gt;،
/// &amp;، <c>"</c>) escape شوند و شکستن تگ &lt;script&gt; ممکن نباشد.
/// </summary>
public static class SeoJsonLd
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // UnicodeRanges.All: متن فارسی به \uXXXX تبدیل نشود، ولی کاراکترهای حساس HTML
        // همچنان escape می‌شوند (برخلاف UnsafeRelaxedJsonEscaping).
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Product(
        string name,
        string? description,
        string url,
        string? imageUrl,
        string? code,
        decimal? priceInToman,
        string categoryName,
        string brand,
        IEnumerable<ProductSpecDto> specs)
    {
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = name,
            ["description"] = description,
            ["url"] = url,
            ["category"] = categoryName,
            ["brand"] = new Dictionary<string, object?>
            {
                ["@type"] = "Brand",
                ["name"] = brand
            },
            ["sku"] = code,
            ["image"] = imageUrl is null ? null : new[] { imageUrl }
        };

        // قیمت در دیتابیس «تومان» است؛ schema.org واحد پول رسمی ایران را IRR (ریال)
        // می‌شناسد، پس برای داده‌ساختاریافته در ۱۰ ضرب می‌شود.
        if (priceInToman is > 0)
        {
            data["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = priceInToman.Value * 10,
                ["priceCurrency"] = "IRR",
                ["availability"] = "https://schema.org/InStock",
                ["url"] = url
            };
        }

        var properties = specs
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) && !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new Dictionary<string, object?>
            {
                ["@type"] = "PropertyValue",
                ["name"] = s.Title,
                ["value"] = s.Value
            })
            .ToList();

        if (properties.Count > 0)
        {
            data["additionalProperty"] = properties;
        }

        return JsonSerializer.Serialize(data, Options);
    }

    public static string Breadcrumb(IEnumerable<(string Name, string Url)> items) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items.Select((item, index) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = item.Name,
                ["item"] = item.Url
            }).ToList()
        }, Options);

    public static string Organization(string name, string url, string? logoUrl) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = name,
            ["url"] = url,
            ["logo"] = logoUrl
        }, Options);
}

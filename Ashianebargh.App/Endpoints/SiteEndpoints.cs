using System.Text;
using Ashianebargh.App.Services;
using Ashianebargh.Application.Content;

namespace Ashianebargh.App.Endpoints;

/// <summary>تنظیمات ظاهری سایت (زبان) و فایل‌های عمومی موتور جست‌وجو.</summary>
public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lang/{code}", (HttpContext http, string code, string? returnUrl) =>
        {
            var value = string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";

            http.Response.Cookies.Append(SiteLanguage.CookieName, value, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            return Results.LocalRedirect(IsLocalPath(returnUrl) ? returnUrl! : "/");
        }).AllowAnonymous();

        // نقشهٔ سایت: پیش‌تر robots.txt به /sitemap.xml اشاره می‌کرد ولی چنین مسیری
        // وجود نداشت و خزنده ۴۰۴ می‌گرفت. حالا فهرست واقعی صفحات، دسته‌ها و محصولات فعال.
        app.MapGet("/sitemap.xml", async (
            HttpContext http,
            IProductService products,
            ICategoryService categories,
            IPageService pages) =>
        {
            var now = DateTime.UtcNow;
            var entries = new List<SitemapEntryDto>
            {
                new("/", now),
                new("/products", now),
                new("/about", now),
                new("/contact", now)
            };

            entries.AddRange(await categories.GetPublicSitemapAsync());
            entries.AddRange(await products.GetPublicSitemapAsync());
            entries.AddRange(await pages.GetPublicSitemapAsync());

            // محتوای سایت کم‌تغییر است؛ کش کوتاه فشار روی دیتابیس را کم می‌کند
            // ولی تغییر تازه‌منتشرشده را هم بیش از یک ساعت پنهان نمی‌کند.
            http.Response.Headers.CacheControl = "public, max-age=3600";

            return Results.Text(
                SitemapBuilder.Build(BaseUrl(http), entries),
                "application/xml",
                Encoding.UTF8);
        }).AllowAnonymous();

        // robots.txt پویا: آدرس sitemap باید مطلق باشد، وگرنه خزنده‌ها آن را نادیده می‌گیرند.
        // مسیرهای خصوصی پنل و احراز هویت هم صریحاً بسته می‌شوند.
        app.MapGet("/robots.txt", (HttpContext http) =>
        {
            var sb = new StringBuilder()
                .AppendLine("User-agent: *")
                .AppendLine("Disallow: /admin/")
                .AppendLine("Disallow: /dashboard")
                .AppendLine("Disallow: /login")
                .AppendLine("Disallow: /login-password")
                .AppendLine("Disallow: /verify-otp")
                .AppendLine("Disallow: /reset-password")
                .AppendLine("Disallow: /profile")
                .AppendLine("Disallow: /sessions")
                .AppendLine("Disallow: /notifications")
                .AppendLine("Disallow: /audit-logs")
                .AllowLine()
                .AppendLine($"Sitemap: {BaseUrl(http)}/sitemap.xml");

            return Results.Text(sb.ToString(), "text/plain", Encoding.UTF8);
        }).AllowAnonymous();

        return app;
    }

    /// <summary>آدرس پایهٔ مطلق؛ ForwardedHeaders قبلاً scheme/host واقعی را ست کرده است.</summary>
    private static string BaseUrl(HttpContext http) =>
        $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}".TrimEnd('/');

    private static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url) &&
        url.StartsWith('/') &&
        !url.StartsWith("//") &&
        !url.StartsWith("/\\");

    /// <summary>خط «Allow: /» بین Disallowها؛ فقط برای خوانایی خروجی.</summary>
    private static StringBuilder AllowLine(this StringBuilder builder) =>
        builder.AppendLine("Allow: /").AppendLine();
}

using Microsoft.AspNetCore.Localization;

namespace Ashianebargh.App.Services;

/// <summary>
/// زبان سایت از کوکی site-lang خوانده می‌شود. بدون کوکی، سایت همیشه فارسی است
/// و زبان مرورگر نادیده گرفته می‌شود؛ بخش مدیریت و حساب کاربری هم مستقل از
/// کوکی، فارسی می‌ماند.
/// </summary>
public sealed class SiteLanguageCultureProvider : RequestCultureProvider
{
    private static readonly string[] AppAreaPrefixes =
    [
        "/admin", "/content", "/dashboard", "/profile", "/sessions", "/notifications",
        "/audit-logs", "/login", "/login-password", "/verify-otp",
        "/reset-password", "/Error", "/not-found"
    ];

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var wantsEnglish = string.Equals(
            httpContext.Request.Cookies[SiteLanguage.CookieName], "en", StringComparison.OrdinalIgnoreCase);
        var isAppArea = AppAreaPrefixes.Any(prefix =>
            httpContext.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<ProviderCultureResult?>(
            wantsEnglish && !isAppArea
                ? new ProviderCultureResult("en-US", "en-US")
                : new ProviderCultureResult("fa-IR", "fa-IR"));
    }
}

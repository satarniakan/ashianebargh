using Ashianebargh.App.Services;

namespace Ashianebargh.App.Endpoints;

/// <summary>تنظیمات ظاهری سایت (فعلاً زبان) برای بازدیدکنندگان.</summary>
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

        return app;
    }

    private static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url) &&
        url.StartsWith('/') &&
        !url.StartsWith("//") &&
        !url.StartsWith("/\\");
}

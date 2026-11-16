namespace Ashianebargh.App.Middleware;

/// <summary>
/// هدرهای امنیتی پایه روی همهٔ پاسخ‌ها (از جمله خطاها).
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// CSP سازگار با Blazor Server و Turnstile.
    /// <para>
    /// <c>'unsafe-inline'</c> عمداً مانده: اسکریپت راه‌انداز Blazor بدون آن اجرا نمی‌شود
    /// و حذفش نیازمند nonce/hash برای همهٔ اسکریپت‌های تولیدی است. در عوض
    /// <c>'unsafe-eval'</c> حذف شده — نه Blazor Server و نه هیچ کتابخانهٔ همراه
    /// (bootstrap/datepicker) از eval/new Function استفاده نمی‌کند.
    /// </para>
    /// <para>
    /// <c>img-src</c> و <c>connect-src</c> محدود به خودِ سرور شده‌اند (حذف <c>https:</c>
    /// و <c>ws:/wss:</c> عام) تا نشت داده به میزبان‌های دلخواه بسته شود. SignalR روی
    /// همان مبدأ است و <c>'self'</c> طبق CSP3 پوشش می‌دهد؛ اگر مرورگری ws/wss را با
    /// 'self' نپذیرد، SignalR به SSE/LongPolling (هر دو httpsِ خودی) برمی‌گردد.
    /// </para>
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' https://challenges.cloudflare.com; " +
        "frame-src https://challenges.cloudflare.com; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'";

    /// <summary>دسترسی به قابلیت‌های حساسِ دستگاه برای همهٔ مبدأها بسته است.</summary>
    public const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
        "magnetometer=(), gyroscope=(), accelerometer=()";

    public static IApplicationBuilder UseAshianebarghSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
            context.Response.Headers["Permissions-Policy"] = PermissionsPolicy;

            // جداسازی مبدأ: پنجرهٔ اصلی از اسناد cross-origin جدا می‌شود (ضد نشت
            // Spectre/window.open). COEP عمداً unsafe-none است؛ require-corp/credentialless
            // iframeِ Turnstile را می‌شکند چون CORP از سمت Cloudflare ارسال نمی‌شود.
            // CORP same-origin جلوی بارگذاری منابع ما را در اسناد بیگانه می‌گیرد.
            context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            context.Response.Headers["Cross-Origin-Embedder-Policy"] = "unsafe-none";

            await next();
        });
}
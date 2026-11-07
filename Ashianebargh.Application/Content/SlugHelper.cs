using System.Text;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Content;

/// <summary>
/// ساخت نامک یکتا-پذیر برای آدرس‌های عمومی. حروف فارسی حفظ می‌شوند، فاصله و
/// نشانه‌ها به خط تیره تبدیل و حروف عربی/ارقام فارسی یکدست می‌شوند.
/// </summary>
public static class SlugHelper
{
    /// <summary>نامک‌هایی که صفحهٔ پویا نباید بگیرد، چون مسیر ثابت سایت یا پنل‌اند.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "about", "contact", "products", "product", "category", "tag",
        "login", "login-password", "logout", "verify-otp", "reset-password",
        "profile", "notifications", "sessions", "audit-logs", "admin", "dashboard",
        "payment-result", "not-found", "error", "health", "health-ui",
        "files", "media", "api", "account", "openapi", "_framework"
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = PersianSearch.Normalize(value).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                // فاصله، خط تیره، زیرخط و هر نشانهٔ دیگر یک جداکنندهٔ واحد می‌سازند
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static bool IsReserved(string slug) => Reserved.Contains(slug);
}

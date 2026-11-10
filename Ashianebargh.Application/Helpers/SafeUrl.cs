namespace Ashianebargh.Application.Helpers;

/// <summary>
/// اعتبارسنجی لینک‌های ورودی کاربر/ادمین پیش از ذخیره یا رندر در href.
/// فقط مسیر نسبیِ داخلی (با یک «/») یا http/https مطلق مجاز است؛
/// <c>javascript:</c>، <c>data:</c> و URLهای protocol-relative (<c>//host</c>) رد می‌شوند
/// تا لینک اعلان به اجرای اسکریپت در مرورگر کاربر تبدیل نشود.
/// </summary>
public static class SafeUrl
{
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var value = url.Trim();
        if (value.StartsWith('/'))
            return value.StartsWith("//") || value.StartsWith("/\\") ? null : value;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : null;
    }
}

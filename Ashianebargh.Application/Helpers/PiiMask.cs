namespace Ashianebargh.Application.Helpers;

/// <summary>
/// پوشاندن دادهٔ شخصی پیش از نوشتن در لاگ. لاگ‌ها روی دیسک می‌مانند و ممکن است
/// در دسترس افرادی باشند که نباید شمارهٔ کامل کاربران را ببینند.
/// </summary>
public static class PiiMask
{
    public static string Phone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "-";

        var value = phone.Trim();
        return value.Length <= 8 ? "***" : $"{value[..4]}***{value[^4..]}";
    }

    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "-";

        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }

    /// <summary>گیرندهٔ Outbox می‌تواند شماره یا ایمیل باشد؛ ایمیل با «@» تشخیص داده می‌شود.</summary>
    public static string Recipient(string? value) =>
        value is not null && value.Contains('@') ? Email(value) : Phone(value);

    /// <summary>
    /// آدرس push یک «capability URL» یکتا برای دستگاه کاربر است (مثل bearer token)؛
    /// فقط میزبان لاگ می‌شود نه مسیر/توکن یکتا، تا شناسهٔ دستگاه در لاگ نشت نکند.
    /// </summary>
    public static string PushEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return "-";

        return Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : "***";
    }

    /// <summary>
    /// پوشاندن شماره/ایمیلِ جاسازی‌شده در متن آزاد (مثل پیام خطای provider پیامک/ایمیل
    /// که ممکن است شماره را echo کند). پیش از ذخیره در Outbox یا لاگ استفاده می‌شود.
    /// </summary>
    public static string FreeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // ایمیل‌ها اول (تا بخشِ.localشان شبیه شماره پردازش نشود)، بعد شماره‌های بلند.
        var masked = System.Text.RegularExpressions.Regex.Replace(
            text, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", m => Email(m.Value));
        masked = System.Text.RegularExpressions.Regex.Replace(
            masked, @"(?:\+?\d[\d\s\-]{7,}\d)", m => Phone(m.Value));
        return masked;
    }
}

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
}

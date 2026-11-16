namespace Ashianebargh.App.Services;

/// <summary>
/// انتخاب متن نمایشی دوزبانه. اگر ترجمهٔ زبان جاری (مثلاً انگلیسی) خالی باشد،
/// به متن پایه (فارسی) برمی‌گردد؛ وگرنه عنوان/متن صفحه یا لینک منو در آن زبان
/// بی‌متن و عملاً مخفی می‌شد.
/// </summary>
public static class LocalizedText
{
    /// <summary>ترجمهٔ معتبر در زبان جاری؛ نبود آن متن پایه برگردانده می‌شود.</summary>
    public static string Pick(bool isCurrentLanguage, string? translated, string? fallback) =>
        isCurrentLanguage && !string.IsNullOrWhiteSpace(translated) ? translated : fallback ?? string.Empty;
}

using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>
/// ترجمهٔ یک صفحه برای یک زبان معین (مثلاً «en» برای انگلیسی).
/// Page.Title/Page.Content به‌صورت فارسی (پیش‌فرض) نگهداری می‌شوند؛
/// ترجمه‌های دیگر در این جدول ذخیره می‌شوند تا مقیاس‌پذیری برای زبان‌های آینده باقی بماند.
/// </summary>
public class PageTranslation : AuditableEntity
{
    public int PageId { get; set; }
    public Page? Page { get; set; }

    /// <summary>کد دو‌حرفی زبان: «fa» یا «en»</summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
}

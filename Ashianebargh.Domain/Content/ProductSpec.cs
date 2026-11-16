using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>
/// یک ردیف مشخصات فنی محصول (عنوان/مقدار). عمداً جدا از HTML توضیحات است تا
/// مشخصات قابل مرتب‌سازی، نمایش جدولی و انتشار به‌صورت داده‌ساختاریافته باشد.
/// </summary>
public class ProductSpec : AuditableEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

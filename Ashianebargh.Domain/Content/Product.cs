using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>محصول قابل نمایش در سایت. تصویر اصلی با شناسهٔ ذخیره‌سازی نگه‌داری می‌شود.</summary>
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>کد فنی/انبارگردانی؛ اختیاری ولی در صورت وجود در جست‌وجوی مدیریتی می‌آید.</summary>
    public string? Code { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string? ShortDescription { get; set; }
    public string? Description { get; set; }

    /// <summary>قیمت به تومان؛ خالی یعنی «قیمت اعلام نشده».</summary>
    public decimal? Price { get; set; }

    public string? MainImageStorageId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

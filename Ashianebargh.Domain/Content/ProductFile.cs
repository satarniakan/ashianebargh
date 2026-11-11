using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>فایل پیوست‌شده به یک محصول (مثلاً PDF مشخصات فنی).</summary>
public class ProductFile : AuditableEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StorageId { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";

    /// <summary>اندازهٔ فایل بر حسب بایت؛ برای نمایش در رابط کاربری.</summary>
    public long SizeBytes { get; set; }
}
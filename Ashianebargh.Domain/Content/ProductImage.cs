using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>تصویر گالری محصول؛ تصویر اصلی همچنان روی خودِ <see cref="Product"/> است.</summary>
public class ProductImage : AuditableEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string StorageId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

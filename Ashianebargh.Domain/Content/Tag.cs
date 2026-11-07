using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>برچسب محصول. رابطهٔ چندبه‌چند با محصول در لایهٔ EF پیکربندی می‌شود.</summary>
public class Tag : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

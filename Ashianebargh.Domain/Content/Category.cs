using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>دستهٔ محصولات. فعلاً یک‌سطحی است؛ نامک یکتا مبنای آدرس عمومی است.</summary>
public class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

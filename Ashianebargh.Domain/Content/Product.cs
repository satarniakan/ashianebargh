using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>Product entity for display on the website. Main image stored via storage ID.
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>Technical code/warehousing; optional but appears in managerial search if present.
    public string? Code { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string? ShortDescription { get; set; }
    public string? ShortDescriptionEn { get; set; }

    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }

    /// <summary>Price in tomans; empty means " Price not announced\.</summary>
 public decimal? Price { get; set; }

 public string? MainImageStorageId { get; set; }
 public bool IsActive { get; set; } = true;
 public bool IsFeatured { get; set; }
 public int SortOrder { get; set; }

 public ICollection<Tag> Tags { get; set; } = new List<Tag>();

public ICollection<ProductFile> Files { get; set; } = new List<ProductFile>();
}

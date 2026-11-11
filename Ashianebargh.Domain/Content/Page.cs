using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>صفحهٔ محتوایی پویا. صفحهٔ «درباره ما» هم یک رکورد با نامک ثابت است.</summary>
public class Page : AuditableEntity
{
    public const string AboutSlug = "about";

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Content { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool ShowInMenu { get; set; }
    public int SortOrder { get; set; }

    /// <summary>ترجمه‌های این صفحه به زبان‌های دیگر.</summary>
    public ICollection<PageTranslation> Translations { get; set; } = new List<PageTranslation>();
}

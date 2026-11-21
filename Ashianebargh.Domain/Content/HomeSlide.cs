using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>اسلاید صفحهٔ اول؛ تصویر و متن روی آن از پنل مدیریت می‌آید. متن اختیاری است (اسلاید فقط‌تصویر).</summary>
public class HomeSlide : AuditableEntity
{
    public string? Title { get; set; }
    public string? TitleEn { get; set; }
    public string? ImageStorageId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
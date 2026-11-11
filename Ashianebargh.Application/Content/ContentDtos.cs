using System.ComponentModel.DataAnnotations;

namespace Ashianebargh.Application.Content;

// ---------- ط¯ط³طھظ‡â€Œط¨ظ†ط¯غŒ ----------

public sealed class CategoryEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "ظ†ط§ظ… ط¯ط³طھظ‡ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(150, ErrorMessage = "ظ†ط§ظ… ط¯ط³طھظ‡ ط­ط¯ط§ع©ط«ط± غ±غµغ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "ظ†ط§ظ…ع© ط­ط¯ط§ع©ط«ط± غ±غµغ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "طھظˆط¶غŒط­ ط­ط¯ط§ع©ط«ط± غ±غ°غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string? Description { get; set; }

    [Range(0, 100000, ErrorMessage = "طھط±طھغŒط¨ ط¨ط§غŒط¯ ط¨غŒظ† غ° طھط§ غ±غ°غ°ظ¬غ°غ°غ° ط¨ط§ط´ط¯.")]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record CategoryListItemDto(
    int Id, string Name, string Slug, string? Description,
    bool IsActive, int SortOrder, int ProductCount, string? ImageStorageId);

// ---------- ط¨ط±ع†ط³ط¨ ----------

public sealed class TagEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "ظ†ط§ظ… ط¨ط±ع†ط³ط¨ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(100, ErrorMessage = "ظ†ط§ظ… ط¨ط±ع†ط³ط¨ ط­ط¯ط§ع©ط«ط± غ±غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "ظ†ط§ظ…ع© ط­ط¯ط§ع©ط«ط± غ±غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string? Slug { get; set; }
}

public sealed record TagListItemDto(int Id, string Name, string Slug, int ProductCount);

// ---------- ظ…ط­طµظˆظ„ ----------

public sealed class ProductEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "ظ†ط§ظ… ظ…ط­طµظˆظ„ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(200, ErrorMessage = "ظ†ط§ظ… ظ…ط­طµظˆظ„ ط­ط¯ط§ع©ط«ط± غ²غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Product name in English (for display when site language is English)</summary>
    [StringLength(200, ErrorMessage = "English title max 200 characters.")]
    public string NameEn { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Slug max 200 characters.")]
    public string? Slug { get; set; }

    [StringLength(50, ErrorMessage = "Product code max 50 characters.")]
    public string? Code { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select product category.")]
    public int CategoryId { get; set; }

    [StringLength(500, ErrorMessage = "Short description max 500 characters.")]
    public string? ShortDescription { get; set; }

    /// <summary>Short description in English</summary>
    [StringLength(500, ErrorMessage = "English short description max 500 characters.")]
    public string? ShortDescriptionEn { get; set; }

    [StringLength(1000, ErrorMessage = "Description max 1000 characters.")]
    public string? Description { get; set; }

    /// <summary>Description in English</summary>
    [StringLength(1000, ErrorMessage = "English description max 1000 characters.")]
    public string? DescriptionEn { get; set; }

    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Price cannot be negative.")]
    public decimal? Price { get; set; }

    public List<int> TagIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    [Range(0, 100000, ErrorMessage = "Sort order must be between 0 and 100000.")]
    public int SortOrder { get; set; }

    /// <summary>Only for displaying current image; changing it is done via upload path.</summary>
    public string? MainImageStorageId { get; set; }

    public List<ProductFileDto> Files { get; set; } = new();
}

public sealed record ProductListItemDto(
    int Id, string Name, string Slug, string? Code, string CategoryName,
    decimal? Price, bool IsActive, bool IsFeatured, int SortOrder, string? MainImageStorageId);

public sealed record ProductCardDto(
    int Id, string Name, string NameEn, string Slug, string? Code, string? ShortDescription, string? ShortDescriptionEn, decimal? Price, string? MainImageStorageId, string CategoryName, string CategorySlug);

public sealed record ProductDetailsDto(
    int Id, string Name, string Slug, string? Code, string CategoryName, string CategorySlug,
    string? ShortDescription, string? Description, decimal? Price, string? MainImageStorageId,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<ProductFileDto> Files);

public sealed record ProductFileDto(int Id, string FileName, string StorageId, string ContentType, long SizeBytes);

/// <summary>اطلاعات فایل برای دانلود عمومی (بدون شناسهٔ ذخیره‌سازی در URL).</summary>
public sealed record ProductFileDownloadDto(int Id, string FileName, string StorageId, string ContentType, long SizeBytes);

public sealed record TagDto(int Id, string Name, string Slug);

public sealed class ProductFilter
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? TagId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PublicProductFilter
{
    public string? CategorySlug { get; set; }
    public string? TagSlug { get; set; }

    /// <summary>عبارت جست‌وجوی عمومی سایت (نام، کد یا توضیح محصول).</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

// ---------- طµظپط­ظ‡ ----------

public sealed class PageEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "ط¹ظ†ظˆط§ظ† طµظپط­ظ‡ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(200, ErrorMessage = "ط¹ظ†ظˆط§ظ† ط­ط¯ط§ع©ط«ط± غ²غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>ط¹ظ†ظˆط§ظ† ط§ظ†ع¯ظ„غŒط³غŒ طµظپط­ظ‡ (ط¨ط±ط§غŒ ظ†ظ…ط§غŒط´ ظˆظ‚طھغŒ ط²ط¨ط§ظ† ط³ط§غŒطھ ط§ظ†ع¯ظ„غŒط³غŒ ط§ط³طھ)</summary>
    [StringLength(200, ErrorMessage = "ط¹ظ†ظˆط§ظ† ط§ظ†ع¯ظ„غŒط³غŒ ط­ط¯ط§ع©ط«ط± غ²غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string TitleEn { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "ظ†ط§ظ…ع© ط­ط¯ط§ع©ط«ط± غ²غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string? Slug { get; set; }

    public string? Content { get; set; }

    /// <summary>ظ…طھظ† ط§ظ†ع¯ظ„غŒط³غŒ طµظپط­ظ‡ (ط¨ط±ط§غŒ ظ†ظ…ط§غŒط´ ظˆظ‚طھغŒ ط²ط¨ط§ظ† ط³ط§غŒطھ ط§ظ†ع¯ظ„غŒط³غŒ ط§ط³طھ)</summary>
    public string? ContentEn { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool ShowInMenu { get; set; }

    [Range(0, 100000, ErrorMessage = "طھط±طھغŒط¨ ط¨ط§غŒط¯ ط¨غŒظ† غ° طھط§ غ±غ°غ°ظ¬غ°غ°غ° ط¨ط§ط´ط¯.")]
    public int SortOrder { get; set; }
}

public sealed record PageListItemDto(
    int Id, string Title, string TitleEn, string Slug, bool IsPublished, bool ShowInMenu, int SortOrder, DateTime CreatedAt);

public sealed record PageMenuItemDto(string Title, string TitleEn, string Slug);

public sealed record PageViewDto(string Title, string TitleEn, string Slug, string? Content, string? ContentEn, DateTime? UpdatedAt);

// ---------- طھظ…ط§ط³ ط¨ط§ ظ…ط§ ----------

public sealed class ContactInfoDto
{
    [StringLength(500, ErrorMessage = "ظ†ط´ط§ظ†غŒ ط­ط¯ط§ع©ط«ط± غµغ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Mobile { get; set; }

    [StringLength(200, ErrorMessage = "ط§غŒظ…غŒظ„ ط­ط¯ط§ع©ط«ط± غ²غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    [EmailAddress(ErrorMessage = "ط§غŒظ…غŒظ„ ظ…ط¹طھط¨ط± ظ†غŒط³طھ.")]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? WorkingHours { get; set; }

    [StringLength(500)]
    public string? MapUrl { get; set; }

    [StringLength(300)]
    public string? Instagram { get; set; }

    [StringLength(300)]
    public string? Telegram { get; set; }

    [StringLength(300)]
    public string? WhatsApp { get; set; }
}

public sealed class ContactFormDto
{
    [Required(ErrorMessage = "ظ†ط§ظ… ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(100, ErrorMessage = "ظ†ط§ظ… ط­ط¯ط§ع©ط«ط± غ±غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ط´ظ…ط§ط±ظ‡ طھظ…ط§ط³ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(30, ErrorMessage = "ط´ظ…ط§ط±ظ‡ طھظ…ط§ط³ ط­ط¯ط§ع©ط«ط± غ³غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "ط§غŒظ…غŒظ„ ظ…ط¹طھط¨ط± ظ†غŒط³طھ.")]
    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "ظ…طھظ† ظ¾غŒط§ظ… ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.")]
    [StringLength(2000, ErrorMessage = "ظ…طھظ† ظ¾غŒط§ظ… ط­ط¯ط§ع©ط«ط± غ²غ°غ°غ° ظ†ظˆغŒط³ظ‡ ط§ط³طھ.")]
    public string Message { get; set; } = string.Empty;
}

public sealed record ContactMessageDto(
    int Id, string Name, string Phone, string? Email, string? Subject,
    string Message, bool IsRead, DateTime CreatedAt);

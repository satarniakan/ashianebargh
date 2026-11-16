using System.ComponentModel.DataAnnotations;

namespace Ashianebargh.Application.Content;

// ---------- دسته‌بندی ----------

public sealed class CategoryEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام دسته را وارد کنید.")]
    [StringLength(150, ErrorMessage = "نام دسته حداکثر ۱۵۰ نویسه است.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "نامک حداکثر ۱۵۰ نویسه است.")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "توضیح حداکثر ۱۰۰۰ نویسه است.")]
    public string? Description { get; set; }

    [Range(0, 100000, ErrorMessage = "ترتیب باید بین ۰ تا ۱۰۰٬۰۰۰ باشد.")]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record CategoryListItemDto(
    int Id, string Name, string Slug, string? Description,
    bool IsActive, int SortOrder, int ProductCount, string? ImageStorageId);

// ---------- برچسب ----------

public sealed class TagEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام برچسب را وارد کنید.")]
    [StringLength(100, ErrorMessage = "نام برچسب حداکثر ۱۰۰ نویسه است.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "نامک حداکثر ۱۰۰ نویسه است.")]
    public string? Slug { get; set; }
}

public sealed record TagListItemDto(int Id, string Name, string Slug, int ProductCount);

// ---------- محصول ----------

public sealed class ProductEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام محصول را وارد کنید.")]
    [StringLength(200, ErrorMessage = "نام محصول حداکثر ۲۰۰ نویسه است.")]
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

    /// <summary>ردیف‌های مشخصات فنی (عنوان/مقدار) که در سایت جدول می‌شوند.</summary>
    public List<ProductSpecDto> Specs { get; set; } = new();

    /// <summary>تصاویر گالری (به‌جز تصویر اصلی).</summary>
    public List<ProductImageDto> Images { get; set; } = new();
}

public sealed record ProductListItemDto(
    int Id, string Name, string Slug, string? Code, string CategoryName,
    decimal? Price, bool IsActive, bool IsFeatured, int SortOrder, string? MainImageStorageId);

public sealed record ProductCardDto(
    int Id, string Name, string NameEn, string Slug, string? Code, string? ShortDescription, string? ShortDescriptionEn, decimal? Price, string? MainImageStorageId, string CategoryName, string CategorySlug);

public sealed record ProductDetailsDto(
    int Id, string Name, string NameEn, string Slug, string? Code, string CategoryName, string CategorySlug,
    string? ShortDescription, string? ShortDescriptionEn, string? Description, string? DescriptionEn,
    decimal? Price, string? MainImageStorageId,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<ProductFileDto> Files,
    IReadOnlyList<ProductSpecDto> Specs,
    IReadOnlyList<ProductImageDto> Images);

/// <summary>یک ردیف مشخصات فنی محصول.</summary>
public sealed record ProductSpecDto(string Title, string Value);

/// <summary>یک تصویر گالری محصول.</summary>
public sealed record ProductImageDto(int Id, string StorageId);

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

/// <summary>ردیف نقشهٔ سایت: مسیر نسبی و آخرین تغییر (برای lastmod).</summary>
public sealed record SitemapEntryDto(string Path, DateTime LastModified);
public sealed class PublicProductFilter
{
    public string? CategorySlug { get; set; }
    public string? TagSlug { get; set; }

    /// <summary>عبارت جست‌وجوی عمومی سایت (نام، کد یا توضیح محصول).</summary>
    public string? Search { get; set; }

    /// <summary>ترتیب نمایش: default | newest | price-asc | price-desc | name.</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

// ---------- صفحه ----------

public sealed class PageEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان صفحه را وارد کنید.")]
    [StringLength(200, ErrorMessage = "عنوان حداکثر ۲۰۰ نویسه است.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>عنوان انگلیسی صفحه (برای نمایش وقتی زبان سایت انگلیسی است)</summary>
    [StringLength(200, ErrorMessage = "عنوان انگلیسی حداکثر ۲۰۰ نویسه است.")]
    public string TitleEn { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "نامک حداکثر ۲۰۰ نویسه است.")]
    public string? Slug { get; set; }

    public string? Content { get; set; }

    /// <summary>متن انگلیسی صفحه (برای نمایش وقتی زبان سایت انگلیسی است)</summary>
    public string? ContentEn { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool ShowInMenu { get; set; }

    [Range(0, 100000, ErrorMessage = "ترتیب باید بین ۰ تا ۱۰۰٬۰۰۰ باشد.")]
    public int SortOrder { get; set; }
}

public sealed record PageListItemDto(
    int Id, string Title, string TitleEn, string Slug, bool IsPublished, bool ShowInMenu, int SortOrder, DateTime CreatedAt);

public sealed record PageMenuItemDto(string Title, string TitleEn, string Slug);

public sealed record PageViewDto(string Title, string TitleEn, string Slug, string? Content, string? ContentEn, DateTime? UpdatedAt);

// ---------- تماس با ما ----------

public sealed class ContactInfoDto
{
    [StringLength(500, ErrorMessage = "نشانی حداکثر ۵۰۰ نویسه است.")]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Mobile { get; set; }

    [StringLength(200, ErrorMessage = "ایمیل حداکثر ۲۰۰ نویسه است.")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
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
    [Required(ErrorMessage = "نام را وارد کنید.")]
    [StringLength(100, ErrorMessage = "نام حداکثر ۱۰۰ نویسه است.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره تماس را وارد کنید.")]
    [StringLength(30, ErrorMessage = "شماره تماس حداکثر ۳۰ نویسه است.")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "متن پیام را وارد کنید.")]
    [StringLength(2000, ErrorMessage = "متن پیام حداکثر ۲۰۰۰ نویسه است.")]
    public string Message { get; set; } = string.Empty;
}

public sealed record ContactMessageDto(
    int Id, string Name, string Phone, string? Email, string? Subject,
    string Message, bool IsRead, DateTime CreatedAt);

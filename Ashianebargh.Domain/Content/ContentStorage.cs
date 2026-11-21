namespace Ashianebargh.Domain.Content;

/// <summary>
/// مالک و نوع فایل‌های محتوای سایت. مالک ثابت است تا آپلود پیش از ذخیرهٔ محصول
/// ممکن باشد و مسیر عمومی تصویر به شناسهٔ محصول وابسته نباشد.
/// </summary>
public static class ContentStorage
{
    public const string Owner = "site-products";
    public const string ProductImageType = "product-image";
    public const string ProductFileType = "product-file";

    /// <summary>کاتالوگ عمومی سایت (یک‌بار ذخیره می‌شود، نه برای هر محصول).</summary>
    public const string CatalogType = "site-catalog";

    /// <summary>تصاویر اسلایدر صفحهٔ اول.</summary>
    public const string SliderImageType = "slider-image";

    /// <summary>تصویر اختصاصی دسته؛ نبودش یعنی کارت دسته از تصویر محصول یا آیکن استفاده می‌کند.</summary>
    public const string CategoryImageType = "category-image";
}

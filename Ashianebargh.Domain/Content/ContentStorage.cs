namespace Ashianebargh.Domain.Content;

/// <summary>
/// مالک و نوع فایل‌های محتوای سایت. مالک ثابت است تا آپلود پیش از ذخیرهٔ محصول
/// ممکن باشد و مسیر عمومی تصویر به شناسهٔ محصول وابسته نباشد.
/// </summary>
public static class ContentStorage
{
    public const string Owner = "site-products";
    public const string ProductImageType = "product-image";
}

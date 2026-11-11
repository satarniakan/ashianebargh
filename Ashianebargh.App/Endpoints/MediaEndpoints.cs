using Microsoft.AspNetCore.StaticFiles;
using Ashianebargh.Application.Content;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.App.Endpoints;

/// <summary>
/// تصاویر محصولات برای بازدیدکنندگان سایت — بدون نیاز به ورود.
/// فقط شناسه‌های موجود در پوشهٔ تصویر محصول و فقط نوع محتوای تصویری سرو می‌شوند؛
/// مسیر از storageId ساخته می‌شود نه نام فایل کاربر.
/// </summary>
public static class MediaEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/products/{storageId}", async (
            HttpContext http,
            IFileStorage storage,
            string storageId) =>
        {
            if (!IsSafeStorageId(storageId))
                return Results.NotFound();

            if (!await storage.ExistsAsync(ContentStorage.Owner, ContentStorage.ProductImageType, storageId))
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(ContentStorage.Owner, ContentStorage.ProductImageType, storageId);
            if (stream is null)
                return Results.NotFound();

            if (!ContentTypes.TryGetContentType(storageId, out var contentType) ||
                !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Results.NotFound();

            http.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";

            return Results.Stream(stream, contentType);
        })
        .AllowAnonymous();

        app.MapGet("/media/product-files/{storageId}", async (
            HttpContext http,
            IFileStorage storage,
            string storageId) =>
        {
            if (!IsSafeStorageId(storageId))
                return Results.NotFound();

            if (!await storage.ExistsAsync(ContentStorage.Owner, ContentStorage.ProductFileType, storageId))
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(ContentStorage.Owner, ContentStorage.ProductFileType, storageId);
            if (stream is null)
                return Results.NotFound();

            if (!ContentTypes.TryGetContentType(storageId, out var contentType))
                contentType = "application/octet-stream";

            http.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";

            return Results.Stream(stream, contentType);
        })
        .AllowAnonymous();

        // کاتالوگ عمومی سایت — یک‌بار ذخیره می‌شود، نه برای هر محصول.
        app.MapGet("/media/catalog", async (
            HttpContext http,
            IFileStorage storage,
            ISettingService settings) =>
        {
            var storageId = await settings.GetAsync(Ashianebargh.Domain.Identity.FrameworkSettingKeys.SiteCatalogFile);
            if (string.IsNullOrWhiteSpace(storageId) || !IsSafeStorageId(storageId))
                return Results.NotFound();

            if (!await storage.ExistsAsync(ContentStorage.Owner, ContentStorage.CatalogType, storageId))
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(ContentStorage.Owner, ContentStorage.CatalogType, storageId);
            if (stream is null)
                return Results.NotFound();

            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers.ContentDisposition =
                "attachment; filename=\"TKS-Catalog.pdf\"";

            return Results.Stream(stream, "application/pdf");
        })
        .AllowAnonymous();

        // دانلود با نام اصلی فایل (مثلاً «TKS-1101.pdf») — شناسهٔ فایل از دیتابیس
        // اعتبارسنجی می‌شود تا فقط فایل‌های محصولات فعال سرو شوند.
        app.MapGet("/media/product-files/{id:int}/download", async (
            HttpContext http,
            IFileStorage storage,
            IProductService products,
            int id) =>
        {
            var file = await products.GetFileForDownloadAsync(id);
            if (file is null)
                return Results.NotFound();

            if (!await storage.ExistsAsync(ContentStorage.Owner, ContentStorage.ProductFileType, file.StorageId))
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(ContentStorage.Owner, ContentStorage.ProductFileType, file.StorageId);
            if (stream is null)
                return Results.NotFound();

            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers.ContentDisposition =
                $"attachment; filename=\"{SanitizeFileName(file.FileName)}\"; " +
                $"filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}";

            return Results.Stream(stream, file.ContentType);
        })
        .AllowAnonymous();

        return app;
    }

    /// <summary>نام فایل برای هدر پاسخ؛ کاراکترهای کنترلی و گیومه حذف می‌شوند.</summary>
    private static string SanitizeFileName(string fileName)
    {
        var safe = new string(fileName.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();
        return string.IsNullOrEmpty(safe) ? "file.pdf" : safe;
    }

    private static bool IsSafeStorageId(string storageId) =>
        storageId.Length is > 0 and <= 100 &&
        string.Equals(Path.GetFileName(storageId), storageId, StringComparison.Ordinal);
}

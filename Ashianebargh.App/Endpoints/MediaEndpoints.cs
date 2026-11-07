using Microsoft.AspNetCore.StaticFiles;
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

        return app;
    }

    private static bool IsSafeStorageId(string storageId) =>
        storageId.Length is > 0 and <= 100 &&
        string.Equals(Path.GetFileName(storageId), storageId, StringComparison.Ordinal);
}

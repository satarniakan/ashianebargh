using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.App.Endpoints;

/// <summary>
/// دانلود فایل‌های <see cref="IFileStorage"/> بدون UseStaticFilesِ بی‌مجوز.
/// <para>
/// امنیت: مسیر از storageId (ناگهانی) ساخته می‌شود نه نام فایل؛ مجوز با
/// <see cref="IFileAccessPolicy"/> (پیش‌فرض: مالک یا ادمین)؛ پاسخ همیشه attachment
/// با <c>X-Content-Type-Options: nosniff</c> تا فایل آپلودشده در مسیر سایت اجرا نشود.
/// </para>
/// <para>
/// محدودیت عمداً ساده‌نگرانه: پاسخ stream کامل است (Range ندارد)، پس جابه‌جاییِ
/// audio/video در player پیش‌پردازش می‌خواهد. اگر دامنه‌ات player دارد، Range را
/// خودش روی همین endpoint اضافه کند.
/// </para>
/// </summary>
public static class FileEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/files/{ownerId}/{fileType}/{storageId}", async (
            HttpContext http,
            ClaimsPrincipal user,
            IFileStorage storage,
            [FromServices] IFileAccessPolicy access,
            string ownerId,
            string fileType,
            string storageId) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            // مسیر از سه بخشِ ورودیِ کاربر ساخته می‌شود؛ فقط شناسه‌های ساده مجازند.
            // (ذخیره‌سازی هم مسیر نهایی را بررسی می‌کند، ولی ردکردن پیش از هر I/O
            // جلوی ../ و کاراکترهای تزریقی در نام هدر Content-Disposition را می‌گیرد.)
            if (!IsSafeSegment(ownerId) || !IsSafeSegment(fileType) || !IsSafeSegment(storageId))
                return Results.NotFound();

            var isAdmin = user.IsInRole(Ashianebargh.Domain.Identity.Roles.Admin);
            if (!await access.CanReadAsync(userId, isAdmin, ownerId, fileType))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            // اگر فایل نبود 404 صریح؛ 403 نده چون مالکیت قبلاً بررسی شده
            if (!await storage.ExistsAsync(ownerId, fileType, storageId))
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(ownerId, fileType, storageId);
            if (stream is null)
                return Results.NotFound();

            // contentType از پسوند؛ ناشناخته → octet-stream (دانلود امن)
            if (!ContentTypes.TryGetContentType(storageId, out var contentType))
                contentType = "application/octet-stream";

            http.Response.Headers.ContentDisposition =
                $"attachment; filename=\"{storageId}\"";
            http.Response.Headers.CacheControl = "private, no-store";

            // اتمیک: اگر هیچ بایتی نخوانده، stream هم باید dispose شود
            return Results.Stream(stream, contentType);
        });

        return app;

        static bool IsSafeSegment(string value) =>
            value.Length is > 0 and <= 100
            && value != "." && value != ".."
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    }
}
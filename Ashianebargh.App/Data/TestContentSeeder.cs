using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ashianebargh.App.Data;

/// <summary>
/// دادهٔ نمونهٔ توسعه برای صفحه‌های عمومی سایت. فقط با فلگ
/// <c>--seed-test-content</c> و در محیط Development اجرا می‌شود و تکراری نمی‌سازد.
/// <para>
/// داده از <c>SeedAssets/products/manifest.json</c> خوانده می‌شود: دسته‌ها، محصولات
/// با تصویر و جدول مشخصات، و فایل‌های پیوست PDF. چون این داده فقط نمونه است،
/// هر بار کامل جایگزین می‌شود (سخت‌حذف) تا فایل‌های ذخیره‌سازی روی هم جمع نشوند.
/// </para>
/// </summary>
public static class TestContentSeeder
{
    private sealed record ManifestCategory(string slug, string name, string description, int sortOrder);
    private sealed record ManifestFile(string name, string file);
    private sealed record ManifestProduct(
        string category, string group, string name, string code, string slug,
        string shortDescription, string description, string? imageFile,
        List<ManifestFile>? files);

    private sealed class ManifestRoot
    {
        public List<ManifestCategory> categories { get; set; } = new();
        public List<ManifestProduct> products { get; set; } = new();
        public string? catalog { get; set; }
    }

    public static async Task SeedAsync(
        AshianebarghDbContext db,
        IFileStorage storage,
        string assetsPath,
        ILogger logger)
    {
        var manifestPath = Path.Combine(assetsPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            logger.LogWarning("manifest.json در {Path} یافت نشد؛ seed محصولات انجام نشد.", manifestPath);
            return;
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<ManifestRoot>(manifestJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new ManifestRoot();

        await PurgePreviousSampleDataAsync(db, storage);

        var categoriesBySlug = new Dictionary<string, Category>(StringComparer.Ordinal);
        foreach (var mc in manifest.categories)
        {
            var category = new Category
            {
                Name = mc.name,
                Slug = mc.slug,
                Description = mc.description,
                SortOrder = mc.sortOrder,
                IsActive = true
            };
            db.Set<Category>().Add(category);
            categoriesBySlug[mc.slug] = category;
        }
        await db.SaveChangesAsync();

        var addedProducts = 0;
        var addedFiles = 0;
        var sort = 0;
        var featuredCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mp in manifest.products)
        {
            if (!categoriesBySlug.TryGetValue(mp.category, out var category))
                continue;

            featuredCount.TryGetValue(mp.category, out var seenInCategory);
            featuredCount[mp.category] = seenInCategory + 1;

            string? storageId = null;
            if (!string.IsNullOrWhiteSpace(mp.imageFile))
            {
                var imagePath = Path.Combine(assetsPath, mp.imageFile);
                if (File.Exists(imagePath))
                {
                    await using var stream = File.OpenRead(imagePath);
                    storageId = await storage.SaveAsync(
                        ContentStorage.Owner, ContentStorage.ProductImageType,
                        stream, mp.imageFile, GuessContentType(mp.imageFile));
                }
            }

            var product = new Product
            {
                Name = mp.name,
                NameEn = mp.name,
                Slug = mp.slug,
                Code = mp.code,
                Category = category,
                ShortDescription = mp.shortDescription,
                ShortDescriptionEn = mp.shortDescription,
                Description = mp.description,
                DescriptionEn = mp.description,
                Price = null,
                MainImageStorageId = storageId,
                IsActive = true,
                // چند محصول اول هر دسته به‌عنوان «ویژه» تا صفحهٔ اصلی خالی نباشد
                IsFeatured = featuredCount.TryGetValue(mp.category, out var seen) && seen < 2,
                SortOrder = sort++
            };
            db.Set<Product>().Add(product);
            addedProducts++;

            // پیوست‌ها با ناوبری رابطه اضافه می‌شوند تا کلید خارجی درست ساخته شود.
            foreach (var mf in mp.files ?? [])
            {
                var pdfPath = Path.Combine(assetsPath, mf.file);
                if (!File.Exists(pdfPath)) continue;

                string pdfStorageId;
                await using (var stream = File.OpenRead(pdfPath))
                {
                    pdfStorageId = await storage.SaveAsync(
                        ContentStorage.Owner, ContentStorage.ProductFileType,
                        stream, mf.name, "application/pdf");
                }

                product.Files.Add(new ProductFile
                {
                    FileName = mf.name,
                    StorageId = pdfStorageId,
                    ContentType = "application/pdf",
                    SizeBytes = new FileInfo(pdfPath).Length
                });
                addedFiles++;
            }
        }

        await db.SaveChangesAsync();

        // کاتالوگ عمومی سایت یک‌بار ذخیره می‌شود (نه برای هر محصول) و شناسه‌اش
        // در تنظیمات می‌نشیند تا لینک دانلود آن در پابرگ کار کند.
        var catalogStorageId = await SeedCatalogAsync(db, storage, assetsPath, manifest.catalog);

        logger.LogInformation(
            "دادهٔ نمونهٔ سایت: {Categories} دسته، {Products} محصول، {Files} فایل پیوست{Catalog}.",
            manifest.categories.Count, addedProducts, addedFiles,
            catalogStorageId is null ? "" : " و کاتالوگ عمومی");
    }

    /// <summary>
    /// دادهٔ نمونهٔ قبلی کامل پاک می‌شود (سخت‌حذف) و فایل‌هایش از فضای ذخیره‌سازی
    /// هم حذف می‌شوند؛ وگرنه هر بار seed حجم دیسک را چند برابر می‌کرد.
    /// </summary>
    private static async Task PurgePreviousSampleDataAsync(AshianebarghDbContext db, IFileStorage storage)
    {
        var imageIds = await db.Set<Product>()
            .IgnoreQueryFilters()
            .Where(p => p.MainImageStorageId != null)
            .Select(p => p.MainImageStorageId!)
            .ToListAsync();
        var fileIds = await db.Set<ProductFile>()
            .IgnoreQueryFilters()
            .Select(f => f.StorageId)
            .ToListAsync();

        // نام جدول‌ها از خودِ مدل EF خوانده می‌شود تا تغییر نگاشت، seed را نشکند.
        foreach (var type in new[] { typeof(ProductFile), typeof(Product), typeof(Category), typeof(Tag) })
        {
            var table = db.Model.FindEntityType(type)?.GetTableName();
            if (string.IsNullOrEmpty(table)) continue;
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}]");
        }
        db.ChangeTracker.Clear();

        foreach (var id in imageIds)
        {
            try { await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.ProductImageType, id); }
            catch { /* پاک‌سازی فایل نباید seed را متوقف کند */ }
        }
        foreach (var id in fileIds)
        {
            try { await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.ProductFileType, id); }
            catch { /* پاک‌سازی فایل نباید seed را متوقف کند */ }
        }
    }

    /// <summary>کاتالوگ عمومی: یک‌بار ذخیره و شناسه‌اش در تنظیمات ثبت می‌شود.</summary>
    private static async Task<string?> SeedCatalogAsync(
        AshianebarghDbContext db, IFileStorage storage, string assetsPath, string? catalogFile)
    {
        var previous = await db.Set<Setting>()
            .IgnoreQueryFilters()
            .Where(s => s.Key == FrameworkSettingKeys.SiteCatalogFile)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(previous))
        {
            try { await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.CatalogType, previous); }
            catch { /* پاک‌سازی فایل نباید seed را متوقف کند */ }
        }

        if (string.IsNullOrWhiteSpace(catalogFile))
            return null;

        var catalogPath = Path.Combine(assetsPath, catalogFile);
        if (!File.Exists(catalogPath))
            return null;

        string storageId;
        await using (var stream = File.OpenRead(catalogPath))
        {
            storageId = await storage.SaveAsync(
                ContentStorage.Owner, ContentStorage.CatalogType,
                stream, catalogFile, "application/pdf");
        }

        db.Set<Setting>().RemoveRange(await db.Set<Setting>()
            .IgnoreQueryFilters()
            .Where(s => s.Key == FrameworkSettingKeys.SiteCatalogFile)
            .ToListAsync());
        db.Set<Setting>().Add(new Setting
        {
            Key = FrameworkSettingKeys.SiteCatalogFile,
            Value = storageId,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return storageId;
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
    }
}

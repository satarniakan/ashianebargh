using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.Application.Content;
using Ashianebargh.App.Endpoints;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Content;
using Ashianebargh.Infrastructure.Data;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// کاتالوگ عمومی: مرتب‌سازی، محصولات مرتبط، مشخصات فنی، گالری تصاویر و نقشهٔ سایت.
/// گارد «تصویر گالری محصول غیرفعال نباید سرو شود» رگرسیون امنیتی است.
/// </summary>
public class ProductCatalogTests
{
    private static (AshianebarghDbContext Db, ContentUnitOfWork Uow, Mock<IFileStorage> Storage) Build()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"catalog-{Guid.NewGuid()}")
            .Options;

        var db = new AshianebarghDbContext(options, new IAshianebarghModule[] { new ContentModelModule() });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(Mock.Of<IOtpRepository>());
        services.AddSingleton(Mock.Of<IAuditLogRepository>());
        services.AddSingleton(Mock.Of<INotificationRepository>());
        services.AddSingleton(Mock.Of<IOutboxRepository>());
        var provider = services.BuildServiceProvider();

        return (db, new ContentUnitOfWork(db, provider), new Mock<IFileStorage>());
    }

    private static ProductService Service(ContentUnitOfWork uow, Mock<IFileStorage> storage) =>
        new(uow, storage.Object, NullLogger<ProductService>.Instance);

    private static async Task<Category> SeedCategoryAsync(AshianebarghDbContext db, string name, string slug)
    {
        var category = new Category { Name = name, Slug = slug, IsActive = true };
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> SeedProductAsync(
        AshianebarghDbContext db,
        Category category,
        string name,
        string slug,
        decimal? price = null,
        bool isActive = true,
        DateTime? createdAt = null)
    {
        var product = new Product
        {
            Name = name,
            NameEn = name,
            Slug = slug,
            Code = slug.ToUpperInvariant(),
            CategoryId = category.Id,
            Category = category,
            Price = price,
            IsActive = isActive,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task PublicSort_PriceAscending_KeepsCallForPriceProductsLast()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db, "ترمینال", "terminal");
        await SeedProductAsync(db, category, "گران", "expensive", 900_000);
        await SeedProductAsync(db, category, "بدون قیمت", "no-price", null);
        await SeedProductAsync(db, category, "ارزان", "cheap", 100_000);

        var result = await Service(uow, storage)
            .GetPublicPagedAsync(new PublicProductFilter { Sort = "price-asc" });

        Assert.Equal(["cheap", "expensive", "no-price"], result.Items.Select(p => p.Slug));
    }

    [Fact]
    public async Task PublicSort_Newest_UsesCreationTime()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db, "کانکتور", "connector");
        await SeedProductAsync(db, category, "قدیمی", "old", createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedProductAsync(db, category, "تازه", "new", createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await Service(uow, storage)
            .GetPublicPagedAsync(new PublicProductFilter { Sort = "newest" });

        Assert.Equal(["new", "old"], result.Items.Select(p => p.Slug));
    }

    [Fact]
    public async Task Related_ExcludesCurrentProductAndOtherCategories()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var terminal = await SeedCategoryAsync(db, "ترمینال", "terminal");
        var other = await SeedCategoryAsync(db, "کانکتور", "connector");
        var current = await SeedProductAsync(db, terminal, "محصول جاری", "current");
        await SeedProductAsync(db, terminal, "هم‌دسته", "sibling");
        await SeedProductAsync(db, other, "دستهٔ دیگر", "other-cat");

        var related = await Service(uow, storage).GetRelatedAsync(current.Id, "terminal", 4);

        Assert.Single(related);
        Assert.Equal("sibling", related[0].Slug);
    }

    [Fact]
    public async Task SaveAsync_PersistsEnglishFieldsAndSpecs_InOrder()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db, "روشنایی", "lighting");
        var service = Service(uow, storage);

        var id = await service.SaveAsync(new ProductEditDto
        {
            Name = "چراغ خطی",
            NameEn = "Linear Light",
            ShortDescription = "چراغ خطی ۳۶ وات",
            ShortDescriptionEn = "36W linear light",
            CategoryId = category.Id,
            Slug = "linear-light",
            Specs =
            [
                new ProductSpecDto("توان", "۳۶ وات"),
                new ProductSpecDto("دمای رنگ", "۴۰۰۰ کلوین"),
                new ProductSpecDto(string.Empty, "ردیف خالی باید حذف شود")
            ]
        });

        var edit = await service.GetForEditAsync(id);
        Assert.NotNull(edit);
        Assert.Equal("Linear Light", edit!.NameEn);
        Assert.Equal("36W linear light", edit.ShortDescriptionEn);
        Assert.Equal(2, edit.Specs.Count);

        var details = await service.GetPublicBySlugAsync("linear-light");
        Assert.NotNull(details);
        Assert.Equal("Linear Light", details!.NameEn);
        Assert.Equal(["توان", "دمای رنگ"], details.Specs.Select(s => s.Title));
        Assert.Equal("۳۶ وات", details.Specs[0].Value);
    }

    [Fact]
    public async Task GalleryImage_IsPublicOnlyWhileProductIsActive()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db, "کلید", "switch");
        var product = await SeedProductAsync(db, category, "کلید مینیاتوری", "mcb");

        db.Set<ProductImage>().Add(new ProductImage
        {
            ProductId = product.Id,
            StorageId = "gallery-1.jpg",
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var service = Service(uow, storage);
        Assert.True(await service.IsImagePublicAsync("gallery-1.jpg"));

        // رگرسیون امنیتی: غیرفعال‌کردن محصول باید دسترسی به تصویر گالری را هم ببندد.
        product.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await service.IsImagePublicAsync("gallery-1.jpg"));
    }

    [Fact]
    public async Task Sitemap_IncludesOnlyActiveProducts()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db, "سنسور", "sensor");
        await SeedProductAsync(db, category, "سنسور حرکتی", "motion-sensor");
        await SeedProductAsync(db, category, "غیرفعال", "inactive", isActive: false);

        var entries = await Service(uow, storage).GetPublicSitemapAsync();

        Assert.Single(entries);
        Assert.Equal("/product/motion-sensor", entries[0].Path);
    }

    [Fact]
    public void Sitemap_BuildsAbsoluteUrls_DeduplicatesAndEscapes()
    {
        var xml = SitemapBuilder.Build("https://example.com/",
        [
            new SitemapEntryDto("/products", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            new SitemapEntryDto("/products", new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)),
            new SitemapEntryDto("/product/a&b", new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc)),
            new SitemapEntryDto(string.Empty, DateTime.UtcNow)
        ]);

        Assert.Contains("<loc>https://example.com/products</loc>", xml);
        Assert.Contains("<lastmod>2026-01-02</lastmod>", xml);
        // مسیر تکراری باید یک بار بیاید و «&» باید escape شود.
        Assert.Equal(1, CountOccurrences(xml, "<loc>https://example.com/products</loc>"));
        Assert.Contains("a&amp;b", xml);
        Assert.DoesNotContain("<loc>https://example.com</loc>", xml);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.Application.Content;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Content;
using Ashianebargh.Infrastructure.Data;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// جست‌وجوی عمومی محصولات و پیوست فایل‌ها: عبارت جست‌وجو باید روی نام/کد/توضیح
/// اثر بگذارد و فایل‌های پیوست باید با اندازهٔ درست ثبت و برای دانلود سرو شوند.
/// </summary>
public class ProductSearchAndFilesTests
{
    private static (AshianebarghDbContext db, ContentUnitOfWork uow, Mock<IFileStorage> storage) Build()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"products-{Guid.NewGuid()}")
            .Options;

        var db = new AshianebarghDbContext(options, new IAshianebarghModule[] { new ContentModelModule() });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(Mock.Of<IOtpRepository>());
        services.AddSingleton(Mock.Of<IAuditLogRepository>());
        services.AddSingleton(Mock.Of<INotificationRepository>());
        services.AddSingleton(Mock.Of<IOutboxRepository>());
        var provider = services.BuildServiceProvider();

        var storage = new Mock<IFileStorage>();
        storage.Setup(s => s.SaveAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string? _, string _, Stream __, string fileName, string? _) => $"stored-{fileName}");

        return (db, new ContentUnitOfWork(db, provider), storage);
    }

    private static async Task<Category> SeedCategoryAsync(AshianebarghDbContext db)
    {
        var category = new Category { Name = "ترمینال", Slug = "terminal", IsActive = true };
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> SeedProductAsync(AshianebarghDbContext db, Category category,
        string name, string code, string? shortDescription = null)
    {
        var product = new Product
        {
            Name = name,
            NameEn = name,
            Slug = code.ToLowerInvariant(),
            Code = code,
            Category = category,
            ShortDescription = shortDescription,
            IsActive = true
        };
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task PublicSearch_MatchesNameOrCode_AndFiltersOutOthers()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db);
        await SeedProductAsync(db, category, "KLF 101 D", "KLF-101-D");
        await SeedProductAsync(db, category, "AFF", "AFF-01");

        var service = new ProductService(uow, storage.Object, NullLogger<ProductService>.Instance);

        var byCode = await service.GetPublicPagedAsync(new PublicProductFilter { Search = "KLF-101-D" });
        Assert.Single(byCode.Items);
        Assert.Equal("KLF 101 D", byCode.Items[0].Name);

        var byName = await service.GetPublicPagedAsync(new PublicProductFilter { Search = "AFF" });
        Assert.Single(byName.Items);
        Assert.Equal("AFF", byName.Items[0].Name);

        var noMatch = await service.GetPublicPagedAsync(new PublicProductFilter { Search = "ZZZ-NOPE" });
        Assert.Empty(noMatch.Items);
    }

    [Fact]
    public async Task PublicSearch_RespectsCategoryFilter()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var terminal = await SeedCategoryAsync(db);
        var other = new Category { Name = "کانکتور", Slug = "connector", IsActive = true };
        db.Set<Category>().Add(other);
        await db.SaveChangesAsync();
        await SeedProductAsync(db, terminal, "AFF", "AFF-01");
        await SeedProductAsync(db, other, "AFF", "AFF-02");

        var service = new ProductService(uow, storage.Object, NullLogger<ProductService>.Instance);

        var result = await service.GetPublicPagedAsync(new PublicProductFilter
        {
            Search = "AFF",
            CategorySlug = "connector"
        });

        Assert.Single(result.Items);
        Assert.Equal("connector", result.Items[0].CategorySlug);
    }

    [Fact]
    public async Task AddFile_StoresRecord_WithSizeAndContentType()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db);
        var product = await SeedProductAsync(db, category, "AFF", "AFF-01");

        var service = new ProductService(uow, storage.Object, NullLogger<ProductService>.Instance);

        var payload = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
        await using var stream = new MemoryStream(payload);
        var dto = await service.AddFileAsync(product.Id, stream, "AFF.pdf", "application/pdf");

        Assert.Equal("AFF.pdf", dto.FileName);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(payload.Length, dto.SizeBytes);

        var stored = await db.ProductFiles.SingleAsync();
        Assert.Equal(product.Id, stored.ProductId);
        Assert.Equal(payload.Length, stored.SizeBytes);
    }

    [Fact]
    public async Task FileForDownload_IsHidden_WhenProductIsInactive()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db);
        var product = await SeedProductAsync(db, category, "AFF", "AFF-01");

        var service = new ProductService(uow, storage.Object, NullLogger<ProductService>.Instance);

        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf")))
        {
            var dto = await service.AddFileAsync(product.Id, stream, "AFF.pdf", "application/pdf");

            var visible = await service.GetFileForDownloadAsync(dto.Id);
            Assert.NotNull(visible);
            Assert.Equal("AFF.pdf", visible!.FileName);
        }

        product.IsActive = false;
        await db.SaveChangesAsync();

        var hidden = await service.GetFileForDownloadAsync(1);
        Assert.Null(hidden);
    }

    [Fact]
    public async Task DeleteProduct_RemovesAttachments()
    {
        var (db, uow, storage) = Build();
        await using var _ = db;
        var category = await SeedCategoryAsync(db);
        var product = await SeedProductAsync(db, category, "AFF", "AFF-01");

        var service = new ProductService(uow, storage.Object, NullLogger<ProductService>.Instance);
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf")))
        {
            await service.AddFileAsync(product.Id, stream, "AFF.pdf", "application/pdf");
        }

        await service.DeleteAsync(product.Id);

        Assert.Empty(await db.ProductFiles.ToListAsync());
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.Is<string>(t => t == "product-file"),
            It.IsAny<string>()), Times.Once);
    }
}

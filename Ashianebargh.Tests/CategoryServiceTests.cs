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
/// تصویر اختصاصی دسته: فقط دستهٔ فعال عمومی است، تعویض/حذف تصویر فایل قبلی را پاک می‌کند
/// و کارت دسته تصویر اختصاصی را به تصویر محصول ترجیح می‌دهد.
/// </summary>
public class CategoryServiceTests
{
    private static (AshianebarghDbContext db, ContentUnitOfWork uow) BuildDb()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"category-{Guid.NewGuid()}")
            .Options;

        var db = new AshianebarghDbContext(options, new IAshianebarghModule[] { new ContentModelModule() });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(Mock.Of<IOtpRepository>());
        services.AddSingleton(Mock.Of<IAuditLogRepository>());
        services.AddSingleton(Mock.Of<INotificationRepository>());
        services.AddSingleton(Mock.Of<IOutboxRepository>());
        var provider = services.BuildServiceProvider();

        return (db, new ContentUnitOfWork(db, provider));
    }

    private static CategoryService BuildService(ContentUnitOfWork uow, Mock<IFileStorage>? storage = null) =>
        new(uow, (storage ?? new Mock<IFileStorage>()).Object, NullLogger<CategoryService>.Instance);

    private static async Task<int> AddCategoryAsync(
        ContentUnitOfWork uow, string name, string? image = null, bool active = true)
    {
        var service = BuildService(uow);
        var id = await service.SaveAsync(new CategoryEditDto { Name = name, IsActive = active });
        if (image is not null)
            await service.SetImageAsync(id, image);
        return id;
    }

    [Fact]
    public async Task SaveAsync_IgnoresDtoImagestorageId()
    {
        var (_, uow) = BuildDb();
        var service = BuildService(uow);

        var id = await service.SaveAsync(new CategoryEditDto { Name = "روشنایی", ImageStorageId = "forged.png" });

        var entity = await uow.Content.Categories.FirstAsync(c => c.Id == id);
        Assert.Null(entity.ImageStorageId);
    }

    [Fact]
    public async Task SetImageAsync_ReplacesImage_AndDeletesOldFile()
    {
        var (_, uow) = BuildDb();
        var storage = new Mock<IFileStorage>();
        var service = BuildService(uow, storage);
        var id = await AddCategoryAsync(uow, "روشنایی", "old.png");

        await service.SetImageAsync(id, "new.png");

        var entity = await uow.Content.Categories.FirstAsync(c => c.Id == id);
        Assert.Equal("new.png", entity.ImageStorageId);
        storage.Verify(s => s.DeleteAsync(ContentStorage.Owner, ContentStorage.CategoryImageType, "old.png"), Times.Once);
        storage.Verify(s => s.DeleteAsync(ContentStorage.Owner, ContentStorage.CategoryImageType, "new.png"), Times.Never);
    }

    [Fact]
    public async Task SetImageAsync_Null_ClearsImageAndDeletesFile()
    {
        var (_, uow) = BuildDb();
        var storage = new Mock<IFileStorage>();
        var service = BuildService(uow, storage);
        var id = await AddCategoryAsync(uow, "روشنایی", "old.png");

        await service.SetImageAsync(id, null);

        var entity = await uow.Content.Categories.FirstAsync(c => c.Id == id);
        Assert.Null(entity.ImageStorageId);
        storage.Verify(s => s.DeleteAsync(ContentStorage.Owner, ContentStorage.CategoryImageType, "old.png"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesImageFile()
    {
        var (_, uow) = BuildDb();
        var storage = new Mock<IFileStorage>();
        var service = BuildService(uow, storage);
        var id = await AddCategoryAsync(uow, "روشنایی", "gone.png");

        await service.DeleteAsync(id);

        storage.Verify(s => s.DeleteAsync(ContentStorage.Owner, ContentStorage.CategoryImageType, "gone.png"), Times.Once);
    }

    [Fact]
    public async Task IsImagePublicAsync_InactiveOrUnknown_False()
    {
        var (_, uow) = BuildDb();
        var service = BuildService(uow);
        await AddCategoryAsync(uow, "فعال", "active.png");
        await AddCategoryAsync(uow, "غیرفعال", "hidden.png", active: false);

        Assert.True(await service.IsImagePublicAsync("active.png"));
        Assert.False(await service.IsImagePublicAsync("hidden.png"));
        Assert.False(await service.IsImagePublicAsync("unknown.png"));
    }

    [Fact]
    public async Task GetPublicImageStorageIdsAsync_OnlyActiveWithImage()
    {
        var (_, uow) = BuildDb();
        var service = BuildService(uow);
        await AddCategoryAsync(uow, "یک", "a.png");
        await AddCategoryAsync(uow, "دو", "b.png", active: false);
        await AddCategoryAsync(uow, "سه");

        var ids = await service.GetPublicImageStorageIdsAsync();

        Assert.Equal(new[] { "a.png" }, ids);
    }

    [Fact]
    public async Task GetActiveAsync_PrefersOwnImage_ThenFallsBackToProductImage()
    {
        var (_, uow) = BuildDb();
        var service = BuildService(uow);
        var withOwn = await AddCategoryAsync(uow, "با تصویر", "own.png");
        var withoutOwn = await AddCategoryAsync(uow, "بدون تصویر");

        uow.Content.Add(new Product
        {
            Name = "محصول یک",
            Slug = "product-1",
            CategoryId = withOwn,
            IsActive = true,
            MainImageStorageId = "p1.png",
            SortOrder = 1
        });
        uow.Content.Add(new Product
        {
            Name = "محصول دو",
            Slug = "product-2",
            CategoryId = withoutOwn,
            IsActive = true,
            MainImageStorageId = "p2.png",
            SortOrder = 1
        });
        await uow.CompleteAsync();

        var list = await service.GetActiveAsync();

        var own = list.First(c => c.Id == withOwn);
        Assert.Equal("own.png", own.ImageStorageId);
        Assert.Equal("p1.png", own.FallbackImageStorageId);
        var fallback = list.First(c => c.Id == withoutOwn);
        Assert.Null(fallback.ImageStorageId);
        Assert.Equal("p2.png", fallback.FallbackImageStorageId);
    }
}
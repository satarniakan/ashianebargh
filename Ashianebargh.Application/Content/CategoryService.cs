using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Application.Content;

public interface ICategoryService
{
    Task<List<CategoryListItemDto>> GetAllAsync();
    Task<List<CategoryListItemDto>> GetActiveAsync();
    Task<CategoryEditDto?> GetAsync(int id);
    Task<int> SaveAsync(CategoryEditDto dto);
    Task DeleteAsync(int id);

    /// <summary>تعویض/حذف تصویر دسته؛ فایل قبلی پاک می‌شود تا فضای ذخیره‌سازی باد نکند.</summary>
    Task SetImageAsync(int id, string? storageId);

    /// <summary>فقط تصویر دستهٔ فعال سرو شود؛ دستهٔ غیرفعال رسانه‌اش لو نرود.</summary>
    Task<bool> IsImagePublicAsync(string storageId);

    /// <summary>شناسه تصاویر عمومی برای نگه‌داشتن نسخه‌های کش‌شده در خانه‌تکانی.</summary>
    Task<List<string>> GetPublicImageStorageIdsAsync();

    /// <summary>مسیر و تاریخ تغییر دسته‌های فعال برای sitemap.xml.</summary>
    Task<List<SitemapEntryDto>> GetPublicSitemapAsync();
}

public sealed class CategoryService(
    IContentUnitOfWork uow,
    IFileStorage storage,
    ILogger<CategoryService> logger) : ICategoryService
{
    public Task<List<CategoryListItemDto>> GetAllAsync() =>
        uow.Content.Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.Products.Count,
                c.ImageStorageId,
                c.Products
                    .Where(p => p.IsActive && p.MainImageStorageId != null)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .Select(p => p.MainImageStorageId)
                    .FirstOrDefault()))
            .ToListAsync();

    public Task<List<CategoryListItemDto>> GetActiveAsync() =>
        uow.Content.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.Products.Count,
                c.ImageStorageId,
                c.Products
                    .Where(p => p.IsActive && p.MainImageStorageId != null)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .Select(p => p.MainImageStorageId)
                    .FirstOrDefault()))
            .ToListAsync();

    public Task<List<SitemapEntryDto>> GetPublicSitemapAsync() =>
        uow.Content.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new SitemapEntryDto("/category/" + c.Slug, c.UpdatedAt ?? c.CreatedAt))
            .ToListAsync();

    public Task<CategoryEditDto?> GetAsync(int id) =>        uow.Content.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryEditDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageStorageId = c.ImageStorageId,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive
            })
            .FirstOrDefaultAsync();

    public async Task<int> SaveAsync(CategoryEditDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new BusinessRuleException("نام دسته را وارد کنید.");

        var slug = SlugHelper.Normalize(dto.Slug);
        if (slug.Length == 0) slug = SlugHelper.Normalize(name);
        if (slug.Length == 0)
            throw new BusinessRuleException("نامک معتبر نیست؛ حداقل یک حرف یا رقم لازم است.");

        if (await uow.Content.Categories.AnyAsync(c => c.Slug == slug && c.Id != dto.Id))
            throw new BusinessRuleException("این نامک قبلاً برای دستهٔ دیگری استفاده شده است.");

        Category entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.Categories.FirstOrDefaultAsync(c => c.Id == dto.Id)
                ?? throw new NotFoundException("دسته", dto.Id);
        }
        else
        {
            entity = new Category();
            uow.Content.Add(entity);
        }

        entity.Name = name;
        entity.Slug = slug;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        // ImageStorageId عمداً از dto کپی نمی‌شود؛ تعویض تصویر فقط از SetImageAsync می‌گذرد.

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task SetImageAsync(int id, string? storageId)
    {
        var entity = await uow.Content.Categories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("دسته", id);

        var oldStorageId = entity.ImageStorageId;
        entity.ImageStorageId = string.IsNullOrWhiteSpace(storageId) ? null : storageId;
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(oldStorageId) && oldStorageId != entity.ImageStorageId)
            await TryDeleteFileAsync(oldStorageId);
    }

    public Task<bool> IsImagePublicAsync(string storageId) =>
        uow.Content.Categories.AsNoTracking()
            .AnyAsync(c => c.IsActive && c.ImageStorageId == storageId);

    public Task<List<string>> GetPublicImageStorageIdsAsync() =>
        uow.Content.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.ImageStorageId != null && c.ImageStorageId != "")
            .Select(c => c.ImageStorageId!)
            .ToListAsync();

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Categories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("دسته", id);

        if (await uow.Content.Products.AnyAsync(p => p.CategoryId == id))
            throw new BusinessRuleException("این دسته محصول دارد؛ ابتدا محصولات را جابه‌جا یا حذف کنید.");

        var storageId = entity.ImageStorageId;
        uow.Content.Remove(entity);
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(storageId))
            await TryDeleteFileAsync(storageId);
    }

    private async Task TryDeleteFileAsync(string storageId)
    {
        try
        {
            await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.CategoryImageType, storageId);
        }
        catch (Exception ex)
        {
            // پاک‌نشدن فایل نباید حذف/ویرایش دسته را شکست دهد؛ فقط برای بررسی لاگ می‌شود.
            logger.LogWarning(ex, "حذف فایل تصویر دسته ناموفق بود: {StorageId}", storageId);
        }
    }
}

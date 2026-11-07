using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Queries;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Content;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductFilter filter);
    Task<ProductEditDto?> GetForEditAsync(int id);
    Task<int> SaveAsync(ProductEditDto dto);
    Task SetImageAsync(int productId, string? storageId);
    Task DeleteAsync(int id);

    Task<PagedResult<ProductCardDto>> GetPublicPagedAsync(PublicProductFilter filter);
    Task<List<ProductCardDto>> GetFeaturedAsync(int count);
    Task<ProductDetailsDto?> GetPublicBySlugAsync(string slug);
}

public sealed class ProductService(
    IContentUnitOfWork uow,
    IFileStorage storage,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductFilter filter)
    {
        var query = uow.Content.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = PersianSearch.Normalize(filter.Search);
            query = query.Where(PersianSearch.ContainsNormalized<Product>(term, p => p.Name, p => p.Code));
        }

        if (filter.CategoryId is > 0)
            query = query.Where(p => p.CategoryId == filter.CategoryId);

        if (filter.TagId is > 0)
            query = query.Where(p => p.Tags.Any(t => t.Id == filter.TagId));

        var projected = query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductListItemDto(
                p.Id, p.Name, p.Slug, p.Code, p.Category!.Name,
                p.Price, p.IsActive, p.IsFeatured, p.SortOrder, p.MainImageStorageId));

        return await projected.ToPagedAsync(filter.Page, filter.PageSize);
    }

    public Task<ProductEditDto?> GetForEditAsync(int id) =>
        uow.Content.Products
            .Where(p => p.Id == id)
            .Select(p => new ProductEditDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Code = p.Code,
                CategoryId = p.CategoryId,
                ShortDescription = p.ShortDescription,
                Description = p.Description,
                Price = p.Price,
                TagIds = p.Tags.Select(t => t.Id).ToList(),
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                MainImageStorageId = p.MainImageStorageId
            })
            .FirstOrDefaultAsync();

    public async Task<int> SaveAsync(ProductEditDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new BusinessRuleException("نام محصول را وارد کنید.");

        var slug = SlugHelper.Normalize(dto.Slug);
        if (slug.Length == 0) slug = SlugHelper.Normalize(name);
        if (slug.Length == 0)
            throw new BusinessRuleException("نامک معتبر نیست؛ حداقل یک حرف یا رقم لازم است.");

        if (!await uow.Content.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            throw new BusinessRuleException("دستهٔ انتخاب‌شده یافت نشد.");

        if (await uow.Content.Products.AnyAsync(p => p.Slug == slug && p.Id != dto.Id))
            throw new BusinessRuleException("این نامک قبلاً برای محصول دیگری استفاده شده است.");

        var tagIds = dto.TagIds.Distinct().ToList();
        var tags = new List<Tag>();
        if (tagIds.Count > 0)
            tags = await uow.Content.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync();
        if (tags.Count != tagIds.Count)
            throw new BusinessRuleException("برچسب انتخاب‌شده یافت نشد؛ فهرست را تازه کنید.");

        Product entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.Products
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == dto.Id)
                ?? throw new NotFoundException("محصول", dto.Id);
        }
        else
        {
            entity = new Product();
            uow.Content.Add(entity);
        }

        entity.Name = name;
        entity.Slug = slug;
        entity.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        entity.CategoryId = dto.CategoryId;
        entity.ShortDescription = string.IsNullOrWhiteSpace(dto.ShortDescription) ? null : dto.ShortDescription.Trim();
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.IsActive = dto.IsActive;
        entity.IsFeatured = dto.IsFeatured;
        entity.SortOrder = dto.SortOrder;

        entity.Tags.Clear();
        foreach (var tag in tags)
            entity.Tags.Add(tag);

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task SetImageAsync(int productId, string? storageId)
    {
        var entity = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("محصول", productId);

        var oldStorageId = entity.MainImageStorageId;
        entity.MainImageStorageId = string.IsNullOrWhiteSpace(storageId) ? null : storageId;
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(oldStorageId) && oldStorageId != entity.MainImageStorageId)
            await TryDeleteFileAsync(oldStorageId);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("محصول", id);

        var storageId = entity.MainImageStorageId;
        uow.Content.Remove(entity);
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(storageId))
            await TryDeleteFileAsync(storageId);
    }

    public async Task<PagedResult<ProductCardDto>> GetPublicPagedAsync(PublicProductFilter filter)
    {
        var query = uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category!.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.CategorySlug))
            query = query.Where(p => p.Category!.Slug == filter.CategorySlug);

        if (!string.IsNullOrWhiteSpace(filter.TagSlug))
            query = query.Where(p => p.Tags.Any(t => t.Slug == filter.TagSlug));

        var projected = query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.Slug, p.ShortDescription, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug));

        return await projected.ToPagedAsync(filter.Page, filter.PageSize);
    }

    public Task<List<ProductCardDto>> GetFeaturedAsync(int count) =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.IsFeatured && p.Category!.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(Math.Clamp(count, 1, 24))
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.Slug, p.ShortDescription, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug))
            .ToListAsync();

    public Task<ProductDetailsDto?> GetPublicBySlugAsync(string slug) =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive && p.Category!.IsActive)
            .Select(p => new ProductDetailsDto(
                p.Id, p.Name, p.Slug, p.Code, p.Category!.Name, p.Category.Slug,
                p.ShortDescription, p.Description, p.Price, p.MainImageStorageId,
                p.Tags.OrderBy(t => t.Name)
                    .Select(t => new TagDto(t.Id, t.Name, t.Slug)).ToList()))
            .FirstOrDefaultAsync();

    private async Task TryDeleteFileAsync(string storageId)
    {
        try
        {
            await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.ProductImageType, storageId);
        }
        catch (Exception ex)
        {
            // پاک‌نشدن فایل نباید حذف/ویرایش محصول را شکست دهد؛ فقط برای بررسی لاگ می‌شود.
            logger.LogWarning(ex, "حذف فایل تصویر محصول ناموفق بود: {StorageId}", storageId);
        }
    }
}

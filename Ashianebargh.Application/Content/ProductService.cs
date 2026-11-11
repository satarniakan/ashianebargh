using Microsoft.EntityFrameworkCore;
using System.IO;
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

    Task<ProductFileDto> AddFileAsync(int productId, Stream stream, string fileName, string contentType);
    Task<ProductFileDto> AttachFileAsync(int productId, string storageId, string fileName, string contentType, long sizeBytes);
    Task DeleteFileAsync(int fileId);
    Task<ProductFileDownloadDto?> GetFileForDownloadAsync(int fileId);

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
            query = query.Where(PersianSearch.ContainsNormalized<Product>(term, p => p.Name, p => p.NameEn, p => p.Code));
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
                MainImageStorageId = p.MainImageStorageId,
                Files = p.Files.Select(f => new ProductFileDto(f.Id, f.FileName, f.StorageId, f.ContentType, f.SizeBytes)).ToList()
            })
            .FirstOrDefaultAsync();

    public async Task<int> SaveAsync(ProductEditDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new BusinessRuleException("ظ†ط§ظ… ظ…ط­طµظˆظ„ ط±ط§ ظˆط§ط±ط¯ ع©ظ†غŒط¯.");

        var slug = SlugHelper.Normalize(dto.Slug);
        if (slug.Length == 0) slug = SlugHelper.Normalize(name);
        if (slug.Length == 0)
            throw new BusinessRuleException("ظ†ط§ظ…ع© ظ…ط¹طھط¨ط± ظ†غŒط³طھط› ط­ط¯ط§ظ‚ظ„ غŒع© ط­ط±ظپ غŒط§ ط±ظ‚ظ… ظ„ط§ط²ظ… ط§ط³طھ.");

        if (!await uow.Content.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            throw new BusinessRuleException("ط¯ط³طھظ‡ظ” ط§ظ†طھط®ط§ط¨â€Œط´ط¯ظ‡ غŒط§ظپطھ ظ†ط´ط¯.");

        if (await uow.Content.Products.AnyAsync(p => p.Slug == slug && p.Id != dto.Id))
            throw new BusinessRuleException("ط§غŒظ† ظ†ط§ظ…ع© ظ‚ط¨ظ„ط§ظ‹ ط¨ط±ط§غŒ ظ…ط­طµظˆظ„ ط¯غŒع¯ط±غŒ ط§ط³طھظپط§ط¯ظ‡ ط´ط¯ظ‡ ط§ط³طھ.");

        var tagIds = dto.TagIds.Distinct().ToList();
        var tags = new List<Tag>();
        if (tagIds.Count > 0)
            tags = await uow.Content.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync();
        if (tags.Count != tagIds.Count)
            throw new BusinessRuleException("ط¨ط±ع†ط³ط¨ ط§ظ†طھط®ط§ط¨â€Œط´ط¯ظ‡ غŒط§ظپطھ ظ†ط´ط¯ط› ظپظ‡ط±ط³طھ ط±ط§ طھط§ط²ظ‡ ع©ظ†غŒط¯.");

        Product entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.Products
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == dto.Id)
                ?? throw new NotFoundException("ظ…ط­طµظˆظ„", dto.Id);
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
            ?? throw new NotFoundException("ظ…ط­طµظˆظ„", productId);

        var oldStorageId = entity.MainImageStorageId;
        entity.MainImageStorageId = string.IsNullOrWhiteSpace(storageId) ? null : storageId;
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(oldStorageId) && oldStorageId != entity.MainImageStorageId)
            await TryDeleteFileAsync(oldStorageId);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("ظ…ط­طµظˆظ„", id);

        var storageId = entity.MainImageStorageId;

        // پیوست‌ها با حذف نرمِ محصول از دید کوئری‌ها پنهان می‌شوند، ولی فایل‌های
        // فیزیکی باید صریح پاک شوند تا فضای ذخیره‌سازی باد نماند.
        var files = await uow.Content.ProductFiles.Where(f => f.ProductId == id).ToListAsync();
        foreach (var file in files)
            uow.Content.Remove(file);

        uow.Content.Remove(entity);
        await uow.CompleteAsync();

        foreach (var file in files)
            await TryDeleteFileAsync(file.StorageId, ContentStorage.ProductFileType);

        if (!string.IsNullOrEmpty(storageId))
            await TryDeleteFileAsync(storageId);
    }

    public async Task<ProductFileDto> AddFileAsync(int productId, Stream stream, string fileName, string contentType)
    {
        var product = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("محصول", productId);

        // برای نمایش حجم فایل در رابط کاربری، اندازه باید قبل از مصرف جریان معلوم باشد.
        Stream source = stream;
        MemoryStream? buffer = null;
        long size;
        if (stream.CanSeek)
        {
            size = stream.Length - stream.Position;
        }
        else
        {
            buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            source = buffer;
            size = buffer.Length;
        }

        try
        {
            var storageId = await storage.SaveAsync(
                ContentStorage.Owner, ContentStorage.ProductFileType,
                source, fileName, contentType);

            var file = new ProductFile
            {
                ProductId = product.Id,
                FileName = fileName,
                StorageId = storageId,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                SizeBytes = size
            };
            uow.Content.Add(file);
            await uow.CompleteAsync();

            return new ProductFileDto(file.Id, file.FileName, file.StorageId, file.ContentType, file.SizeBytes);
        }
        finally
        {
            if (buffer is not null)
                await buffer.DisposeAsync();
        }
    }

    /// <summary>
    /// اتصال فایلی که پیش‌تر در فضای ذخیره‌سازی ذخیره شده به یک محصول.
    /// برای حالتی که فایل پیش از ساخت محصول آپلود می‌شود.
    /// </summary>
    public async Task<ProductFileDto> AttachFileAsync(int productId, string storageId, string fileName,
        string contentType, long sizeBytes)
    {
        var product = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("محصول", productId);

        var file = new ProductFile
        {
            ProductId = product.Id,
            FileName = fileName,
            StorageId = storageId,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = sizeBytes
        };
        uow.Content.Add(file);
        await uow.CompleteAsync();

        return new ProductFileDto(file.Id, file.FileName, file.StorageId, file.ContentType, file.SizeBytes);
    }

    public async Task DeleteFileAsync(int fileId)
    {
        var file = await uow.Content.ProductFiles.FirstOrDefaultAsync(f => f.Id == fileId)
            ?? throw new NotFoundException("فایل", fileId);

        await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.ProductFileType, file.StorageId);
        uow.Content.Remove(file);
        await uow.CompleteAsync();
    }

    public Task<ProductFileDownloadDto?> GetFileForDownloadAsync(int fileId) =>
        uow.Content.ProductFiles.AsNoTracking()
            .Where(f => f.Id == fileId && f.Product!.IsActive && f.Product.Category!.IsActive)
            .Select(f => new ProductFileDownloadDto(f.Id, f.FileName, f.StorageId, f.ContentType, f.SizeBytes))
            .FirstOrDefaultAsync();

    public async Task<PagedResult<ProductCardDto>> GetPublicPagedAsync(PublicProductFilter filter)
    {
        var query = uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category!.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.CategorySlug))
            query = query.Where(p => p.Category!.Slug == filter.CategorySlug);

        if (!string.IsNullOrWhiteSpace(filter.TagSlug))
            query = query.Where(p => p.Tags.Any(t => t.Slug == filter.TagSlug));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = PersianSearch.Normalize(filter.Search);
            query = query.Where(PersianSearch.ContainsNormalized<Product>(term,
                p => p.Name, p => p.NameEn, p => p.Code, p => p.ShortDescription, p => p.ShortDescriptionEn));
        }

        var projected = query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.NameEn, p.Slug, p.Code, p.ShortDescription, p.ShortDescriptionEn, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug));

        return await projected.ToPagedAsync(filter.Page, filter.PageSize);
    }

    public Task<List<ProductCardDto>> GetFeaturedAsync(int count) =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.IsFeatured && p.Category!.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(Math.Clamp(count, 1, 24))
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.NameEn, p.Slug, p.Code, p.ShortDescription, p.ShortDescriptionEn, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug))
            .ToListAsync();

    public Task<ProductDetailsDto?> GetPublicBySlugAsync(string slug) =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive && p.Category!.IsActive)
            .Select(p => new ProductDetailsDto(
                p.Id, p.Name, p.Slug, p.Code, p.Category!.Name, p.Category.Slug,
                p.ShortDescription, p.Description, p.Price, p.MainImageStorageId,
                p.Tags.OrderBy(t => t.Name)
                    .Select(t => new TagDto(t.Id, t.Name, t.Slug)).ToList(),
                p.Files.Select(f => new ProductFileDto(f.Id, f.FileName, f.StorageId, f.ContentType, f.SizeBytes)).ToList()))
            .FirstOrDefaultAsync();

    private async Task TryDeleteFileAsync(string storageId, string fileType = ContentStorage.ProductImageType)
    {
        try
        {
            await storage.DeleteAsync(ContentStorage.Owner, fileType, storageId);
        }
        catch (Exception ex)
        {
            // ظ¾ط§ع©â€Œظ†ط´ط¯ظ† ظپط§غŒظ„ ظ†ط¨ط§غŒط¯ ط­ط°ظپ/ظˆغŒط±ط§غŒط´ ظ…ط­طµظˆظ„ ط±ط§ ط´ع©ط³طھ ط¯ظ‡ط¯ط› ظپظ‚ط· ط¨ط±ط§غŒ ط¨ط±ط±ط³غŒ ظ„ط§ع¯ ظ…غŒâ€Œط´ظˆط¯.
            logger.LogWarning(ex, "ط­ط°ظپ ظپط§غŒظ„ طھطµظˆغŒط± ظ…ط­طµظˆظ„ ظ†ط§ظ…ظˆظپظ‚ ط¨ظˆط¯: {StorageId}", storageId);
        }
    }
}

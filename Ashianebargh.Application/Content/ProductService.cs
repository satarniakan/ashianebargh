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

    /// <summary>افزودن تصویر گالری به محصول موجود.</summary>
    Task<ProductImageDto> AddImageAsync(int productId, Stream stream, string fileName);

    /// <summary>حذف تصویر گالری (ردیف و فایل فیزیکی).</summary>
    Task DeleteImageAsync(int imageId);

    /// <summary>آیا این تصویر به محصولی فعال (در دستهٔ فعال) تعلق دارد؟ برای سرو عمومی رسانه.</summary>
    Task<bool> IsImagePublicAsync(string storageId);

    /// <summary>آیا این فایل پیوستِ محصولی فعال (در دستهٔ فعال) است؟ برای سرو عمومی رسانه.</summary>
    Task<bool> IsFilePublicAsync(string storageId);

    Task<PagedResult<ProductCardDto>> GetPublicPagedAsync(PublicProductFilter filter);
    Task<List<ProductCardDto>> GetFeaturedAsync(int count);

    /// <summary>محصولات هم‌دسته برای بخش «محصولات مرتبط» صفحهٔ جزئیات.</summary>
    Task<List<ProductCardDto>> GetRelatedAsync(int productId, string categorySlug, int count);

    /// <summary>مسیر و تاریخ تغییر محصولات فعال برای sitemap.xml.</summary>
    Task<List<SitemapEntryDto>> GetPublicSitemapAsync();

    /// <summary>
    /// شناسهٔ ذخیره‌سازی همهٔ تصاویر (اصلی + گالری) که هنوز عمومی‌اند؛ مبنای
    /// خانه‌تکانی کش نسخه‌های کوچک‌شده تا تصاویر محصولات حذف/غیرفعال‌شده نمانند.
    /// </summary>
    Task<List<string>> GetPublicImageStorageIdsAsync();

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
                NameEn = p.NameEn,
                Slug = p.Slug,
                Code = p.Code,
                CategoryId = p.CategoryId,
                ShortDescription = p.ShortDescription,
                ShortDescriptionEn = p.ShortDescriptionEn,
                Description = p.Description,
                DescriptionEn = p.DescriptionEn,
                Price = p.Price,
                TagIds = p.Tags.Select(t => t.Id).ToList(),
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                MainImageStorageId = p.MainImageStorageId,
                Files = p.Files.Select(f => new ProductFileDto(f.Id, f.FileName, f.StorageId, f.ContentType, f.SizeBytes)).ToList(),
                Specs = p.Specs.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .Select(s => new ProductSpecDto(s.Title, s.Value)).ToList(),
                Images = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                    .Select(i => new ProductImageDto(i.Id, i.StorageId)).ToList()
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
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == dto.Id)
                ?? throw new NotFoundException("محصول", dto.Id);
        }
        else
        {
            entity = new Product();
            uow.Content.Add(entity);
        }

        entity.Name = name;
        entity.NameEn = string.IsNullOrWhiteSpace(dto.NameEn) ? string.Empty : dto.NameEn.Trim();
        entity.Slug = slug;
        entity.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        entity.CategoryId = dto.CategoryId;
        entity.ShortDescription = string.IsNullOrWhiteSpace(dto.ShortDescription) ? null : dto.ShortDescription.Trim();
        entity.ShortDescriptionEn = string.IsNullOrWhiteSpace(dto.ShortDescriptionEn) ? null : dto.ShortDescriptionEn.Trim();
        entity.Description = dto.Description;
        entity.DescriptionEn = dto.DescriptionEn;
        entity.Price = dto.Price;
        entity.IsActive = dto.IsActive;
        entity.IsFeatured = dto.IsFeatured;
        entity.SortOrder = dto.SortOrder;

        entity.Tags.Clear();
        foreach (var tag in tags)
            entity.Tags.Add(tag);

        SyncSpecs(entity, dto.Specs);

        await uow.CompleteAsync();
        return entity.Id;
    }

    /// <summary>
    /// مشخصات فنی را با فهرست ارسالی هم‌تراز می‌کند: ردیف‌های خالی نادیده گرفته
    /// می‌شوند، ترتیب ورودی حفظ می‌شود و ردیف‌های حذف‌شده پاک می‌شوند.
    /// </summary>
    private static void SyncSpecs(Product product, List<ProductSpecDto>? specs)
    {
        var incoming = (specs ?? new List<ProductSpecDto>())
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) && !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => (Title: s.Title.Trim(), Value: s.Value.Trim()))
            .ToList();

        product.Specs.Clear();

        for (var i = 0; i < incoming.Count; i++)
        {
            product.Specs.Add(new ProductSpec
            {
                Title = incoming[i].Title,
                Value = incoming[i].Value,
                SortOrder = i
            });
        }
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

        // پیوست‌ها با حذف نرمِ محصول از دید کوئری‌ها پنهان می‌شوند، ولی فایل‌های
        // فیزیکی باید صریح پاک شوند تا فضای ذخیره‌سازی باد نماند.
        var files = await uow.Content.ProductFiles.Where(f => f.ProductId == id).ToListAsync();
        foreach (var file in files)
            uow.Content.Remove(file);

        // گالری هم مثل پیوست‌ها: ردیف حذف و فایل فیزیکی پاک شود.
        var images = await uow.Content.ProductImages.Where(i => i.ProductId == id).ToListAsync();
        foreach (var image in images)
            uow.Content.Remove(image);

        uow.Content.Remove(entity);
        await uow.CompleteAsync();

        foreach (var file in files)
            await TryDeleteFileAsync(file.StorageId, ContentStorage.ProductFileType);

        foreach (var image in images)
            await TryDeleteFileAsync(image.StorageId);

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

        // فایل باید واقعاً در فضای ذخیره‌سازی وجود داشته باشد؛ وگرنه storageId جعلی یا
        // متعلق به فضای دیگر به محصول وصل می‌شود و از مسیر عمومی رسانه قابل دانلود می‌گردد.
        if (string.IsNullOrWhiteSpace(storageId) ||
            !await storage.ExistsAsync(ContentStorage.Owner, ContentStorage.ProductFileType, storageId))
            throw new BusinessRuleException("فایل آپلودشده در فضای ذخیره‌سازی یافت نشد.");

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

    // سرو عمومی رسانه باید فقط فایلِ محصولات فعال را نشان دهد؛ وگرنه با غیرفعال‌کردن
    // محصول، تصویر/فایلش همچنان از مسیر storageId قابل دانلود می‌ماند (نشت محتوای پنهان).
    // تصاویر گالری هم مثل تصویر اصلی باید همین گارد را داشته باشند.
    public async Task<bool> IsImagePublicAsync(string storageId) =>
        await uow.Content.Products.AsNoTracking()
            .AnyAsync(p => p.MainImageStorageId == storageId && p.IsActive && p.Category!.IsActive)
        || await uow.Content.ProductImages.AsNoTracking()
            .AnyAsync(i => i.StorageId == storageId && i.Product!.IsActive && i.Product.Category!.IsActive);

    public Task<bool> IsFilePublicAsync(string storageId) =>
        uow.Content.ProductFiles.AsNoTracking()
            .AnyAsync(f => f.StorageId == storageId && f.Product!.IsActive && f.Product.Category!.IsActive);

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

        var projected = OrderPublic(query, filter.Sort)
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.NameEn, p.Slug, p.Code, p.ShortDescription, p.ShortDescriptionEn, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug));

        return await projected.ToPagedAsync(filter.Page, filter.PageSize);
    }

    /// <summary>
    /// ترتیب فهرست عمومی. «price-asc/desc» ابتدا محصولات بدون قیمت را جدا می‌کند
    /// (قیمت نامعلوم نباید ارزان‌ترین به نظر برسد) و در نهایت نام برای ترتیب پایدار.
    /// </summary>
    private static IQueryable<Product> OrderPublic(IQueryable<Product> query, string? sort) =>
        sort switch
        {
            "newest" => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            "price-asc" => query.OrderBy(p => p.Price == null).ThenBy(p => p.Price).ThenBy(p => p.Name),
            "price-desc" => query.OrderBy(p => p.Price == null).ThenByDescending(p => p.Price).ThenBy(p => p.Name),
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
        };

    public Task<List<ProductCardDto>> GetRelatedAsync(int productId, string categorySlug, int count) =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category!.IsActive && p.Id != productId && p.Category!.Slug == categorySlug)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(Math.Clamp(count, 1, 12))
            .Select(p => new ProductCardDto(
                p.Id, p.Name, p.NameEn, p.Slug, p.Code, p.ShortDescription, p.ShortDescriptionEn, p.Price,
                p.MainImageStorageId, p.Category!.Name, p.Category.Slug))
            .ToListAsync();

    public Task<List<SitemapEntryDto>> GetPublicSitemapAsync() =>
        uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category!.IsActive && p.Slug != string.Empty)
            .OrderBy(p => p.Id)
            .Select(p => new SitemapEntryDto("/product/" + p.Slug, p.UpdatedAt ?? p.CreatedAt))
            .ToListAsync();

    public async Task<List<string>> GetPublicImageStorageIdsAsync()
    {
        var mainImages = await uow.Content.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category!.IsActive && p.MainImageStorageId != null)
            .Select(p => p.MainImageStorageId!)
            .ToListAsync();

        var galleryImages = await uow.Content.ProductImages.AsNoTracking()
            .Where(i => i.Product!.IsActive && i.Product.Category!.IsActive)
            .Select(i => i.StorageId)
            .ToListAsync();

        return mainImages.Concat(galleryImages)
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
                p.Id, p.Name, p.NameEn, p.Slug, p.Code, p.Category!.Name, p.Category.Slug,
                p.ShortDescription, p.ShortDescriptionEn, p.Description, p.DescriptionEn,
                p.Price, p.MainImageStorageId,
                p.Tags.OrderBy(t => t.Name)
                    .Select(t => new TagDto(t.Id, t.Name, t.Slug)).ToList(),
                p.Files.Select(f => new ProductFileDto(f.Id, f.FileName, f.StorageId, f.ContentType, f.SizeBytes)).ToList(),
                p.Specs.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .Select(s => new ProductSpecDto(s.Title, s.Value)).ToList(),
                p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                    .Select(i => new ProductImageDto(i.Id, i.StorageId)).ToList()))
            .FirstOrDefaultAsync();

    public async Task<ProductImageDto> AddImageAsync(int productId, Stream stream, string fileName)
    {
        var product = await uow.Content.Products.FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("محصول", productId);

        var storageId = await storage.SaveAsync(
            ContentStorage.Owner, ContentStorage.ProductImageType, stream, fileName, "image/*");

        var nextOrder = await uow.Content.ProductImages
            .Where(i => i.ProductId == productId)
            .Select(i => (int?)i.SortOrder).MaxAsync() ?? -1;

        var image = new ProductImage
        {
            ProductId = product.Id,
            StorageId = storageId,
            SortOrder = nextOrder + 1
        };
        uow.Content.Add(image);
        await uow.CompleteAsync();

        return new ProductImageDto(image.Id, image.StorageId);
    }

    public async Task DeleteImageAsync(int imageId)
    {
        var image = await uow.Content.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId)
            ?? throw new NotFoundException("تصویر", imageId);

        uow.Content.Remove(image);
        await uow.CompleteAsync();

        await TryDeleteFileAsync(image.StorageId);
    }

    private async Task TryDeleteFileAsync(string storageId, string fileType = ContentStorage.ProductImageType)
    {
        try
        {
            await storage.DeleteAsync(ContentStorage.Owner, fileType, storageId);
        }
        catch (Exception ex)
        {
            // پاک‌نشدن فایل نباید حذف/ویرایش محصول را شکست دهد؛ فقط برای بررسی لاگ می‌شود.
            logger.LogWarning(ex, "حذف فایل تصویر محصول ناموفق بود: {StorageId}", storageId);
        }
    }
}

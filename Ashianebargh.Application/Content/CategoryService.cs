using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;

namespace Ashianebargh.Application.Content;

public interface ICategoryService
{
    Task<List<CategoryListItemDto>> GetAllAsync();
    Task<List<CategoryListItemDto>> GetActiveAsync();
    Task<CategoryEditDto?> GetAsync(int id);
    Task<int> SaveAsync(CategoryEditDto dto);
    Task DeleteAsync(int id);
}

public sealed class CategoryService(IContentUnitOfWork uow) : ICategoryService
{
    public Task<List<CategoryListItemDto>> GetAllAsync() =>
        uow.Content.Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.Products.Count))
            .ToListAsync();

    public Task<List<CategoryListItemDto>> GetActiveAsync() =>
        uow.Content.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.Products.Count))
            .ToListAsync();

    public Task<CategoryEditDto?> GetAsync(int id) =>
        uow.Content.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryEditDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
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

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Categories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("دسته", id);

        if (await uow.Content.Products.AnyAsync(p => p.CategoryId == id))
            throw new BusinessRuleException("این دسته محصول دارد؛ ابتدا محصولات را جابه‌جا یا حذف کنید.");

        uow.Content.Remove(entity);
        await uow.CompleteAsync();
    }
}

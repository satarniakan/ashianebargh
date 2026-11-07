using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;

namespace Ashianebargh.Application.Content;

public interface ITagService
{
    Task<List<TagListItemDto>> GetAllAsync();
    Task<TagEditDto?> GetAsync(int id);
    Task<int> SaveAsync(TagEditDto dto);
    Task DeleteAsync(int id);
}

public sealed class TagService(IContentUnitOfWork uow) : ITagService
{
    public Task<List<TagListItemDto>> GetAllAsync() =>
        uow.Content.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagListItemDto(t.Id, t.Name, t.Slug, t.Products.Count))
            .ToListAsync();

    public Task<TagEditDto?> GetAsync(int id) =>
        uow.Content.Tags
            .Where(t => t.Id == id)
            .Select(t => new TagEditDto { Id = t.Id, Name = t.Name, Slug = t.Slug })
            .FirstOrDefaultAsync();

    public async Task<int> SaveAsync(TagEditDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new BusinessRuleException("نام برچسب را وارد کنید.");

        var slug = SlugHelper.Normalize(dto.Slug);
        if (slug.Length == 0) slug = SlugHelper.Normalize(name);
        if (slug.Length == 0)
            throw new BusinessRuleException("نامک معتبر نیست؛ حداقل یک حرف یا رقم لازم است.");

        if (await uow.Content.Tags.AnyAsync(t => t.Slug == slug && t.Id != dto.Id))
            throw new BusinessRuleException("این نامک قبلاً برای برچسب دیگری استفاده شده است.");

        Tag entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.Tags.FirstOrDefaultAsync(t => t.Id == dto.Id)
                ?? throw new NotFoundException("برچسب", dto.Id);
        }
        else
        {
            entity = new Tag();
            uow.Content.Add(entity);
        }

        entity.Name = name;
        entity.Slug = slug;

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Tags.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException("برچسب", id);

        uow.Content.Remove(entity);
        await uow.CompleteAsync();
    }
}

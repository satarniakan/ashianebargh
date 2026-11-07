using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;

namespace Ashianebargh.Application.Content;

public interface IPageService
{
    Task<List<PageListItemDto>> GetAllAsync();
    Task<PageEditDto?> GetAsync(int id);
    Task<PageEditDto> GetAboutForEditAsync();
    Task<int> SaveAsync(PageEditDto dto);
    Task SaveAboutAsync(PageEditDto dto);
    Task DeleteAsync(int id);
    Task<List<PageMenuItemDto>> GetMenuAsync();
    Task<PageViewDto?> GetPublishedBySlugAsync(string slug);
    Task<PageViewDto?> GetAboutAsync();
}

public sealed class PageService(IContentUnitOfWork uow) : IPageService
{
    public Task<List<PageListItemDto>> GetAllAsync() =>
        uow.Content.Pages
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Select(p => new PageListItemDto(
                p.Id, p.Title, p.Slug, p.IsPublished, p.ShowInMenu, p.SortOrder, p.CreatedAt))
            .ToListAsync();

    public Task<PageEditDto?> GetAsync(int id) =>
        uow.Content.Pages
            .Where(p => p.Id == id)
            .Select(p => new PageEditDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Content = p.Content,
                IsPublished = p.IsPublished,
                ShowInMenu = p.ShowInMenu,
                SortOrder = p.SortOrder
            })
            .FirstOrDefaultAsync();

    public async Task<PageEditDto> GetAboutForEditAsync()
    {
        var existing = await uow.Content.Pages
            .Where(p => p.Slug == Page.AboutSlug)
            .Select(p => new PageEditDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Content = p.Content,
                IsPublished = p.IsPublished,
                ShowInMenu = p.ShowInMenu,
                SortOrder = p.SortOrder
            })
            .FirstOrDefaultAsync();

        return existing ?? new PageEditDto
        {
            Title = "درباره ما",
            Slug = Page.AboutSlug,
            IsPublished = true
        };
    }

    public async Task<int> SaveAsync(PageEditDto dto)
    {
        var title = (dto.Title ?? string.Empty).Trim();
        if (title.Length == 0)
            throw new BusinessRuleException("عنوان صفحه را وارد کنید.");

        var slug = SlugHelper.Normalize(dto.Slug);
        if (slug.Length == 0) slug = SlugHelper.Normalize(title);
        if (slug.Length == 0)
            throw new BusinessRuleException("نامک معتبر نیست؛ حداقل یک حرف یا رقم لازم است.");

        if (SlugHelper.IsReserved(slug))
            throw new BusinessRuleException("این نامک برای مسیرهای ثابت سایت رزرو شده است؛ نامک دیگری انتخاب کنید.");

        if (await uow.Content.Pages.AnyAsync(p => p.Slug == slug && p.Id != dto.Id))
            throw new BusinessRuleException("این نامک قبلاً برای صفحهٔ دیگری استفاده شده است.");

        Page entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.Pages.FirstOrDefaultAsync(p => p.Id == dto.Id)
                ?? throw new NotFoundException("صفحه", dto.Id);

            if (entity.Slug == Page.AboutSlug)
                throw new BusinessRuleException("صفحهٔ درباره ما از مسیر مدیریت خودش ویرایش می‌شود.");
        }
        else
        {
            entity = new Page();
            uow.Content.Add(entity);
        }

        entity.Title = title;
        entity.Slug = slug;
        entity.Content = dto.Content;
        entity.IsPublished = dto.IsPublished;
        entity.ShowInMenu = dto.ShowInMenu;
        entity.SortOrder = dto.SortOrder;

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task SaveAboutAsync(PageEditDto dto)
    {
        var title = string.IsNullOrWhiteSpace(dto.Title) ? "درباره ما" : dto.Title.Trim();

        var entity = await uow.Content.Pages.FirstOrDefaultAsync(p => p.Slug == Page.AboutSlug);
        if (entity is null)
        {
            entity = new Page { Slug = Page.AboutSlug };
            uow.Content.Add(entity);
        }

        entity.Title = title;
        entity.Content = dto.Content;
        entity.IsPublished = dto.IsPublished;
        entity.ShowInMenu = false;
        entity.SortOrder = 0;

        await uow.CompleteAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.Pages.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("صفحه", id);

        if (entity.Slug == Page.AboutSlug)
            throw new BusinessRuleException("صفحهٔ درباره ما قابل حذف نیست.");

        uow.Content.Remove(entity);
        await uow.CompleteAsync();
    }

    public Task<List<PageMenuItemDto>> GetMenuAsync() =>
        uow.Content.Pages.AsNoTracking()
            .Where(p => p.IsPublished && p.ShowInMenu && p.Slug != Page.AboutSlug)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Select(p => new PageMenuItemDto(p.Title, p.Slug))
            .ToListAsync();

    public Task<PageViewDto?> GetPublishedBySlugAsync(string slug) =>
        uow.Content.Pages.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsPublished)
            .Select(p => new PageViewDto(p.Title, p.Slug, p.Content, p.UpdatedAt))
            .FirstOrDefaultAsync();

    public Task<PageViewDto?> GetAboutAsync() =>
        uow.Content.Pages.AsNoTracking()
            .Where(p => p.Slug == Page.AboutSlug && p.IsPublished)
            .Select(p => new PageViewDto(p.Title, p.Slug, p.Content, p.UpdatedAt))
            .FirstOrDefaultAsync();
}

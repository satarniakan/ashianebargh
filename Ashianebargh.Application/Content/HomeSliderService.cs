using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Application.Content;

public interface IHomeSliderService
{
    Task<List<HomeSlideListItemDto>> GetAllAsync();
    Task<HomeSlideEditDto?> GetAsync(int id);
    Task<int> SaveAsync(HomeSlideEditDto dto);
    Task DeleteAsync(int id);

    /// <summary>تعویض/حذف تصویر اسلاید؛ فایل قبلی پاک می‌شود تا فضای ذخیره‌سازی باد نکند.</summary>
    Task SetImageAsync(int id, string? storageId);

    Task<HomeSliderSettingsDto> GetSettingsAsync();
    Task SaveSettingsAsync(HomeSliderSettingsDto dto);

    /// <summary>اسلایدهای فعالِ دارای تصویر به ترتیب نمایش + تنظیمات؛ برای صفحهٔ اول.</summary>
    Task<HomeSliderPublicDto> GetPublicAsync();

    /// <summary>فقط تصویر اسلاید فعال سرو شود؛ اسلاید غیرفعال/پنهان رسانه‌اش لو نرود.</summary>
    Task<bool> IsImagePublicAsync(string storageId);

    /// <summary>شناسه تصاویر عمومی برای نگه‌داشتن نسخه‌های کش‌شده در خانه‌تکانی.</summary>
    Task<List<string>> GetPublicImageStorageIdsAsync();
}

public sealed class HomeSliderService(
    IContentUnitOfWork uow,
    ISettingService settings,
    IFileStorage storage,
    ILogger<HomeSliderService> logger) : IHomeSliderService
{
    public Task<List<HomeSlideListItemDto>> GetAllAsync() =>
        uow.Content.HomeSlides
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new HomeSlideListItemDto(
                s.Id, s.Title, s.TitleEn, s.ImageStorageId, s.IsActive, s.SortOrder))
            .ToListAsync();

    public Task<HomeSlideEditDto?> GetAsync(int id) =>
        uow.Content.HomeSlides
            .Where(s => s.Id == id)
            .Select(s => new HomeSlideEditDto
            {
                Id = s.Id,
                Title = s.Title,
                TitleEn = s.TitleEn,
                ImageStorageId = s.ImageStorageId,
                IsActive = s.IsActive,
                SortOrder = s.SortOrder
            })
            .FirstOrDefaultAsync();

    public async Task<int> SaveAsync(HomeSlideEditDto dto)
    {
        var title = (dto.Title ?? string.Empty).Trim();

        HomeSlide entity;
        if (dto.Id > 0)
        {
            entity = await uow.Content.HomeSlides.FirstOrDefaultAsync(s => s.Id == dto.Id)
                ?? throw new NotFoundException("اسلاید", dto.Id);
        }
        else
        {
            entity = new HomeSlide();
            uow.Content.Add(entity);
        }

        entity.Title = title.Length == 0 ? null : title;
        entity.TitleEn = string.IsNullOrWhiteSpace(dto.TitleEn) ? null : dto.TitleEn.Trim();
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;

        await uow.CompleteAsync();
        return entity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await uow.Content.HomeSlides.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("اسلاید", id);

        var storageId = entity.ImageStorageId;
        uow.Content.Remove(entity);
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(storageId))
            await TryDeleteFileAsync(storageId);
    }

    public async Task SetImageAsync(int id, string? storageId)
    {
        var entity = await uow.Content.HomeSlides.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("اسلاید", id);

        var oldStorageId = entity.ImageStorageId;
        entity.ImageStorageId = string.IsNullOrWhiteSpace(storageId) ? null : storageId;
        await uow.CompleteAsync();

        if (!string.IsNullOrEmpty(oldStorageId) && oldStorageId != entity.ImageStorageId)
            await TryDeleteFileAsync(oldStorageId);
    }

    public async Task<HomeSliderSettingsDto> GetSettingsAsync()
    {
        var transition = await settings.GetAsync(SliderSettingKeys.Transition);
        var interval = await settings.GetAsync(SliderSettingKeys.IntervalSeconds);
        var enabled = await settings.GetAsync(SliderSettingKeys.Enabled);

        return new HomeSliderSettingsDto
        {
            // نبود ردیف یا مقدار خراب یعنی روشن؛ فقط «false» صریح اسلایدر را خاموش می‌کند.
            Enabled = !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase),
            Transition = NormalizeTransition(transition),
            IntervalSeconds = int.TryParse(interval, out var seconds) &&
                seconds is >= SliderSettingKeys.MinIntervalSeconds and <= SliderSettingKeys.MaxIntervalSeconds
                ? seconds
                : SliderSettingKeys.DefaultIntervalSeconds
        };
    }

    public async Task SaveSettingsAsync(HomeSliderSettingsDto dto)
    {
        var transition = (dto.Transition ?? string.Empty).Trim();
        if (Array.IndexOf(SliderSettingKeys.Transitions, transition) < 0)
            throw new BusinessRuleException("نوع انیمیشن معتبر نیست.");

        if (dto.IntervalSeconds is < SliderSettingKeys.MinIntervalSeconds or > SliderSettingKeys.MaxIntervalSeconds)
            throw new BusinessRuleException("زمان نمایش باید بین ۲ و ۶۰ ثانیه باشد.");

        await settings.SetAsync(SliderSettingKeys.Transition, transition);
        await settings.SetAsync(SliderSettingKeys.IntervalSeconds,
            dto.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await settings.SetAsync(SliderSettingKeys.Enabled, dto.Enabled ? "true" : "false");
    }

    public async Task<HomeSliderPublicDto> GetPublicAsync()
    {
        var slides = await uow.Content.HomeSlides.AsNoTracking()
            .Where(s => s.IsActive && s.ImageStorageId != null && s.ImageStorageId != "")
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new HomeSlideViewDto(s.Title, s.TitleEn, s.ImageStorageId!))
            .ToListAsync();

        var current = await GetSettingsAsync();
        return new HomeSliderPublicDto(current.Enabled, current.Transition, current.IntervalSeconds, slides);
    }

    public Task<bool> IsImagePublicAsync(string storageId) =>
        uow.Content.HomeSlides.AsNoTracking()
            .AnyAsync(s => s.IsActive && s.ImageStorageId == storageId);

    public Task<List<string>> GetPublicImageStorageIdsAsync() =>
        uow.Content.HomeSlides.AsNoTracking()
            .Where(s => s.IsActive && s.ImageStorageId != null && s.ImageStorageId != "")
            .Select(s => s.ImageStorageId!)
            .ToListAsync();

    private static string NormalizeTransition(string? value) =>
        value is not null && Array.IndexOf(SliderSettingKeys.Transitions, value) >= 0
            ? value
            : SliderSettingKeys.DefaultTransition;

    private async Task TryDeleteFileAsync(string storageId)
    {
        try
        {
            await storage.DeleteAsync(ContentStorage.Owner, ContentStorage.SliderImageType, storageId);
        }
        catch (Exception ex)
        {
            // پاک‌نشدن فایل نباید حذف/ویرایش اسلاید را شکست دهد؛ فقط برای بررسی لاگ می‌شود.
            logger.LogWarning(ex, "حذف فایل تصویر اسلایدر ناموفق بود: {StorageId}", storageId);
        }
    }
}
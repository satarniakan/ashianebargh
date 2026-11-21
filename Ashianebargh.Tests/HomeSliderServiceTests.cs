using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.Application.Content;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Content;
using Ashianebargh.Infrastructure.Data;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// اسلایدر صفحهٔ اول: فقط اسلاید فعالِ دارای تصویر عمومی است، تنظیمات انیمیشن/زمان
/// اعتبارسنجی می‌شوند و فایل تصویر هنگام تعویض/حذف پاک می‌شود تا فضای ذخیره‌سازی باد نکند.
/// </summary>
public class HomeSliderServiceTests
{
    private static (AshianebarghDbContext db, ContentUnitOfWork uow) BuildDb()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"slider-{Guid.NewGuid()}")
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

    private static Mock<ISettingService> Settings(Dictionary<string, string?> store)
    {
        var mock = new Mock<ISettingService>();
        mock.Setup(s => s.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) =>
                store.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
                    ? value
                    : key switch
                    {
                        SliderSettingKeys.Transition => SliderSettingKeys.DefaultTransition,
                        SliderSettingKeys.IntervalSeconds => SliderSettingKeys.DefaultIntervalSeconds.ToString(),
                        _ => string.Empty
                    });
        mock.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback((string key, string? value) => store[key] = value)
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static HomeSliderService BuildService(
        ContentUnitOfWork uow,
        Dictionary<string, string?>? store = null,
        Mock<IFileStorage>? storage = null) =>
        new(uow, Settings(store ?? new Dictionary<string, string?>()).Object,
            (storage ?? new Mock<IFileStorage>()).Object,
            NullLogger<HomeSliderService>.Instance);

    private static async Task<int> AddSlideAsync(
        ContentUnitOfWork uow, string title, string? image, bool active = true, int order = 0)
    {
        var service = BuildService(uow);
        var id = await service.SaveAsync(new HomeSlideEditDto
        {
            Title = title,
            IsActive = active,
            SortOrder = order
        });
        if (image is not null)
            await service.SetImageAsync(id, image);
        return id;
    }

    [Fact]
    public async Task SaveAsync_TrimsTitle_AndNullsEmptyEnglish()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = BuildService(uow);

        var id = await service.SaveAsync(new HomeSlideEditDto
        {
            Title = "  متن اسلاید  ",
            TitleEn = "   "
        });

        var entity = await db.Set<HomeSlide>().FirstAsync(s => s.Id == id);
        Assert.Equal("متن اسلاید", entity.Title);
        Assert.Null(entity.TitleEn);
    }

    [Fact]
    public async Task SaveAsync_EmptyTitle_SavesImageOnlySlide()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = BuildService(uow);

        // متن اسلاید اختیاری است؛ اسلاید فقط‌تصویر باید ذخیره و عمومی شود.
        var id = await service.SaveAsync(new HomeSlideEditDto { Title = "   " });
        await service.SetImageAsync(id, "only-image.png");

        var entity = await db.Set<HomeSlide>().FirstAsync(s => s.Id == id);
        Assert.Null(entity.Title);

        var publicSlider = await service.GetPublicAsync();
        Assert.Single(publicSlider.Slides);
        Assert.Null(publicSlider.Slides[0].Title);
        Assert.Equal("only-image.png", publicSlider.Slides[0].ImageStorageId);
    }

    [Fact]
    public async Task GetPublicAsync_OnlyActiveWithImage_InDisplayOrder()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;

        await AddSlideAsync(uow, "دوم", "b.png", order: 2);
        await AddSlideAsync(uow, "اول", "a.png", order: 1);
        await AddSlideAsync(uow, "پنهان", "c.png", active: false);
        await AddSlideAsync(uow, "بدون تصویر", null);

        var publicSlider = await BuildService(uow).GetPublicAsync();

        Assert.Equal(new string?[] { "اول", "دوم" }, publicSlider.Slides.Select(s => s.Title));
        Assert.Equal(new[] { "a.png", "b.png" }, publicSlider.Slides.Select(s => s.ImageStorageId));
    }

    [Fact]
    public async Task GetPublicAsync_NoRows_ReturnsDefaultsAndEmptySlides()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;

        var publicSlider = await BuildService(uow).GetPublicAsync();

        Assert.Empty(publicSlider.Slides);
        Assert.Equal(SliderSettingKeys.DefaultTransition, publicSlider.Transition);
        Assert.Equal(SliderSettingKeys.DefaultIntervalSeconds, publicSlider.IntervalSeconds);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAndReadsBack()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var store = new Dictionary<string, string?>();
        var service = BuildService(uow, store);

        await service.SaveSettingsAsync(new HomeSliderSettingsDto { Transition = "slide", IntervalSeconds = 7, Enabled = false });

        var read = await service.GetSettingsAsync();
        Assert.Equal("slide", read.Transition);
        Assert.Equal(7, read.IntervalSeconds);
        Assert.False(read.Enabled);
        Assert.Equal("slide", store[SliderSettingKeys.Transition]);
        Assert.Equal("7", store[SliderSettingKeys.IntervalSeconds]);
        Assert.Equal("false", store[SliderSettingKeys.Enabled]);
    }

    [Fact]
    public void SliderSettingKeys_RegisteredInHostCatalog()
    {
        // کلیدِ ثبت‌نشده در کاتالوگ را SetAsync رد می‌کند و ذخیرهٔ تنظیمات اسلایدر می‌شکند.
        var catalogKeys = new Ashianebargh.App.AppSettingCatalog().All.Select(d => d.Key).ToHashSet();
        Assert.Subset(catalogKeys, new HashSet<string>
        {
            SliderSettingKeys.Enabled, SliderSettingKeys.Transition, SliderSettingKeys.IntervalSeconds
        });
    }

    [Fact]
    public async Task GetPublicAsync_DisabledSlider_ReportsDisabledWithSlides()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = BuildService(uow);
        await service.SaveSettingsAsync(new HomeSliderSettingsDto { Enabled = false });
        await AddSlideAsync(uow, "اسلاید", "a.png");

        var publicSlider = await service.GetPublicAsync();

        // خاموش‌بودن کل اسلایدر تصمیم رندر صفحهٔ اول است؛ سرویس همچنان اسلایدها را می‌دهد.
        Assert.False(publicSlider.Enabled);
        Assert.Single(publicSlider.Slides);
    }

    [Theory]
    [InlineData("zoom")]
    [InlineData("")]
    public async Task SaveSettingsAsync_UnknownTransition_Throws(string transition)
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = BuildService(uow);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SaveSettingsAsync(new HomeSliderSettingsDto { Transition = transition }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(61)]
    public async Task SaveSettingsAsync_IntervalOutOfRange_Throws(int seconds)
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = BuildService(uow);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SaveSettingsAsync(new HomeSliderSettingsDto { IntervalSeconds = seconds }));
    }

    [Fact]
    public async Task GetSettingsAsync_CorruptStoredValues_FallBackToDefaults()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var store = new Dictionary<string, string?>
        {
            [SliderSettingKeys.Transition] = "zoom",
            [SliderSettingKeys.IntervalSeconds] = "abc"
        };

        var read = await BuildService(uow, store).GetSettingsAsync();

        Assert.Equal(SliderSettingKeys.DefaultTransition, read.Transition);
        Assert.Equal(SliderSettingKeys.DefaultIntervalSeconds, read.IntervalSeconds);
        Assert.True(read.Enabled);
    }

    [Fact]
    public async Task IsImagePublicAsync_InactiveOrUnknown_False()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;

        await AddSlideAsync(uow, "فعال", "live.png");
        await AddSlideAsync(uow, "پنهان", "hidden.png", active: false);
        var service = BuildService(uow);

        Assert.True(await service.IsImagePublicAsync("live.png"));
        Assert.False(await service.IsImagePublicAsync("hidden.png"));
        Assert.False(await service.IsImagePublicAsync("missing.png"));
    }

    [Fact]
    public async Task SetImageAsync_Replacement_DeletesOldFile()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var storage = new Mock<IFileStorage>();
        var service = BuildService(uow, storage: storage);
        var id = await service.SaveAsync(new HomeSlideEditDto { Title = "اسلاید" });
        await service.SetImageAsync(id, "old.png");

        await service.SetImageAsync(id, "new.png");

        storage.Verify(s => s.DeleteAsync(
            ContentStorage.Owner, ContentStorage.SliderImageType, "old.png"), Times.Once);
        storage.Verify(s => s.DeleteAsync(
            ContentStorage.Owner, ContentStorage.SliderImageType, "new.png"), Times.Never);
        Assert.Equal("new.png", (await service.GetAsync(id))!.ImageStorageId);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesRow_AndDeletesFile()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var storage = new Mock<IFileStorage>();
        var service = BuildService(uow, storage: storage);
        var id = await AddSlideAsync(uow, "اسلاید", "gone.png");

        await service.DeleteAsync(id);

        Assert.Empty(await service.GetAllAsync());
        var row = await db.Set<HomeSlide>().IgnoreQueryFilters().FirstAsync(s => s.Id == id);
        Assert.True(row.IsDeleted);
        storage.Verify(s => s.DeleteAsync(
            ContentStorage.Owner, ContentStorage.SliderImageType, "gone.png"), Times.Once);
    }

    [Fact]
    public async Task GetPublicImageStorageIdsAsync_OnlyActiveWithImage()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;

        await AddSlideAsync(uow, "فعال", "keep.png");
        await AddSlideAsync(uow, "پنهان", "drop.png", active: false);
        await AddSlideAsync(uow, "بدون تصویر", null);

        var ids = await BuildService(uow).GetPublicImageStorageIdsAsync();

        Assert.Equal(new[] { "keep.png" }, ids);
    }
}
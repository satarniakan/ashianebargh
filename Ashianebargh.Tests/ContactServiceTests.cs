using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ashianebargh.Application.Content;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Content;
using Ashianebargh.Infrastructure.Data;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// فرم تماس عمومی: کپچا جلوی ارسال خودکار را می‌گیرد و سقف پنجره‌ای (سراسری/هر شماره)
/// جلوی پرکردن جدول پیام‌ها را. رگرسیونِ یافتهٔ امنیتی «فرم تماس بدون کپچا/محدودیت».
/// </summary>
public class ContactServiceTests
{
    private static (AshianebarghDbContext db, ContentUnitOfWork uow) BuildDb()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"contact-{Guid.NewGuid()}")
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

    private static Mock<ICaptchaValidator> Captcha(bool succeeds)
    {
        var mock = new Mock<ICaptchaValidator>();
        mock.Setup(c => c.ValidateAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds);
        return mock;
    }

    private static ContactFormDto Form(string phone = "09120000000") => new()
    {
        Name = "کاربر آزمایشی",
        Phone = phone,
        Message = "متن پیام آزمایشی"
    };

    [Fact]
    public async Task SubmitAsync_ValidForm_StoresMessage()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = new ContactService(uow, new MemoryCache(new MemoryCacheOptions()), Captcha(true).Object);

        await service.SubmitAsync(Form(), "token");

        Assert.Single(await db.Set<ContactMessage>().ToListAsync());
    }

    [Fact]
    public async Task SubmitAsync_CaptchaFails_Throws_AndStoresNothing()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = new ContactService(uow, new MemoryCache(new MemoryCacheOptions()), Captcha(false).Object);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(Form(), "bad-token"));

        Assert.Empty(await db.Set<ContactMessage>().ToListAsync());
    }

    [Fact]
    public async Task SubmitAsync_PerPhoneThrottle_BlocksFourthWithinWindow()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = new ContactService(uow, new MemoryCache(new MemoryCacheOptions()), Captcha(true).Object);

        // سه پیام اول مجازند؛ چهارمی در همان پنجره رد می‌شود.
        await service.SubmitAsync(Form(), "t");
        await service.SubmitAsync(Form(), "t");
        await service.SubmitAsync(Form(), "t");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(Form(), "t"));
        Assert.Equal(3, await db.Set<ContactMessage>().CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_ThrottleIsPerPhone_NotGlobalAcrossPhones()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = new ContactService(uow, new MemoryCache(new MemoryCacheOptions()), Captcha(true).Object);

        // سهمیهٔ شمارهٔ اول پر می‌شود…
        await service.SubmitAsync(Form("09120000001"), "t");
        await service.SubmitAsync(Form("09120000001"), "t");
        await service.SubmitAsync(Form("09120000001"), "t");
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(Form("09120000001"), "t"));

        // …ولی شمارهٔ دیگر سهمیهٔ مستقل خودش را دارد.
        await service.SubmitAsync(Form("09120000002"), "t");
        Assert.Equal(4, await db.Set<ContactMessage>().CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_EmptyName_Throws_BeforeConsumingQuota()
    {
        var (db, uow) = BuildDb();
        await using var _ = db;
        var service = new ContactService(uow, new MemoryCache(new MemoryCacheOptions()), Captcha(true).Object);

        var invalid = Form();
        invalid.Name = "";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(invalid, "t"));
        Assert.Empty(await db.Set<ContactMessage>().ToListAsync());
    }
}
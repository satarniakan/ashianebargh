using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.App.Services;
using Ashianebargh.Application.Content;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Tests;

/// <summary>
/// خانه‌تکانی داده‌ها: هر قلم با سقفِ خودش اجرا می‌شود؛ صفر/منفی → غیرفعال؛
/// شکست یک قلم بقیهٔ قلم‌ها را متوقف نمی‌کند.
/// </summary>
public class DataRetentionJobTests
{
    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTime now) => _now = new DateTimeOffset(now, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class Harness
    {
        public Mock<IServiceScopeFactory> ScopeFactory { get; } = new();
        public Mock<IServiceScope> Scope { get; } = new();
        public Mock<IServiceProvider> Sp { get; } = new();
        public Mock<ILoginHistoryRepository> LoginHistory { get; } = new();
        public Mock<IApiTokenRepository> ApiToken { get; } = new();
        public Mock<INotificationRepository> Notification { get; } = new();
        public Mock<IAuditLogRepository> AuditLog { get; } = new();
        public Mock<IOutboxRepository> Outbox { get; } = new();
        public Mock<IProductService> Products { get; } = new();
        public Mock<IHomeSliderService> Slider { get; } = new();
        public Mock<ICategoryService> Categories { get; } = new();

        /// <summary>ریشهٔ موقت کش نسخه‌های تصویر برای این اجرا.</summary>
        public string VariantRoot { get; } =
            Path.Combine(Path.GetTempPath(), $"ashretention-{Guid.NewGuid():N}");

        public ImageVariantService Variants { get; }

        public Harness()
        {
            LoginHistory.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
            ApiToken.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
            Notification.Setup(r => r.DeleteReadOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
            AuditLog.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
            Outbox.Setup(r => r.DeleteSentOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
            Products.Setup(p => p.GetPublicImageStorageIdsAsync()).ReturnsAsync(new List<string>());
            Slider.Setup(s => s.GetPublicImageStorageIdsAsync()).ReturnsAsync(new List<string>());
            Categories.Setup(c => c.GetPublicImageStorageIdsAsync()).ReturnsAsync(new List<string>());

            var variantConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = VariantRoot })
                .Build();
            Variants = new ImageVariantService(variantConfig, NullLogger<ImageVariantService>.Instance);

            Sp.Setup(s => s.GetService(typeof(ILoginHistoryRepository))).Returns(LoginHistory.Object);
            Sp.Setup(s => s.GetService(typeof(IApiTokenRepository))).Returns(ApiToken.Object);
            Sp.Setup(s => s.GetService(typeof(INotificationRepository))).Returns(Notification.Object);
            Sp.Setup(s => s.GetService(typeof(IAuditLogRepository))).Returns(AuditLog.Object);
            Sp.Setup(s => s.GetService(typeof(IOutboxRepository))).Returns(Outbox.Object);
            Sp.Setup(s => s.GetService(typeof(IProductService))).Returns(Products.Object);
            Sp.Setup(s => s.GetService(typeof(IHomeSliderService))).Returns(Slider.Object);
            Sp.Setup(s => s.GetService(typeof(ICategoryService))).Returns(Categories.Object);
            Sp.Setup(s => s.GetService(typeof(ImageVariantService))).Returns(Variants);

            Scope.Setup(s => s.ServiceProvider).Returns(Sp.Object);
            ScopeFactory.Setup(f => f.CreateScope()).Returns(Scope.Object);
        }

        public DataRetentionJob Build(IConfiguration config, DateTime now) =>
            new(ScopeFactory.Object, config, NullLogger<DataRetentionJob>.Instance,
                new FixedClock(now));
    }

    private static IConfiguration Config(params (string key, string value)[] items) =>
        new ConfigurationBuilder().AddInMemoryCollection(items.ToDictionary(i => i.key, i => (string?)i.value)).Build();

    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_ZeroOrNegativeDays_DoesNotCallRepository()
    {
        var h = new Harness();
        var job = h.Build(Config(
            ("Retention:LoginHistoryDays", "0"),
            ("Retention:ApiTokenDays", "-1"),
            ("Retention:OutboxDays", "0")), Now);

        await job.ExecuteAsync(default);

        h.LoginHistory.Verify(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
        h.ApiToken.Verify(r => r.DeleteExpiredAsync(It.IsAny<DateTime>()), Times.Never);
        h.Outbox.Verify(r => r.DeleteSentOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MissingKeys_UseDefaultDays()
    {
        var h = new Harness();
        var job = h.Build(new ConfigurationBuilder().Build(), Now);

        await job.ExecuteAsync(default);

        h.LoginHistory.Verify(r => r.DeleteOlderThanAsync(Now.AddDays(-DataRetentionJob.Defaults.LoginHistoryDays)), Times.Once);
        h.ApiToken.Verify(r => r.DeleteExpiredAsync(Now.AddDays(-DataRetentionJob.Defaults.ApiTokenDays)), Times.Once);
        h.Notification.Verify(r => r.DeleteReadOlderThanAsync(Now.AddDays(-DataRetentionJob.Defaults.NotificationDays)), Times.Once);
        h.AuditLog.Verify(r => r.DeleteOlderThanAsync(Now.AddDays(-DataRetentionJob.Defaults.AuditLogDays)), Times.Once);
        h.Outbox.Verify(r => r.DeleteSentOlderThanAsync(Now.AddDays(-DataRetentionJob.Defaults.OutboxDays)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OneRepositoryThrows_StillRunsOthers()
    {
        var h = new Harness();
        h.LoginHistory.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        h.ApiToken.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(7);

        var job = h.Build(Config(
            ("Retention:LoginHistoryDays", "30"),
            ("Retention:ApiTokenDays", "60"),
            ("Retention:OutboxDays", "90")), Now);

        await job.ExecuteAsync(default);

        h.ApiToken.Verify(r => r.DeleteExpiredAsync(Now.AddDays(-60)), Times.Once);
        h.Notification.Verify(r => r.DeleteReadOlderThanAsync(It.IsAny<DateTime>()), Times.Once);
        h.AuditLog.Verify(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>()), Times.Once);
        h.Outbox.Verify(r => r.DeleteSentOlderThanAsync(It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesEachTableAtConfiguredCutoff()
    {
        var h = new Harness();
        h.LoginHistory.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(1);
        h.ApiToken.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(2);
        h.Notification.Setup(r => r.DeleteReadOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(3);
        h.AuditLog.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(4);
        h.Outbox.Setup(r => r.DeleteSentOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(5);

        var job = h.Build(Config(
            ("Retention:LoginHistoryDays", "30"),
            ("Retention:ApiTokenDays", "60"),
            ("Retention:NotificationDays", "7"),
            ("Retention:AuditLogDays", "365"),
            ("Retention:OutboxDays", "90")), Now);

        await job.ExecuteAsync(default);

        h.LoginHistory.Verify(r => r.DeleteOlderThanAsync(Now.AddDays(-30)), Times.Once);
        h.ApiToken.Verify(r => r.DeleteExpiredAsync(Now.AddDays(-60)), Times.Once);
        h.Notification.Verify(r => r.DeleteReadOlderThanAsync(Now.AddDays(-7)), Times.Once);
        h.AuditLog.Verify(r => r.DeleteOlderThanAsync(Now.AddDays(-365)), Times.Once);
        h.Outbox.Verify(r => r.DeleteSentOlderThanAsync(Now.AddDays(-90)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PrunesImageVariantsThatAreNoLongerPublic()
    {
        var h = new Harness();
        // کش نسخه‌ها زیر «MediaVariants» در ریشهٔ Storage قرار می‌گیرد.
        var cacheRoot = Path.Combine(h.VariantRoot, "MediaVariants");
        Directory.CreateDirectory(cacheRoot);

        var keptPath = Path.Combine(cacheRoot, "public.png.320.webp");
        var orphanPath = Path.Combine(cacheRoot, "deleted.png.320.webp");
        var sliderPath = Path.Combine(cacheRoot, "slide.png.320.webp");
        var categoryPath = Path.Combine(cacheRoot, "cat.png.320.webp");
        await File.WriteAllTextAsync(keptPath, "webp");
        await File.WriteAllTextAsync(orphanPath, "webp");
        await File.WriteAllTextAsync(sliderPath, "webp");
        await File.WriteAllTextAsync(categoryPath, "webp");

        // هر دو فایل قدیمی‌تر از مهلت ۳۰روزه‌اند تا سنجش، مهلت را هم پوشش دهد.
        var old = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(keptPath, old);
        File.SetLastWriteTimeUtc(orphanPath, old);
        File.SetLastWriteTimeUtc(sliderPath, old);
        File.SetLastWriteTimeUtc(categoryPath, old);

        h.Products.Setup(p => p.GetPublicImageStorageIdsAsync())
            .ReturnsAsync(new List<string> { "public.png" });
        h.Slider.Setup(s => s.GetPublicImageStorageIdsAsync())
            .ReturnsAsync(new List<string> { "slide.png" });
        h.Categories.Setup(c => c.GetPublicImageStorageIdsAsync())
            .ReturnsAsync(new List<string> { "cat.png" });

        var job = h.Build(Config(("Retention:ImageVariantDays", "30")), Now);
        await job.ExecuteAsync(default);

        Assert.True(File.Exists(keptPath), "نسخهٔ تصویر عمومی نباید حذف شود.");
        Assert.True(File.Exists(sliderPath), "نسخهٔ تصویر اسلایدر فعال هم نباید حذف شود.");
        Assert.True(File.Exists(categoryPath), "نسخهٔ تصویر دستهٔ فعال هم نباید حذف شود.");
        Assert.False(File.Exists(orphanPath), "نسخهٔ تصویر غیرعمومی باید حذف شود.");
    }

    [Fact]
    public async Task ExecuteAsync_ImageVariantDaysZero_KeepsEverything()
    {
        var h = new Harness();
        var cacheRoot = Path.Combine(h.VariantRoot, "MediaVariants");
        Directory.CreateDirectory(cacheRoot);

        var orphanPath = Path.Combine(cacheRoot, "deleted.png.320.webp");
        await File.WriteAllTextAsync(orphanPath, "webp");
        File.SetLastWriteTimeUtc(orphanPath, new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        var job = h.Build(Config(("Retention:ImageVariantDays", "0")), Now);
        await job.ExecuteAsync(default);

        Assert.True(File.Exists(orphanPath));
        h.Products.Verify(p => p.GetPublicImageStorageIdsAsync(), Times.Never);
    }
}
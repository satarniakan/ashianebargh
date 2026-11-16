using Ashianebargh.Application.Content;
using Ashianebargh.Application.Jobs;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.App.Services;

/// <summary>
/// خانه‌تکانی دوره‌ای جدول‌های رشدکننده (هر ۲۴ ساعت). بدون این، تاریخچهٔ ورود،
/// توکن‌های منقضی، اعلان‌های خوانده‌شده و لاگ‌های رویداد تا ابد جمع می‌شوند.
/// <para>
/// سقف‌ها از appsettings خوانده می‌شوند (<c>Retention:*</c>)؛ خالی/صفر یعنی حذف نشود
/// (پیش‌فرض‌های محافظه‌کارانه در <see cref="Defaults"/>). تاریخچهٔ ورودِ تازه
/// عمداً مشمول نیست — حذفِ آن تصمیم دامنه است.
/// </para>
/// </summary>
public class DataRetentionJob : IRecurringJob
{
    /// <summary>پیش‌فرض سقف نگه‌داری هر جدول (روز). صفر یعنی حذف نشود.</summary>
    public static class Defaults
    {
        public const int LoginHistoryDays = 180;
        public const int NotificationDays = 90;
        public const int ApiTokenDays = 90;
        public const int AuditLogDays = 730;
        public const int OutboxDays = 90;

        /// <summary>
        /// مهلت نگه‌داری نسخه‌های کوچک‌شدهٔ تصویر که دیگر به محصولی عمومی تعلق ندارند
        /// (تصویر پاک/محصول غیرفعال شده). مهلت دارد تا رقابت با رندر هم‌زمان رخ ندهد.
        /// </summary>
        public const int ImageVariantDays = 30;
    }

    public string Name => "data-retention";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ILogger<DataRetentionJob> _logger;

    public DataRetentionJob(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<DataRetentionJob> logger, TimeProvider? clock = null)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var now = _clock.GetUtcNow().UtcDateTime;

        // هر قلم مستقل اجرا می‌شود: شکست یکی نباید بقیهٔ خانه‌تکانی را متوقف کند
        var deleted = 0;
        deleted += await RunOneAsync("تاریخخچه ورود", "Retention:LoginHistoryDays",
            Defaults.LoginHistoryDays, now,
            sp.GetRequiredService<ILoginHistoryRepository>().DeleteOlderThanAsync);

        // توکن‌های منقضی/لغوشده (روش موجود از قبل هست و cutoff کلی دارد)
        deleted += await RunOneAsync("توکن‌های API", "Retention:ApiTokenDays",
            Defaults.ApiTokenDays, now,
            sp.GetRequiredService<IApiTokenRepository>().DeleteExpiredAsync);

        deleted += await RunOneAsync("اعلان‌های خوانده‌شده", "Retention:NotificationDays",
            Defaults.NotificationDays, now,
            sp.GetRequiredService<INotificationRepository>().DeleteReadOlderThanAsync);

        deleted += await RunOneAsync("لاگ رویدادها", "Retention:AuditLogDays",
            Defaults.AuditLogDays, now,
            sp.GetRequiredService<IAuditLogRepository>().DeleteOlderThanAsync);

        // پیام‌های Sent جدول بی‌پایان می‌سازند؛ Failed برای بررسی ادمین می‌مانند
        deleted += await RunOneAsync("صف پیام‌ها", "Retention:OutboxDays",
            Defaults.OutboxDays, now,
            sp.GetRequiredService<IOutboxRepository>().DeleteSentOlderThanAsync);

        if (deleted > 0)
            _logger.LogInformation("خانه‌تکانی داده: {Count} ردیف حذف شد", deleted);

        // کش نسخه‌های کوچک‌شدهٔ تصویر از جنس «فایل» است نه «ردیف»؛ پس مسیر جدا دارد.
        await PruneImageVariantsAsync(sp, now);
    }

    /// <summary>
    /// پاک‌سازی فایل‌های کش تصویر که تصویر مبدأشان دیگر عمومی نیست.
    /// شکست این مرحله (مثلاً نبود دسترسی به دیسک) نباید بقیهٔ خانه‌تکانی را بشکند.
    /// </summary>
    private async Task PruneImageVariantsAsync(IServiceProvider sp, DateTime now)
    {
        var days = _configuration.GetValue("Retention:ImageVariantDays", Defaults.ImageVariantDays);
        if (days <= 0)
            return;

        try
        {
            var keep = (await sp.GetRequiredService<IProductService>().GetPublicImageStorageIdsAsync())
                .ToHashSet(StringComparer.Ordinal);

            var removed = sp.GetRequiredService<ImageVariantService>().Prune(keep, now.AddDays(-days));
            if (removed > 0)
                _logger.LogInformation("خانه‌تکانی کش تصویر: {Count} فایل حذف شد", removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در خانه‌تکانی کش تصویر");
        }
    }

    /// <summary>اجرای یک قلم با سقف پیکربندی‌شده؛ صفر/منفی یعنی غیرفعال. شکست فقط لاگ می‌شود.</summary>
    private async Task<int> RunOneAsync(string label, string configKey, int defaultDays,
        DateTime now, Func<DateTime, Task<int>> delete)
    {
        var days = _configuration.GetValue(configKey, defaultDays);
        if (days <= 0)
            return 0;

        try
        {
            var count = await delete(now.AddDays(-days));
            if (count > 0)
                _logger.LogInformation("خانه‌تکانی {Label}: {Count} ردیف قدیمی‌تر از {Days} روز حذف شد",
                    label, count, days);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در خانه‌تکانی {Label}", label);
            return 0;
        }
    }
}
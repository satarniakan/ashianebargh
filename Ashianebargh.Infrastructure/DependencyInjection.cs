using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Content;
using Ashianebargh.Infrastructure.Data;
using Ashianebargh.Infrastructure.Repositories;
using Ashianebargh.Infrastructure.Services;

namespace Ashianebargh.Infrastructure;

public static class DependencyInjection
{
    /// <param name="isDevelopment">
    /// پیش‌فرض false (امن‌ترین حالت). فقط در Development متن پیامک‌های شبیه‌سازی‌شده
    /// (شامل کد OTP) لاگ می‌شود.
    /// </param>
    public static IServiceCollection AddAshianebarghInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false,
        string? migrationsAssembly = null)
    {
        // گاردهای Production: استقرار تازه نباید «باز» یا «قفل» بالا بیاید.
        // بدون این‌ها یا OTP بدون کپچا پذیرفته می‌شود (provider غیر Turnstile)، یا
        // همهٔ ورودها شکست می‌خورند (کلید کپچا/pepper خالی) و ادمین اول هم نمی‌تواند
        // وارد شود تا آن‌ها را از صفحهٔ تنظیمات پر کند — بن‌بست چرخه‌ای.
        // مقادیر باید از env/appsettings برسند؛ چرخش بعدی از صفحهٔ تنظیمات (ردیف دیتابیس)
        // مجاز است به شرطی که env همچنان یک مقدار معتبر نگه دارد.
        if (!isDevelopment)
        {
            if (!string.Equals(configuration["Captcha:Provider"], "Turnstile", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Captcha:Provider باید در Production روی «Turnstile» تنظیم شود؛ در غیر این صورت " +
                    "درخواست‌های OTP بدون کپچا پذیرفته می‌شوند. برای اجرای محلی از محیط Development استفاده کنید.");

            if (string.IsNullOrWhiteSpace(configuration["Captcha:Turnstile:SecretKey"]) ||
                string.IsNullOrWhiteSpace(configuration["Captcha:Turnstile:SiteKey"]))
                throw new InvalidOperationException(
                    "کلیدهای Turnstile (Captcha:Turnstile:SecretKey و SiteKey) باید در Production از " +
                    "env/appsettings موجود باشند؛ با کلید خالی اعتبارسنج fail-closed است و هیچ ورودی " +
                    "(حتی ادمین اول) ممکن نیست.");

            if (string.IsNullOrWhiteSpace(configuration["Security:Pepper"]))
                throw new InvalidOperationException(
                    "Security:Pepper باید در Production تنظیم شود؛ با pepper خالی هش OTP/توکن به " +
                    "SHA-256 ساده برمی‌گردد و لو رفتن دیتابیس به‌تنهایی برای brute-force کافی است.");
        }

        services.AddDbContext<AshianebarghDbContext>(opt =>
        {
            // مدلِ این context به ماژول‌های دامنه بستگی دارد؛ بدون این، EF مدلِ هر
            // ماژول‌دار را برای contextهای بدون ماژول هم برمی‌گرداند.
            opt.ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>();
            opt.UseSqlServer(configuration.GetConnectionString("Default"),
                sql =>
                {
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);

                    // مایگریشن‌ها در مونتاژ میزبان‌اند نه پایه، تا اسکیمای دامنهٔ هر
                    // پروژه وارد فریم‌ورک مشترک نشود. میزبان اسم مونتاژ خودش را می‌دهد:
                    // AddAshianebarghInfrastructure(config, env.IsDevelopment(),
                    //     migrationsAssembly: typeof(Program).Assembly.GetName().Name)
                    if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                        sql.MigrationsAssembly(migrationsAssembly);
                });
        });

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // سیاست رمز صریح و مستند — پیش‌فرض‌های Identity (از جمله حداقل ۶ کاراکتر)
            // قبلاً هیچ‌جا نوشته نشده بود و فرم «حداقل ۳ کاراکتر» وعده می‌داد ولی
            // ذخیره‌سازی رد می‌کرد. این دو باید همیشه با هم به‌روز شوند.
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;

            // تنظیمات قفل موقت حساب — AuthService با lockoutOnFailure:true کار می‌کند
            // (در Identity پیش‌فرض FALSE است)؛ بدون این، مهاجم می‌توانست بی‌نهایت رمز
            // روی یک حساب امتحان کند. محدودیت نرخ فقط جلوی انبوه IP را می‌گیرد.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AshianebarghDbContext>()
        .AddDefaultTokenProviders()
        .AddErrorDescriber<PersianIdentityErrorDescriber>()
        .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

        // AddIdentity به‌صورت پیش‌فرض "/Account/Login" را برای صفحهٔ ورود می‌داند،
        // در حالی که صفحهٔ واقعی ورود اینجا "/login" است. بدون این، کاربر لاگ‌اوت‌شده
        // به مسیر اشتباه ریدایرکت می‌شود. (باید بعد از AddIdentity بیاید.)
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";

            // سفت‌وبندی صریح به‌جای اتکا به پیش‌فرض‌ها:
            // - SecurePolicy در Development همان SameAsRequest می‌ماند تا لاگین روی
            //   http://localhost هم کار کند؛ در Production همیشه Always (کوکی فقط روی HTTPS).
            // - SameSite=Lax: تعادل CSRF و ناوبری عادی.
            // - انقضای ۸ ساعته با تمدید لغزان به‌جای ۱۴ روز پیش‌فرض Identity.
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddScoped<IAshianebarghUnitOfWork, AshianebarghUnitOfWork>();

        // ماژول محتوا (محصولات، دسته‌ها، برچسب‌ها، صفحات و تماس با ما)
        services.AddSingleton<IAshianebarghModule, ContentModelModule>();
        services.AddScoped<IContentUnitOfWork, ContentUnitOfWork>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOtpRepository, OtpCodeRepository>();
        services.AddScoped<IOtpThrottleRepository, OtpThrottleRepository>();
        services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
        services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<INumberSequenceRepository, NumberSequenceRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IPushSender, VapidPushSender>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // «چه کسی ساخت/ویرایش کرد» روی همهٔ AuditableEntityها ثبت می‌شود.
        // AddHttpContextAccessor لازم است؛ بیرون از درخواست HTTP مقدار null برمی‌گردد.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // منبع زمان مشترک — سرویس‌ها و DbContext همگی از همین می‌خوانند تا در تست قابل ثابت‌کردن باشد
        services.TryAddSingleton(TimeProvider.System);

        // پیامک: بر اساس «Sms:Provider» — Kavenegar واقعی یا Fake.
        // FakeSmsSender فقط در Development متن پیامک (شامل کد OTP) را لاگ می‌کند.
        // سرویس‌های بیرونی (پیامک/کپچا) timeout صریح ۱۵ ثانیه دارند تا یک
        // سرویس کند، threadpool و OutboxProcessor را قفل نکند (پیش‌فرض ۱۰۰ ثانیه است).
        services.AddHttpClient("kavenegar", client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient("turnstile", client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient();

        // کپچا: بر اساس «Captcha:Provider» — Turnstile واقعی، وگرنه غیرفعال.
        // غیرفعال یعنی endpointها بدون کپچا کار می‌کنند (توسعه)؛ در Production حتماً Turnstile بگذارید.
        services.AddScoped<ICaptchaValidator>(sp =>
        {
            if (string.Equals(configuration["Captcha:Provider"], "Turnstile", StringComparison.OrdinalIgnoreCase))
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("turnstile");
                return new TurnstileCaptchaValidator(
                    http,
                    sp.GetRequiredService<ISettingService>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<TurnstileCaptchaValidator>>());
            }

            var fake = string.Equals(configuration["Captcha:Provider"], "Fake", StringComparison.OrdinalIgnoreCase);
            return new PermissiveCaptchaValidator(
                sp.GetRequiredService<ILogger<PermissiveCaptchaValidator>>(),
                name: fake ? "fake" : "none",
                logTokens: fake && isDevelopment);
        });

        services.AddScoped<ISmsSender>(sp =>
        {
            if (string.Equals(configuration["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase))
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("kavenegar");
                return new KavenegarSmsSender(
                    http,
                    sp.GetRequiredService<ISettingService>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<KavenegarSmsSender>>());
            }

            return new FakeSmsSender(
                sp.GetRequiredService<ILogger<FakeSmsSender>>(), logMessageBody: isDevelopment);
        });

        // ایمیل تراکنشی — مقادیر از تنظیمات (ردیف دیتابیس، وگرنه «Email:Smtp:*»)؛
        // Host خالی یعنی ارسال با خطای روشن در Outbox ثبت می‌شود
        services.AddScoped<IEmailSender>(sp => new SmtpEmailSender(
            sp.GetRequiredService<ISettingService>(),
            sp.GetRequiredService<IConfiguration>()));

        // اعلان تلگرام (اختیاری: فقط وقتی BotToken/ChatId تنظیم شده باشد فعال می‌شود)
        services.AddSingleton<ITelegramNotifier, TelegramNotifier>();

        return services;
    }

    /// <summary>
    /// ساخت نقش‌های پایه و همگام‌سازی مجوزهای ادمین. باید بعد از
    /// <c>app.Build()</c>، و پس از مایگریشن دیتابیس و قبل از <c>app.Run()</c> صدا زده شود.
    /// </summary>
    public static async Task InitializeAshianebarghAsync(
        this IServiceProvider services, IReadOnlyList<string>? extraRoles = null)
    {
        await RoleSeeder.SeedAsync(services, extraRoles);
    }
}
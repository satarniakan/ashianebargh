using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Ashianebargh.Application;
using Ashianebargh.Infrastructure;
using Ashianebargh.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Ashianebargh.Application.Content;
using Ashianebargh.Application.Jobs;
using Ashianebargh.App.Authentication;
using Ashianebargh.App.Authorization;
using Ashianebargh.App.Endpoints;
using Microsoft.FeatureManagement;
using Ashianebargh.App.Middleware;
using Ashianebargh.App.Services;

namespace Ashianebargh.App;

/// <summary>
/// متدهای راه‌اندازی مشترک برای هر برنامه‌ای که روی Ashianebargh می‌نشیند.
/// هر کدام فقط به پایه اشاره دارند؛ هیچ تنظیم دامنه‌ای اینجا نیست.
/// ترتیب فراخوانی در <c>Program.cs</c> مهم است و در README مستند شده.
/// </summary>
public static class AshianebarghSetup
{
    /// <summary>
    /// ثبت سرویس‌ها: Application + Infrastructure + پیام‌رسانی + OTP + Outbox.
    /// </summary>
    public static IServiceCollection AddAshianebargh(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment, string? migrationsAssembly = null)
    {
        services.AddAshianebarghApplication();
        services.AddAshianebarghInfrastructure(configuration, isDevelopment, migrationsAssembly);

        // cache توزیع‌شده: اگر اتصال Redis تنظیم شده باشد از Redis استفاده می‌شود،
        // در غیر این صورت InMemory (برای توسعه و محیط‌های تک‌نشانه‌ای).
        var redisConfig = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConfig))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConfig;
                options.InstanceName = "Ashianebargh:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // Health Checks UI (AspNetCore.HealthChecks.UI 9.x) با EF Core 10 باینری‌سازگار
        // نیست (MissingMethodException هنگام استارتاپ). تا انتشار نسخهٔ 10، فقط /health
        // فعال است؛ داشبورد UI با پکیج سازگار دوباره اضافه می‌شود.

        // سرویس ایمپورت محصولات از tksterminal.com
        services.AddHttpClient("tksimport", client =>
        {
            client.BaseAddress = new Uri("https://tksterminal.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ITksImportService, TksImportService>();

        // ساخت نسخه‌های کوچک‌شدهٔ تصاویر محصول (srcset). سرویس سبک است ولی ساخت
        // نسخهٔ جدید I/O و CPU دارد؛ scoped می‌ماند تا با logging درخواست هم‌تراز باشد.
        services.AddScoped<ImageVariantService>();

        // Feature Flags (Microsoft.FeatureManagement) — بخش‌های اختیاری را بدون تغییر کد روشن/خاموش کنید
        services.AddFeatureManagement();

        // کارهای دوره‌ای با IRecurringJob/BackgroundService اجرا می‌شوند (DataRetentionJob،
        // OutboxProcessor)؛ Quartz ثبت می‌شد ولی هیچ jobی نداشت و فقط یک hosted service
        // بلااستفاده بالا می‌آورد.
        services.AddCascadingAuthenticationState();

        // fetchهای JS توکن ضدجعل را در هدر X-XSRF-TOKEN می‌فرستند (ashianebarghApi.post)؛
        // فرم‌ها همان فیلد مخفی پیش‌فرض. اینجا ثبت می‌شود (نه Program) تا هر میزبانی
        // که MapAshianebargh را صدا می‌زند — از جمله هاست تست — سرویس را داشته باشد.
        services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

        // پایه خودش سرویس‌های مجوزدهی را ثبت می‌کند تا به «تصادفی» بودنِ
        // AddRazorComponents در میزبان وابسته نباشد (وگرنه هر میزبانی که
        // Razor Components اضافه نکند، هنگام UseAuthorization خطا می‌گیرد).
        services.AddAuthentication(options =>
        {
            options.DefaultForbidScheme = ApiTokenAuthenticationHandler.SchemeName;
        })
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
                ApiTokenAuthenticationHandler.SchemeName, displayName: "API Token", configureOptions: _ => { });

        services.AddAuthorizationBuilder()
            // endpointهای API موبایل: فقط توکن، نه کوکی
            .AddPolicy("Api", policy => policy
                .AddAuthenticationSchemes(ApiTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser())
            // endpointهای مشترک وب و موبایل (اعلان‌ها): کوکی یا توکن
            .AddPolicy("App", policy => policy
                .AddAuthenticationSchemes(
                    IdentityConstants.ApplicationScheme,
                    ApiTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser());

        // deny-by-default: هر endpoint/صفحه‌ای که صراحتاً «عمومی» علامت نخورده،
        // ورود می‌خواهد. این تور ایمنی است تا مسیر جدیدِ فراموش‌شده تصادفاً عمومی
        // نماند. مسیرهای عمومی با [AllowAnonymous] / .AllowAnonymous() مشخص شده‌اند
        // (صفحه‌های سایت، فرم‌های ورود، رسانه، /health، /lang و دارایی‌های استاتیک).
        // نکته: سیاست fallback فقط روی endpointهایی اعمال می‌شود که هیچ متادیتای
        // مجوزدهی ندارند؛ مسیرهای دارای RequireAuthorization/Authorize از policy خودشان استفاده می‌کنند.
        services.Configure<AuthorizationOptions>(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // سیاست‌ساز خودکار: هر کلید IPermissionCatalog خودش یک policy است.
        // باید بعد از AddAuthorization بیاید تا جایگزین provider پیش‌فرض شود.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        services.AddScoped<ToastService>();
        services.AddSingleton<RecurringJobStatusTracker>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<RecurringJobRunner>();
        services.AddSingleton<IRecurringJob, OtpCleanupJob>();
        services.AddSingleton<IRecurringJob, DataRetentionJob>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // درخواست‌های کد پیامکی: حداکثر ۳ در هر ۵ دقیقه از هر IP.
            // (سقف دقیق‌ترِ «به‌ازای هر شماره» در خود OtpService است، چون HTTP فقط IP را می‌بیند.)
            options.AddPolicy("otp-request", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

            // حدس کد: حداکثر ۸ تلاش در هر ۵ دقیقه.
            options.AddPolicy("otp-verify", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 8,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

            // ورود با رمز: حداکثر ۱۰ تلاش در هر ۱۵ دقیقه برای جلوگیری از حدس زدن رمز.
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0
                    }));

            // تکمیل پروفایل/گذاشتن رمز — عملیات گرانِ PBKDF2 است و قبلاً هیچ محدودیتی نداشت،
            // پس یک اسکریپت می‌توانست با درخواست‌های مکرر، CPU سرور را بسوزاند.
            // توجه: این policy فقط مسیر HTTP را می‌پوشاند؛ صفحهٔ پروفایل circuit بلazor است
            // و رویدادهایش از اینجا رد نمی‌شوند، پس سقفِ آن مسیر داخل AuthService است.
            options.AddPolicy("profile", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>
    /// مسیریابی پیش‌فرض پایه: احراز هویت + اعلان‌ها. مسیریابی دامنه بعد از این می‌آید.
    /// </summary>
    public static IEndpointRouteBuilder MapAshianebargh(this IEndpointRouteBuilder app)
    {
        app.MapAccountEndpoints();
        app.MapNotificationsEndpoints();
        app.MapNotificationsApiEndpoints();
        app.MapAuthApiEndpoints();
        app.MapPushEndpoints();
        app.MapImpersonationEndpoints();
        app.MapFileEndpoints();
        app.MapMediaEndpoints();
        app.MapAdminJobsEndpoints();
        return app;
    }

    /// <summary>
    /// تنظیمات پایه که باید در <c>Program.cs</c> میزبان اعمال شود، به‌ترتیب:
    /// هدرهای forwarded، فرهنگ فارسی، مدیریت خطا، middlewareهای امنیتی.
    /// </summary>
    public static RequestLocalizationOptions PersianLocalization()
    {
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("fa-IR"),
            SupportedCultures = [new CultureInfo("fa-IR"), new CultureInfo("en-US")],
            SupportedUICultures = [new CultureInfo("fa-IR"), new CultureInfo("en-US")]
        };

        // کوکی زبان سایت بر تنظیمات مرورگر اولویت دارد
        options.RequestCultureProviders.Insert(0, new Ashianebargh.App.Services.SiteLanguageCultureProvider());
        return options;
    }
}
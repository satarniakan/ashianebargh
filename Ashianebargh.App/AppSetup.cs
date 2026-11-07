using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.App.Components.Layout;

namespace Ashianebargh.App;

/// <summary>
/// کاتالوگ مجوزها: کلید هر بخش از پنل محتوا. ادمین همه را خودکار دارد
/// (RoleSeeder هنگام استارتاپ همگام می‌کند)، بقیهٔ نقش‌ها از صفحهٔ «مجوز نقش‌ها».
/// </summary>
public sealed class AppPermissionCatalog : IPermissionCatalog
{
    public IReadOnlyList<PermissionDescriptor> All { get; } =
    [
        new(ContentPermissions.Products, "مدیریت محصولات", "محتوا"),
        new(ContentPermissions.Categories, "مدیریت دسته‌بندی‌ها", "محتوا"),
        new(ContentPermissions.Tags, "مدیریت برچسب‌ها", "محتوا"),
        new(ContentPermissions.Pages, "مدیریت صفحات", "محتوا"),
        new(ContentPermissions.Contact, "مدیریت تماس با ما", "محتوا")
    ];
}

/// <summary>
/// کاتالوگ تنظیمات میزبان: کلیدهای پایه + همهٔ تنظیمات اتصال‌ها (پیامک، ایمیل،
/// پرداخت، کپچا، وب‌پوش، توکن). مقادیر محرمانه رمزنگاری‌شده ذخیره می‌شوند.
/// ترتیب مقدار مؤثر هر کلید: ردیف دیتابیس ← appsettings ← پیش‌فرض؛ پس خالی
/// گذاشتن یعنی «از appsettings بخوان».
/// </summary>
public sealed class AppSettingCatalog : ISettingCatalog
{
    public IReadOnlyList<SettingDescriptor> All { get; } =
    [
        new(FrameworkSettingKeys.SiteName, "نام سامانه", "آشیانه برق", "عمومی"),
        new(FrameworkSettingKeys.OtpSmsTemplate, "قالب پیامک کد ورود ({Code})",
            SettingDefaults.Get(FrameworkSettingKeys.OtpSmsTemplate), "پیامک"),

        new(IntegrationSettingKeys.SmsKavenegarApiKey, "کلید API کاوه‌نگار (Sms:Kavenegar:ApiKey)",
            "", "پیامک", IsSecret: true),
        new(IntegrationSettingKeys.SmsKavenegarSender, "شماره خط ارسال‌کننده (Sms:Kavenegar:Sender)",
            "", "پیامک"),

        new(IntegrationSettingKeys.SmtpHost, "هاست SMTP (Email:Smtp:Host)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpPort, "پورت SMTP (پیش‌فرض 587)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpUsername, "نام کاربری SMTP", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpPassword, "رمز SMTP", "", "ایمیل", IsSecret: true),
        new(IntegrationSettingKeys.SmtpFromAddress, "فرستنده (FromAddress)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpFromName, "نام فرستنده (FromName)", "", "ایمیل"),

        new(IntegrationSettingKeys.ZarinPalMerchantId, "مرچنت زرین‌پال (Payment:ZarinPal:MerchantId)",
            "", "پرداخت", IsSecret: true),
        new(IntegrationSettingKeys.ZarinPalSandbox, "حالت sandbox زرین‌پال (true/false)", "", "پرداخت"),

        new(IntegrationSettingKeys.ZibalMerchantId, "مرچنت زیبال (Payment:Zibal:MerchantId)",
            "", "پرداخت", IsSecret: true),
        new(IntegrationSettingKeys.ZibalSandbox, "حالت sandbox زیبال (true/false)", "", "پرداخت"),

        new(IntegrationSettingKeys.TurnstileSecretKey, "کلید محرمانه Turnstile (سرور)",
            "", "کپچا", IsSecret: true),
        new(IntegrationSettingKeys.TurnstileSiteKey, "کلید عمومی Turnstile (ویجت لاگین)",
            "", "کپچا"),

        new(IntegrationSettingKeys.VapidSubject, "آدرس تماس VAPID (مثل mailto:admin@example.com)", "", "وب‌پوش"),
        new(IntegrationSettingKeys.VapidPublicKey, "کلید عمومی VAPID", "", "وب‌پوش"),
        new(IntegrationSettingKeys.VapidPrivateKey, "کلید خصوصی VAPID", "", "وب‌پوش", IsSecret: true),

        new(IntegrationSettingKeys.ApiTokenLifetimeDays, "عمر توکن موبایل به روز (پیش‌فرض ۱۸۰)", "", "توکن")
    ];
}

/// <summary>
/// منوی میزبان: بخش‌های پایه + گروه «محتوا».
/// </summary>
public sealed class AppNavProvider : INavProvider
{
    public IReadOnlyList<NavItem> GetRootItems() =>
    [
        // داشبورد پنل
        NavItem.Link("داشبورد", "dashboard", "bi-speedometer2"),

        // محتوای سایت
        NavItem.Group("محتوا", "bi-collection",
            NavItem.Link("محصولات", "content/products", "bi-box-seam", ContentPermissions.Products),
            NavItem.Link("دسته‌بندی‌ها", "content/categories", "bi-diagram-3", ContentPermissions.Categories),
            NavItem.Link("برچسب‌ها", "content/tags", "bi-tags", ContentPermissions.Tags),
            NavItem.Link("صفحات", "content/pages", "bi-file-earmark-text", ContentPermissions.Pages),
            NavItem.Link("درباره ما", "content/about", "bi-info-circle", ContentPermissions.Pages),
            NavItem.Link("تماس با ما", "content/contact", "bi-telephone", ContentPermissions.Contact)),

        // مدیریت کاربران، مجوزها و پیام‌رسانی
        NavItem.Group("مدیریت", "bi-shield-lock",
            NavItem.Link("کاربران", "admin/users", "bi-people", Roles.Admin),
            NavItem.Link("مجوز نقش‌ها", "admin/roles", "bi-key", Roles.Admin),
            NavItem.Link("اعلان سراسری", "admin/broadcast", "bi-megaphone", Roles.Admin),
            NavItem.Link("صف پیام‌ها", "admin/outbox", "bi-send", Roles.Admin),
            NavItem.Link("پرداخت‌ها", "admin/payments", "bi-credit-card", Roles.Admin),
            NavItem.Link("تنظیمات", "admin/settings", "bi-sliders", Roles.Admin)),

        // سیستم — لاگ‌ها و تنظیمات حساب
        NavItem.Group("سیستم", "bi-gear",
            NavItem.Link("گزارش رویدادها", "audit-logs", "bi-journal-text", Roles.Admin),
            NavItem.Link("اعلان‌ها", "notifications", "bi-bell"),
            NavItem.Link("نشست‌های فعال", "sessions", "bi-phone"),
            NavItem.Link("پروفایل", "profile", "bi-person"))
    ];
}

/// <summary>
/// سیاست پیش‌فرض دسترسی فایل: خودِ مالک یا ادمین. ماژول دامنه می‌تواند
/// با ثبت <see cref="IFileAccessPolicy"/> خودش جایگزینش کند (TryAdd پایین).
/// </summary>
public sealed class OwnerOrAdminFileAccessPolicy : IFileAccessPolicy
{
    public Task<bool> CanReadAsync(string userId, bool isAdmin, string ownerId, string fileType)
        => Task.FromResult(isAdmin || string.Equals(userId, ownerId, StringComparison.Ordinal));
}

/// <summary>
/// راه‌اندازی میزبان.
/// </summary>
public static class AppSetup
{
    /// <summary>
    /// کاتالوگ مجوزها، کاتالوگ تنظیمات و منو: همین سه نقطه‌اند که پایه به دامنه وصل می‌شوند.
    /// </summary>
    public static IServiceCollection AddAppWeb(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionCatalog, AppPermissionCatalog>();
        services.AddSingleton<ISettingCatalog, AppSettingCatalog>();
        services.AddSingleton<INavProvider, AppNavProvider>();
        // سیاست فایل با TryAdd تا پروژهٔ مصرف‌کننده بتواند قبل از/بعد از این پیش‌فرض را ثبت کند
        services.TryAddSingleton<IFileAccessPolicy, OwnerOrAdminFileAccessPolicy>();
        return services;
    }
}

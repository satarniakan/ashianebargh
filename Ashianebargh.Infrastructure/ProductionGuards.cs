using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Ashianebargh.Infrastructure;

/// <summary>
/// گاردهای استارتاپ Production.
/// <para>
/// چرا فقط «خالی نبودن» کافی نیست: مقادیر نمونهٔ <c>.env.example</c> هم غیرخالی‌اند.
/// اگر پذیرفته شوند، استقرار با pepper عمومیِ داخل ریپو بالا می‌آید (لو رفتن
/// دیتابیس به‌تنهایی برای brute-force کافی می‌شود) و شمارهٔ ادمین اولِ نمونه
/// به هر دارندهٔ آن شماره نقش Admin می‌دهد. پس هر مقدار باید هم موجود باشد،
/// هم نمونه نباشد، هم حداقل آنتروپی را داشته باشد.
/// </para>
/// <para>
/// این گاردها عمداً fail-fast هستند: استقرارِ «باز» یا «قفل» بدتر از استقرارِ
/// متوقف‌شده با پیام روشن است، چون ادمین اول هنوز رمز ندارد و نمی‌تواند
/// از صفحهٔ تنظیمات مشکل را رفع کند (بن‌بست چرخه‌ای).
/// </para>
/// </summary>
public static partial class ProductionGuards
{
    /// <summary>حداقل طول pepper — کوتاه‌تر از این، حدس آفلاین عملی می‌شود.</summary>
    public const int MinPepperLength = 32;

    /// <summary>حداقل تعداد کاراکتر متمایز pepper؛ جلوی «aaaa…» را می‌گیرد.</summary>
    public const int MinPepperDistinctChars = 8;

    private const int MinTurnstileKeyLength = 16;

    /// <summary>شمارهٔ ادمین نمونهٔ ریپو؛ پذیرفتنش یعنی Admin عمومی.</summary>
    public const string SampleAdminPhone = "09120000000";

    // نشانگرهای «مقدار نمونه / جایگزین‌نشده». عمداً عمومی انتخاب شده‌اند تا
    // قالب‌های دیگری که مصرف‌کننده از این پایه می‌سازد هم پوشش داده شوند.
    private static readonly string[] SampleMarkers =
    [
        "changeme", "change_me", "replace_me", "replaceme", "dev_pass",
        "example", "dummy", "placeholder", "your-", "your_", "xxxx", "todo",
    ];

    [GeneratedRegex("^09[0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex IranianMobileRegex();

    /// <summary>
    /// آیا این مقدار یکی از نمونه‌های شناخته‌شدهٔ ریپو/قالب است؟
    /// مقادیر خالی اینجا «نمونه» نیستند؛ گاردِ جداگانهٔ حضور، خالی را رد می‌کند.
    /// </summary>
    public static bool LooksLikeSample(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        foreach (var marker in SampleMarkers)
        {
            if (trimmed.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>همهٔ گاردهای Production را یک‌جا اعمال می‌کند.</summary>
    public static void ValidateAll(IConfiguration configuration)
    {
        ValidateCaptcha(configuration);
        ValidatePepper(configuration);
        ValidateFirstAdminPhone(configuration);
        ValidateSms(configuration);
    }

    /// <summary>
    /// کپچای واقعی الزامی است: provider غیر Turnstile یعنی درخواست‌های OTP
    /// بدون کپچا پذیرفته می‌شوند، و کلید خالی/نمونه یعنی اعتبارسنج fail-closed
    /// است و هیچ ورودی (حتی ادمین اول) ممکن نیست.
    /// </summary>
    public static void ValidateCaptcha(IConfiguration configuration)
    {
        if (!string.Equals(configuration["Captcha:Provider"], "Turnstile", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Captcha:Provider باید در Production روی «Turnstile» تنظیم شود؛ در غیر این صورت " +
                "درخواست‌های OTP بدون کپچا پذیرفته می‌شوند. برای اجرای محلی از محیط Development استفاده کنید.");

        RequireTurnstileKey(configuration["Captcha:Turnstile:SiteKey"], "Captcha:Turnstile:SiteKey");
        RequireTurnstileKey(configuration["Captcha:Turnstile:SecretKey"], "Captcha:Turnstile:SecretKey");
    }

    /// <summary>
    /// pepper نمک سمت سرورِ هش OTP/توکن است. خالی یعنی بازگشت به SHA-256 ساده،
    /// کوتاه یا کم‌آنتروپی یعنی حدس آفلاین، و نمونه یعنی کلید عمومیِ ریپو.
    /// </summary>
    public static void ValidatePepper(IConfiguration configuration)
    {
        var pepper = configuration["Security:Pepper"];

        if (string.IsNullOrWhiteSpace(pepper))
            throw new InvalidOperationException(
                "Security:Pepper باید در Production تنظیم شود؛ با pepper خالی هش OTP/توکن به " +
                "SHA-256 ساده برمی‌گردد و لو رفتن دیتابیس به‌تنهایی برای brute-force کافی است.");

        if (LooksLikeSample(pepper))
            throw new InvalidOperationException(
                "Security:Pepper یک مقدار نمونه است (مثل ChangeMe_RandomLongSecret در .env.example). " +
                "این مقدار در ریپو عمومی است؛ یک رشتهٔ تصادفی بلند بگذارید، مثلاً خروجی openssl rand -hex 32.");

        if (pepper.Trim().Length < MinPepperLength)
            throw new InvalidOperationException(
                $"Security:Pepper باید دست‌کم {MinPepperLength} کاراکتر باشد؛ مقدار کوتاه‌تر " +
                "حدس آفلاینِ هش‌های OTP/توکن را عملی می‌کند.");

        if (pepper.Trim().Distinct().Count() < MinPepperDistinctChars)
            throw new InvalidOperationException(
                $"Security:Pepper باید دست‌کم {MinPepperDistinctChars} کاراکتر متمایز داشته باشد؛ " +
                "رشتهٔ تکراری (مثل «aaaa…») آنتروپی ندارد.");
    }

    /// <summary>
    /// شمارهٔ ادمین اول: تنها راه bootstrap روی دیتابیس تازه است (ادمین اول رمز
    /// ندارد و با OTP وارد می‌شود). نبودنش بن‌بست می‌سازد و مقدار نمونه‌اش
    /// به هر دارندهٔ آن شماره Admin می‌دهد.
    /// </summary>
    public static void ValidateFirstAdminPhone(IConfiguration configuration)
    {
        var phone = configuration["Identity:FirstAdminPhoneNumber"]?.Trim();

        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException(
                "Identity:FirstAdminPhoneNumber باید در Production تنظیم شود؛ بدون آن روی دیتابیس " +
                "تازه هیچ ادمینی ساخته نمی‌شود و صفحهٔ تنظیمات هم پشت ورود قفل می‌ماند.");

        if (phone == SampleAdminPhone || LooksLikeSample(phone))
            throw new InvalidOperationException(
                $"Identity:FirstAdminPhoneNumber مقدار نمونهٔ ریپو ({SampleAdminPhone}) است. " +
                "هرکس با این شماره OTP بگیرد Admin می‌شود؛ شمارهٔ واقعیِ مدیر سامانه را بگذارید.");

        if (!IranianMobileRegex().IsMatch(phone))
            throw new InvalidOperationException(
                "Identity:FirstAdminPhoneNumber باید یک شمارهٔ موبایل ایران با قالب 09xxxxxxxxx باشد.");
    }

    /// <summary>
    /// فرستندهٔ پیامک واقعی: با Fake در Production هیچ کدی ارسال نمی‌شود ولی
    /// پاسخ کاربر «کد ارسال شد» می‌ماند — همان بن‌بستِ کپچای خالی، این‌بار خاموش.
    /// </summary>
    public static void ValidateSms(IConfiguration configuration)
    {
        if (!string.Equals(configuration["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Sms:Provider باید در Production روی «Kavenegar» تنظیم شود؛ با Fake هیچ پیامکی " +
                "ارسال نمی‌شود و ادمین اول نمی‌تواند وارد شود تا آن را از صفحهٔ تنظیمات اصلاح کند.");

        var apiKey = configuration["Sms:Kavenegar:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Sms:Kavenegar:ApiKey باید در Production تنظیم شود؛ بدون آن ارسال OTP شکست می‌خورد.");

        if (LooksLikeSample(apiKey))
            throw new InvalidOperationException(
                "Sms:Kavenegar:ApiKey یک مقدار نمونه است؛ کلید واقعیِ پنل Kavenegar را بگذارید.");
    }

    private static void RequireTurnstileKey(string? value, string keyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{keyName} باید در Production از env/appsettings موجود باشد؛ با کلید خالی " +
                "اعتبارسنج fail-closed است و هیچ ورودی (حتی ادمین اول) ممکن نیست.");

        if (LooksLikeSample(value))
            throw new InvalidOperationException(
                $"{keyName} یک مقدار نمونه است (مثل 0x4AAAAAAA_replace_me در .env.example). " +
                "کلید واقعی را از داشبورد Cloudflare Turnstile بردارید.");

        var trimmed = value.Trim();

        // کلیدهای واقعی Turnstile همیشه با «0x» شروع می‌شوند؛ این بررسی جلوی
        // جای‌گذاری تصادفیِ یک رشتهٔ دلخواه را می‌گیرد بدون آنکه کلید معتبر رد شود.
        if (trimmed.Length < MinTurnstileKeyLength ||
            !trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{keyName} باید کلید واقعی Turnstile باشد (با «0x» شروع می‌شود و دست‌کم " +
                $"{MinTurnstileKeyLength} کاراکتر دارد).");
    }
}
using System.Text.RegularExpressions;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Validators;

/// <summary>
/// اعتبارسنجی ورودی‌های احراز هویت در لایهٔ سرویس، نه فقط در فرم HTML.
/// چرا اینجا: فرم وب، API موبایل و circuit بلazor هر سه به همین سرویس‌ها می‌رسند و
/// اعتبارسنجی سمت کلاینت قابل دور زدن است. ستون‌های دیتابیس هم سقف طول دارند
/// (شماره nvarchar(32) و هش کد nvarchar(64))، پس ورودی بلند یعنی خطای ذخیره‌سازی
/// به‌جای پیام روشن؛ و ورودی بی‌قالب پیش از رسیدن به دیتابیس باید رد شود.
/// </summary>
public static partial class AuthInput
{
    /// <summary>سقف طول نام کاربری/ایمیل — هم‌اندازهٔ ستون‌های Identity.</summary>
    public const int MaxUserNameLength = 256;

    /// <summary>سقف طول رمز ورودی: هش‌کردن رمزِ مگابایتی CPU سرور را می‌سوزاند.</summary>
    public const int MaxPasswordLength = 1024;

    [GeneratedRegex("^09[0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex IranianMobileRegex();

    [GeneratedRegex("^[0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex OtpCodeRegex();

    /// <summary>
    /// شماره را نرمال می‌کند (ارقام فارسی/عربی به انگلیسی) و اگر قالب معتبر نبود null می‌دهد.
    /// نرمال‌سازی پیش از بررسی قالب است، وگرنه شمارهٔ درستِ فارسی‌تایپ‌شده رد می‌شد.
    /// </summary>
    public static string? NormalizeValidPhone(string? phone)
    {
        var normalized = PersianSearch.NormalizePhone(phone);
        return IranianMobileRegex().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>کد یک‌بارمصرف دقیقاً شش رقم انگلیسی است؛ فاصلهٔ ابتدا/انتها نادیده گرفته می‌شود.</summary>
    public static bool IsValidOtpCode(string? code) =>
        code is not null && OtpCodeRegex().IsMatch(code.Trim());

    /// <summary>آیا نام کاربری و رمز در سقف طول مجازند؟ ورودی بلند پیش از کوئری/هش رد می‌شود.</summary>
    public static bool IsWithinLengthBounds(string? userName, string? password) =>
        userName is not null && userName.Length <= MaxUserNameLength &&
        password is not null && password.Length <= MaxPasswordLength;
}
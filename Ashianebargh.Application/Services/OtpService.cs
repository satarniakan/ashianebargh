using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.Helpers;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Services;

public interface IOtpService
{
    Task GenerateAndSendOtpAsync(string phoneNumber);
    Task<bool> VerifyOtpAsync(string phoneNumber, string code);

    /// <summary>حذف کدهای منقضی. رکوردهای منقضی هیچ‌وقت خوانده نمی‌شوند (کوئری تأیید
    /// ExpiresAt آینده می‌خواهد) و فقط جدول را پر می‌کنند. بازگشت: تعداد حذف‌شده.</summary>
    Task<int> PurgeExpiredAsync(TimeSpan grace);
}

public class OtpService : IOtpService
{
    private readonly IOtpRepository _otpRepository;
    private readonly IOtpThrottleRepository _throttles;
    private readonly ISmsSender _smsSender;
    private readonly ISettingService _settings;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly ILogger<OtpService> _logger;
    private readonly TimeProvider _clock;

    public OtpService(IOtpRepository otpRepository, IOtpThrottleRepository throttles,
        ISmsSender smsSender, ISettingService settings, IAshianebarghUnitOfWork unitOfWork,
        ILogger<OtpService> logger, TimeProvider? clock = null)
    {
        _otpRepository = otpRepository;
        _throttles = throttles;
        _smsSender = smsSender;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _logger = logger;
        // تزریق‌پذیر تا انقضای پنجرهٔ قفل در تست قابل بررسی باشد
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// سقف تلاش ناموفق برای هر شماره در بازه. محدودیت نرخِ HTTP فقط بر اساس IP است،
    /// پس بدون این شمارنده، مهاجم با چرخش IP می‌تواند یک شماره را بمباران کند
    /// (هم برای حدس کد و هم برای باطل‌کردن کدهای قبلیِ صاحبش).
    /// </summary>
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// سقف درخواست تولید کد به‌ازای هر شماره. سقف HTTP («otp-request») IP-محور است،
    /// پس مهاجم با IPهای چرخان می‌توانست یک شمارهٔ هدف را بمباران پیامکی کند:
    /// هزینهٔ مستقیم برای صاحب خط و فلج‌شدن ورودش.
    /// </summary>
    private const int MaxGenerationsPerWindow = 3;

    private static readonly TimeSpan GenerationWindow = TimeSpan.FromMinutes(15);

    /// <summary>وضعیت یک پنجرهٔ شمارنده: تعداد رخداد و شروع پنجره (null یعنی پنجره‌ای باز نیست).</summary>
    private sealed record WindowState(int Count, DateTime? WindowStartUtc);

    /// <summary>
    /// خواندن وضعیت یک پنجره از دیتابیس. رکورد تروتل برای شماره‌ای که هنوز هیچ
    /// رخدادی نداشته ساخته نمی‌شود تا جدول فقط شماره‌های فعال را داشته باشد.
    /// </summary>
    private async Task<WindowState> GetWindowAsync(string phoneNumber, bool generation)
    {
        var throttle = await _throttles.GetByPhoneAsync(phoneNumber);
        if (throttle is null)
            return new WindowState(0, null);

        return generation
            ? new WindowState(throttle.GenerationCount, throttle.GenerationWindowStartUtc)
            : new WindowState(throttle.FailedCount, throttle.FailedWindowStartUtc);
    }

    /// <summary>
    /// آیا پنجره پر شده؟ پنجره «لغزان» نبود: با هر رخداد، شروعِ پنجره تمدید نمی‌شود،
    /// وگرنه یک تلاش هر ۹ دقیقه شماره را برای همیشه قفل می‌کرد.
    /// </summary>
    private static bool IsWindowExhausted(WindowState state, int maxCount, TimeSpan window, DateTime nowUtc)
    {
        if (state.WindowStartUtc is null)
            return false;

        // پنجره منقضی شده — مثل این است که پنجره‌ای نیست
        if (nowUtc - state.WindowStartUtc.Value >= window)
            return false;

        return state.Count >= maxCount;
    }

    private async Task<bool> IsBlockedAsync(string phoneNumber)
    {
        var state = await GetWindowAsync(phoneNumber, generation: false);
        return IsWindowExhausted(state, MaxFailedAttempts, AttemptWindow, _clock.GetUtcNow().UtcDateTime);
    }

    private Task RegisterFailedAttemptAsync(string phoneNumber) =>
        // افزایش در دیتابیس و اتمیک است: دو تلاش همزمان یکی دیگری را گم نمی‌کند
        _throttles.RegisterAttemptAsync(
            phoneNumber, generation: false, _clock.GetUtcNow().UtcDateTime, AttemptWindow);

    // ورود موفق ⇒ شمارندهٔ تلاش‌های ناموفق پاک می‌شود
    private Task ClearFailedAttemptsAsync(string phoneNumber) =>
        _throttles.ClearFailedAttemptsAsync(phoneNumber);

    public async Task GenerateAndSendOtpAsync(string phoneNumber)
    {
        phoneNumber = PersianSearch.NormalizePhone(phoneNumber);

        // شماره‌ای که چند بار پشت‌سرهم کد اشتباه داده، فعلاً ورودی جدید نمی‌گیرد
        // تا مهاجم نتواند با درخواست‌های مکرر، کدهای معتبر کاربر را باطل کند
        if (await IsBlockedAsync(phoneNumber))
        {
            _logger.LogWarning("OTP request blocked for {PhoneNumber} after repeated failures",
                PiiMask.Phone(phoneNumber));
            throw new BusinessRuleException(
                "تلاش‌های ناموفق زیاد بود. لطفاً ۱۰ دقیقه دیگر تلاش کنید.");
        }

        // شمارش پیش از ارسال و اتمیک: درخواست‌هایی که ارسالشان شکست می‌خورد هم شمرده
        // می‌شوند (وگرنه حلقهٔ «درخواست ← خطای پیامک ← درخواست» سقف را دور می‌زند) و
        // درخواست‌های همزمان نمی‌توانند شمارنده را از دست بدهند.
        var generation = await _throttles.RegisterAttemptAsync(
            phoneNumber, generation: true, _clock.GetUtcNow().UtcDateTime, GenerationWindow);
        if (generation.GenerationCount > MaxGenerationsPerWindow)
        {
            _logger.LogWarning("OTP generation limit reached for {PhoneNumber}",
                PiiMask.Phone(phoneNumber));
            throw new BusinessRuleException(
                "تعداد درخواست کد برای این شماره بیش از حد مجاز است. لطفاً ۱۵ دقیقه دیگر تلاش کنید.");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otp = new OtpCode
        {
            PhoneNumber = phoneNumber,
            // فقط هش کد ذخیره می‌شود نه خود کد — دسترسی مستقیم به دیتابیس دیگر امکان ورود نمی‌دهد
            Code = await HashOtpAsync(phoneNumber, code),
            ExpiresAt = _clock.GetUtcNow().UtcDateTime.AddMinutes(5),
            IsUsed = false
        };

        await _otpRepository.AddAsync(otp);
        await _unitOfWork.CompleteAsync();

        // متن پیامک از تنظیمات می‌آید ({Code} جای کد می‌نشیند) تا بدون deploy عوض شود
        var template = await _settings.GetAsync(FrameworkSettingKeys.OtpSmsTemplate);
        var body = template.Contains("{Code}", StringComparison.Ordinal)
            ? template.Replace("{Code}", code, StringComparison.Ordinal)
            : $"{template} {code}";

        // پیامک پیش از باطل‌کردن کدهای قبلی ارسال می‌شود: اگر سرویس پیامک شکست بخورد،
        // کد قبلی هنوز معتبر است و کاربر نه با کدی بی‌اعتبار و نه بدون کد می‌ماند
        await _smsSender.SendAsync(phoneNumber, body);

        // فقط پس از ارسال موفق — تا آخرین کد ارسال‌شده قابل استفاده بماند
        await _otpRepository.InvalidateOthersAsync(
            phoneNumber, otp.Id, _clock.GetUtcNow().UtcDateTime);
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("OTP generated for {PhoneNumber}", PiiMask.Phone(phoneNumber));
    }

    public async Task<bool> VerifyOtpAsync(string phoneNumber, string code)
    {
        // نرمال‌سازی اینجا (نه فقط در فراخواننده): هش هنگام ساخت با شمارهٔ نرمال‌شده
        // ساخته شده و مسیر API هم بدون آن کد معتبر را رد می‌کرد.
        phoneNumber = PersianSearch.NormalizePhone(phoneNumber);

        if (await IsBlockedAsync(phoneNumber))
        {
            _logger.LogWarning("OTP verification blocked for {PhoneNumber} after repeated failures",
                PiiMask.Phone(phoneNumber));
            return false;
        }

        // همان هشِ لحظهٔ ساخت اعمال می‌شود تا بدون ذخیرهٔ کد خام در دیتابیس، کد پیدا شود.
        // رکوردهای قدیمیِ «بدون pepper» هم پذیرفته می‌شوند تا چرخش pepper ورود جاری را
        // نشکند — ولی فقط وقتی pepper واقعاً تنظیم است (وگرنه همان کوئری اول کافی است و
        // نباید به‌ازای هر تلاش ناموفق دو کوئری به دیتابیس بزنیم).
        var now = _clock.GetUtcNow().UtcDateTime;
        var peppered = await HashOtpAsync(phoneNumber, code);
        var otp = await _otpRepository.GetLatestValidAsync(phoneNumber, peppered, now);
        if (otp is null && !string.Equals(peppered, HashOtp(phoneNumber, code, pepper: null), StringComparison.Ordinal))
            otp = await _otpRepository.GetLatestValidAsync(phoneNumber, HashOtp(phoneNumber, code, pepper: null), now);

        if (otp is null)
        {
            await RegisterFailedAttemptAsync(phoneNumber);
            _logger.LogWarning("Invalid or expired OTP attempt for {PhoneNumber}",
                PiiMask.Phone(phoneNumber));
            return false;
        }

        // مصرف اتمیک: اگر درخواست موازی دیگری قبلاً همین کد را مصرف کرده باشد، false می‌شود
        if (!await _otpRepository.TryMarkAsUsedAsync(otp.Id))
        {
            _logger.LogWarning("OTP already consumed by a concurrent request for {PhoneNumber}",
                PiiMask.Phone(phoneNumber));
            return false;
        }

        await ClearFailedAttemptsAsync(phoneNumber);
        return true;
    }

    public async Task<int> PurgeExpiredAsync(TimeSpan grace)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime - grace;
        var deletedCodes = await _otpRepository.DeleteExpiredAsync(cutoff);

        // رکوردهای تروتلِ شماره‌هایی که مدت‌هاست فعالیتی نداشته‌اند هم پاک می‌شوند تا جدول رشد نکند
        var deletedThrottles = await _throttles.DeleteStaleAsync(_clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(1));
        return deletedCodes + deletedThrottles;
    }

    /// <summary>
    /// هش کد یک‌بارمصرف — کد خام هرگز در دیتابیس ذخیره نمی‌شود.
    /// شمارهٔ موبایل در هش لحاظ می‌شود تا هش یک کد رایج (مثل ۱۲۳۴۵۶) برای همه یکسان نباشد؛
    /// یادآوری: فضای کد ۶ رقمی است، پس محافظت اصلی در برابر حدس، محدودیت نرخ درخواست‌هاست.
    /// pepper سمت سرور (Security:Pepper) هم لحاظ می‌شود تا لو رفتن دیتابیس به‌تنهایی
    /// برای brute-force کافی نباشد؛ خالی یعنی رفتار قدیمی (سازگاری).
    /// </summary>
    private async Task<string> HashOtpAsync(string phoneNumber, string code)
    {
        var pepper = await _settings.GetEffectiveAsync(
            IntegrationSettingKeys.SecurityPepper, "Security:Pepper");
        return HashOtp(phoneNumber, code, string.IsNullOrWhiteSpace(pepper) ? null : pepper);
    }

    internal static string HashOtp(string phoneNumber, string code, string? pepper = null)
    {
        // پیش‌فرض null یعنی «بدون pepper» تا فراخوانی قدیمیِ دوآرگومانی (تست‌ها)
        // دقیقاً رفتار قبلی را بدهد؛ رشتهٔ خالی هم مثل null (بدون pepper) است.
        var input = string.IsNullOrEmpty(pepper)
            ? $"{phoneNumber}:{code}"
            : $"{phoneNumber}:{code}:{pepper}";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));
    }
}

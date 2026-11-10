using Ashianebargh.Domain.Entities;

namespace Ashianebargh.Domain.Interfaces;

public interface IOtpThrottleRepository
{
    Task<OtpThrottle?> GetByPhoneAsync(string phoneNumber);

    Task AddAsync(OtpThrottle throttle);

    /// <summary>
    /// ثبت اتمیک یک رخداد در پنجرهٔ شمارنده (تولید کد یا تلاش ناموفق)، با ایجاد
    /// رکورد در صورت نبود. «خواندن ← افزایش ← نوشتن» با UPDATE شرطی انجام می‌شود تا
    /// دو درخواست همزمان افزایش یکدیگر را گم نکنند (سقف تلاش دور زده می‌شد).
    /// پنجرهٔ منقضی از نو شروع می‌شود. بازگشت: وضعیت بعد از افزایش.
    /// </summary>
    Task<OtpThrottle> RegisterAttemptAsync(string phoneNumber, bool generation, DateTime nowUtc, TimeSpan window);

    /// <summary>صفرکردن اتمیک شمارندهٔ تلاش ناموفق (پس از ورود موفق).</summary>
    Task ClearFailedAttemptsAsync(string phoneNumber);

    /// <summary>
    /// حذف رکوردهای کهنه‌ای که هر دو پنجره‌شان قدیمی‌تر از cutoff است (یا هرگز باز نشده).
    /// جدول فقط به‌ازای شماره‌های فعال رشد می‌کند. بازگشت: تعداد حذف‌شده.
    /// </summary>
    Task<int> DeleteStaleAsync(DateTime cutoffUtc);
}

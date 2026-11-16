using Ashianebargh.Domain.Entities;

namespace Ashianebargh.Domain.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(OtpCode otp);

    /// <summary>
    /// آخرین کد معتبرِ همان شماره و همان هدف. فیلتر هدف اینجا هم اعمال می‌شود
    /// (علاوه بر هش) تا حتی اگر هشِ دو مسیر یکی می‌شد، کدِ ورود برای تعیین رمز
    /// پیدا نشود.
    /// </summary>
    Task<OtpCode?> GetLatestValidAsync(string phoneNumber, string code, OtpPurpose purpose, DateTime nowUtc);

    Task MarkAsUsedAsync(int id);

    /// <summary>مصرف اتمیک و یک‌بارمصرف: فقط اگر IsUsed=false باشد علامت می‌خورد —
    /// دو درخواست موازی با یک کد هر دو موفق نمی‌شوند</summary>
    Task<bool> TryMarkAsUsedAsync(int id);

    /// <summary>باطل‌کردن کدهای معتبرِ دیگرِ همان شماره و همان هدف به‌جز کدِ keepId —
    /// فقط آخرین کد ارسال‌شده معتبر بماند. هدف جداگانه فیلتر می‌شود تا درخواست
    /// کدِ تعیین رمز، کدِ ورودِ در جریان کاربر را باطل نکند.</summary>
    Task InvalidateOthersAsync(string phoneNumber, OtpPurpose purpose, int keepId, DateTime nowUtc);

    /// <summary>حذف رکوردهایی که تا cutoff منقضی شده‌اند. بازگشت: تعداد حذف‌شده.</summary>
    Task<int> DeleteExpiredAsync(DateTime cutoffUtc);
}
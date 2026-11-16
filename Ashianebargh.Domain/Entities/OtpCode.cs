namespace Ashianebargh.Domain.Entities;

public class OtpCode
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// هدف صدور کد. در هش و در کوئری تأیید لحاظ می‌شود تا کدِ ورود برای
    /// تعیین رمز (یا برعکس) پذیرفته نشود. پیش‌فرض Login برای رکوردهای قدیمی
    /// است که پیش از افزودن این ستون ساخته شده‌اند.
    /// </summary>
    public OtpPurpose Purpose { get; set; } = OtpPurpose.Login;

    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
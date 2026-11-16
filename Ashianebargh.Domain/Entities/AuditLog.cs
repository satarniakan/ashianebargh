public class AuditLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string Details { get; set; } = string.Empty;

    // زمانِ مرجع را AuditService از TimeProvider می‌دهد تا در تست قابل کنترل باشد؛
    // این پیش‌فرض فقط برای ساختِ مستقیم/تست است، نه مسیر تولید.
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // شناسهٔ پایدارِ انجام‌دهنده (ApplicationUser.Id). برخلاف UserEmail که از
    // User.Identity?.Name (= شمارهٔ تلفن) پر می‌شود و PII است، این فیلد غیرشخصی است
    // و حتی پس از تغییر شماره/ایمیل، انتسابِ رویداد به کاربر را حفظ می‌کند.
    public string? ActorId { get; set; }

    // آدرس IP درخواستِ ثبت‌کننده (وقتی HttpContext در دسترس است).
    public string? IpAddress { get; set; }

    public AuditLog() { }

    public AuditLog(string eventType, string? userEmail, string details,
        DateTime? occurredAt = null, string? actorId = null, string? ipAddress = null)
    {
        EventType = eventType;
        UserEmail = userEmail;
        Details = details;
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        ActorId = actorId;
        IpAddress = ipAddress;
    }
}
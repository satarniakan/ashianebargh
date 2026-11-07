using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>اطلاعات تماس سایت؛ فقط یک رکورد نگه‌داری می‌شود.</summary>
public class ContactInfo : AuditableEntity
{
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? WorkingHours { get; set; }
    public string? MapUrl { get; set; }
    public string? Instagram { get; set; }
    public string? Telegram { get; set; }
    public string? WhatsApp { get; set; }
}

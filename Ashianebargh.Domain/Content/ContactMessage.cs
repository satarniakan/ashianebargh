using Ashianebargh.Domain.Common;

namespace Ashianebargh.Domain.Content;

/// <summary>پیام ارسالی از فرم تماس عمومی.</summary>
public class ContactMessage : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

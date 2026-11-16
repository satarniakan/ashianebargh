using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Queries;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Content;

public interface IContactService
{
    Task<ContactInfoDto> GetInfoAsync();
    Task SaveInfoAsync(ContactInfoDto dto);
    Task SubmitAsync(ContactFormDto dto, string? captchaToken);
    Task<PagedResult<ContactMessageDto>> GetMessagesAsync(int page, int pageSize = 20);
    Task SetReadAsync(int id, bool isRead);
    Task DeleteMessageAsync(int id);
    Task<int> GetUnreadCountAsync();
}

public sealed class ContactService(IContentUnitOfWork uow, IMemoryCache cache, ICaptchaValidator captcha) : IContactService
{
    // فرم تماس عمومی و بدون ورود است؛ بدون سقف، یک اسکریپت می‌تواند جدول پیام‌ها را
    // پر کند (DoS/هرزنامه). سقف سراسری جلوی حجم را می‌گیرد و سقف هر شماره جلوی تکرار.
    private const int GlobalWindowLimit = 100;
    private const int PerPhoneWindowLimit = 3;
    private static readonly TimeSpan SubmitWindow = TimeSpan.FromMinutes(10);
    private static readonly object ThrottleGate = new();
    public async Task<ContactInfoDto> GetInfoAsync()
    {
        var info = await uow.Content.ContactInfos.AsNoTracking()
            .OrderBy(i => i.Id)
            .Select(i => new ContactInfoDto
            {
                Address = i.Address,
                Phone = i.Phone,
                Mobile = i.Mobile,
                Email = i.Email,
                WorkingHours = i.WorkingHours,
                MapUrl = i.MapUrl,
                Instagram = i.Instagram,
                Telegram = i.Telegram,
                WhatsApp = i.WhatsApp
            })
            .FirstOrDefaultAsync();

        return info ?? new ContactInfoDto();
    }

    public async Task SaveInfoAsync(ContactInfoDto dto)
    {
        var entity = await uow.Content.ContactInfos.OrderBy(i => i.Id).FirstOrDefaultAsync();
        if (entity is null)
        {
            entity = new ContactInfo();
            uow.Content.Add(entity);
        }

        entity.Address = Clean(dto.Address);
        entity.Phone = CleanPhone(dto.Phone);
        entity.Mobile = CleanPhone(dto.Mobile);
        entity.Email = Clean(dto.Email);
        entity.WorkingHours = Clean(dto.WorkingHours);
        entity.MapUrl = Clean(dto.MapUrl);
        entity.Instagram = Clean(dto.Instagram);
        entity.Telegram = Clean(dto.Telegram);
        entity.WhatsApp = Clean(dto.WhatsApp);

        await uow.CompleteAsync();
    }

    public async Task SubmitAsync(ContactFormDto dto, string? captchaToken)
    {
        // کپچا پیش از هر کاری: جلوی ارسال خودکار/انبوه را می‌گیرد. در توسعه که کلید
        // Turnstile تنظیم نیست، اعتبارسنج غیرفعال همیشه موفق برمی‌گرداند.
        if (!await captcha.ValidateAsync(captchaToken, remoteIp: null))
            throw new BusinessRuleException("اعتبارسنجی امنیتی ناموفق بود؛ دوباره تلاش کنید.");

        var name = (dto.Name ?? string.Empty).Trim();
        var phone = PersianSearch.NormalizePhone(dto.Phone);
        var message = (dto.Message ?? string.Empty).Trim();

        if (name.Length == 0)
            throw new BusinessRuleException("نام را وارد کنید.");
        if (phone.Length == 0)
            throw new BusinessRuleException("شماره تماس را وارد کنید.");
        if (message.Length == 0)
            throw new BusinessRuleException("متن پیام را وارد کنید.");
        if (name.Length > 100 || phone.Length > 30 || message.Length > 2000)
            throw new BusinessRuleException("طول یکی از فیلدهای پیام بیش از حد مجاز است.");

        // سقف سراسری (ضد DoS) و سقف هر شماره (ضد هرزنامهٔ تکراری).
        if (!TryConsumeQuota("contact:global", GlobalWindowLimit) ||
            !TryConsumeQuota($"contact:phone:{phone}", PerPhoneWindowLimit))
            throw new BusinessRuleException("تعداد پیام‌های ارسالی بیش از حد مجاز است؛ لطفاً کمی بعد دوباره تلاش کنید.");

        uow.Content.Add(new ContactMessage
        {
            Name = name,
            Phone = phone,
            Email = Clean(dto.Email),
            Subject = Clean(dto.Subject),
            Message = message
        });

        await uow.CompleteAsync();
    }

    /// <summary>سقف پنجره‌ای اتمیک؛ مثل سقفِ نوشتن رمز در AuthService.</summary>
    private bool TryConsumeQuota(string key, int limit)
    {
        lock (ThrottleGate)
        {
            var count = cache.TryGetValue(key, out int current) ? current : 0;
            if (count >= limit) return false;
            cache.Set(key, count + 1, SubmitWindow);
            return true;
        }
    }

    public async Task<PagedResult<ContactMessageDto>> GetMessagesAsync(int page, int pageSize = 20)
    {
        var projected = uow.Content.ContactMessages.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new ContactMessageDto(
                m.Id, m.Name, m.Phone, m.Email, m.Subject, m.Message, m.IsRead, m.CreatedAt));

        return await projected.ToPagedAsync(page, pageSize);
    }

    public async Task SetReadAsync(int id, bool isRead)
    {
        var entity = await uow.Content.ContactMessages.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException("پیام", id);

        entity.IsRead = isRead;
        await uow.CompleteAsync();
    }

    public async Task DeleteMessageAsync(int id)
    {
        var entity = await uow.Content.ContactMessages.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException("پیام", id);

        uow.Content.Remove(entity);
        await uow.CompleteAsync();
    }

    public Task<int> GetUnreadCountAsync() =>
        uow.Content.ContactMessages.CountAsync(m => !m.IsRead);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanPhone(string? value)
    {
        var normalized = PersianSearch.NormalizePhone(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

using Microsoft.EntityFrameworkCore;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Queries;
using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Content;

public interface IContactService
{
    Task<ContactInfoDto> GetInfoAsync();
    Task SaveInfoAsync(ContactInfoDto dto);
    Task SubmitAsync(ContactFormDto dto);
    Task<PagedResult<ContactMessageDto>> GetMessagesAsync(int page, int pageSize = 20);
    Task SetReadAsync(int id, bool isRead);
    Task DeleteMessageAsync(int id);
    Task<int> GetUnreadCountAsync();
}

public sealed class ContactService(IContentUnitOfWork uow) : IContactService
{
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

    public async Task SubmitAsync(ContactFormDto dto)
    {
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

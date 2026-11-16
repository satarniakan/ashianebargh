using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Application.Services;

/// <summary>ثبت تاریخچهٔ ورودها (موفق و ناموفق) با IP و عامل کاربر.</summary>
public interface ILoginHistoryService
{
    Task RecordAsync(string? userId, string? userName, bool succeeded, string method,
        string? ipAddress, string? userAgent, string? failureReason = null);
}

public class LoginHistoryService : ILoginHistoryService
{
    private readonly ILoginHistoryRepository _repository;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public LoginHistoryService(ILoginHistoryRepository repository, IAshianebarghUnitOfWork unitOfWork,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task RecordAsync(string? userId, string? userName, bool succeeded, string method,
        string? ipAddress, string? userAgent, string? failureReason = null)
    {
        // همهٔ این ستون‌ها سقف طول دارند و مقدارشان از ورودیِ کنترل‌نشدهٔ مهاجم می‌آید
        // (نام کاربریِ فرم/API و هدر User-Agent). بدون برش، یک درخواستِ بلند به‌جای
        // «ورود ناموفق» خطای ذخیره‌سازی و پاسخ ۵۰۰ می‌داد — یعنی ثبت تاریخچهٔ ورود
        // خودش به یک بردار DoS تبدیل می‌شد.
        await _repository.AddAsync(new LoginHistory
        {
            UserId = Truncate(userId, 450),
            UserName = Truncate(userName, 256),
            Succeeded = succeeded,
            Method = Truncate(method, 32) ?? string.Empty,
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
            FailureReason = Truncate(failureReason, 256),
            OccurredAtUtc = _clock.GetUtcNow().UtcDateTime
        });
        await _unitOfWork.CompleteAsync();
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Queries;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Application.Services;

/// <summary>
/// ثبت رویدادهای سامانه. ساختار داده‌اش کاملاً عمومی است (چه کسی، چه وقت، چه کرد)؛
/// متن رویداد را فراخواننده می‌دهد، پس هیچ وابستگی به دامنه ندارد.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// ثبت یک رویداد. <paramref name="actorId"/> و <paramref name="ipAddress"/> اختیاری‌اند؛
    /// اگر داده نشوند از HttpContext جاری (کاربرِ واردشده و IP درخواست) پر می‌شوند.
    /// </summary>
    Task LogEventAsync(string eventType, string? userEmail, string details,
        string? actorId = null, string? ipAddress = null);
    Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100);
    Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly IHttpContextAccessor? _httpContext;

    public AuditService(
        IAuditLogRepository repository,
        IAshianebarghUnitOfWork unitOfWork,
        TimeProvider? clock = null,
        IHttpContextAccessor? httpContext = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock ?? TimeProvider.System;
        _httpContext = httpContext;
    }

    public async Task LogEventAsync(string eventType, string? userEmail, string details,
        string? actorId = null, string? ipAddress = null)
    {
        var http = _httpContext?.HttpContext;
        // شناسهٔ پایدارِ کاربر و IP از درخواست جاری؛ فراخواننده می‌تواند صریح override کند
        // (مثلاً رویدادِ جانشینی که actor واقعی ادمین است، نه کاربرِ جانشین‌شده).
        actorId ??= http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        ipAddress ??= http?.Connection.RemoteIpAddress?.ToString();

        await _repository.AddAsync(new AuditLog(eventType, userEmail, details,
            _clock.GetUtcNow().UtcDateTime, actorId, ipAddress));

        // عمداً مستقل commit می‌شود: audit باید حتی وقتی تراکنش کسب‌وکار rollback می‌شود
        // (مثلاً تلاشِ ناموفقِ ورود/تغییر رمز) باقی بماند؛ اتمیک‌کردن آن با عملیات،
        // رویدادهای امنیتیِ شکست‌خورده را از بین می‌برد.
        await _unitOfWork.CompleteAsync();
    }

    public Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100) =>
        _repository.GetRecentAsync(count);

    public async Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        // سقف مشترک با ToPagedAsync: ?pageSize=10000000 نباید کل جدول را به حافظه بکشد
        if (pageSize > PagedQueryExtensions.MaxPageSize) pageSize = PagedQueryExtensions.MaxPageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, search);

        return new PagedResult<AuditLog>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
using Ashianebargh.Domain.Entities;

namespace Ashianebargh.Domain.Interfaces;

public interface IPushSubscriptionRepository
{
    Task<List<PushSubscription>> GetForUserAsync(string userId);

    Task<PushSubscription?> GetByEndpointAsync(string userId, string endpoint);

    /// <summary>
    /// جست‌وجوی سراسری بر اساس Endpoint (بدون فیلتر کاربر): چون Endpoint در کل
    /// جدول یکتاست، برای upsert و انتقال مالکیت لازم است.
    /// </summary>
    Task<PushSubscription?> GetByEndpointGlobalAsync(string endpoint);

    Task AddAsync(PushSubscription subscription);

    Task RemoveAsync(PushSubscription subscription);

    Task<int> RemoveByEndpointAsync(string endpoint);
}

/// <summary>
/// ارسال سطح‌پایین وب‌پوش به یک اشتراک. خطای «اشتراک منقضی» با استثنای
/// <see cref="PushSubscriptionExpiredException"/> گزارش می‌شود تا فراخواننده پاکش کند.
/// </summary>
public interface IPushSender
{
    Task SendAsync(PushSubscription subscription, string title, string? body, string? url,
        CancellationToken cancellationToken = default);
}

/// <summary>اشتراک مرورگر دیگر معتبر نیست (410 Gone / 404) — رکوردش باید حذف شود.</summary>
public sealed class PushSubscriptionExpiredException(string endpoint)
    : Exception("Push subscription expired.")
{
    // پیام عمداً بدون endpoint است؛ خودِ endpoint یک capability URL یکتاست و اگر
    // پیام خطا جایی لاگ/نمایش داده شود نباید شناسهٔ دستگاه کاربر درز کند.
    public string Endpoint { get; } = endpoint;
}

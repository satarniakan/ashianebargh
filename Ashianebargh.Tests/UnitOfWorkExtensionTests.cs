using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;
using Ashianebargh.Infrastructure.Repositories;

namespace Ashianebargh.Tests;

/// <summary>
/// نقطهٔ اتصال سوم (واحد کار دامنه): پروژهٔ مصرف‌کننده از
/// <see cref="AshianebarghUnitOfWork"/> ارث می‌برد و ریپوی خودش را با همان DbContext
/// اضافه می‌کند — بدون ویرایش پایه، با یک تراکنش مشترک.
/// </summary>
public class UnitOfWorkExtensionTests
{
    /// <summary>ریپوی دامنهٔ نمونه: فقط برای اثبات seam.</summary>
    private sealed class NoteRepository(AshianebarghDbContext context)
    {
        public Task AddAsync(Setting note) => context.Settings.AddAsync(note).AsTask();
        public Task<int> CountAsync() => context.Settings.CountAsync();
    }

    private interface ITestUnitOfWork : IAshianebarghUnitOfWork
    {
        NoteRepository Notes { get; }
    }

    private sealed class TestUnitOfWork : AshianebarghUnitOfWork, ITestUnitOfWork
    {
        public TestUnitOfWork(AshianebarghDbContext context, IServiceProvider services)
            : base(context, services)
        {
            Notes = new NoteRepository(context);
        }

        public NoteRepository Notes { get; }
    }

    [Fact]
    public async Task DerivedUnitOfWork_SharesOneContext_AndCommitsTogether()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"uow-{Guid.NewGuid()}")
            .Options;

        await using var context = new AshianebarghDbContext(options, Array.Empty<IAshianebarghModule>());

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton(Mock.Of<IOtpRepository>());
        services.AddSingleton(Mock.Of<IAuditLogRepository>());
        services.AddSingleton(Mock.Of<INotificationRepository>());
        services.AddSingleton(Mock.Of<IOutboxRepository>());
        await using var provider = services.BuildServiceProvider();

        ITestUnitOfWork uow = new TestUnitOfWork(context, provider);

        await uow.Notes.AddAsync(new Setting { Key = "a", Value = "1", UpdatedAtUtc = DateTime.UtcNow });
        await uow.AuditLogs.AddAsync(new AuditLog("test", null, "جزئیات"));
        await uow.CompleteAsync();

        Assert.Equal(1, await uow.Notes.CountAsync());
    }
}

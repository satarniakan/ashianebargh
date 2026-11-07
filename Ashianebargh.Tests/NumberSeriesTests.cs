using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;
using Ashianebargh.Infrastructure.Repositories;

namespace Ashianebargh.Tests;

/// <summary>
/// شمارهٔ ترتیبی: از ۱ شروع، پشت‌سرهم، مستقل برای هر نام، و قالب‌بندی با پیشوند/صفر.
/// (مسابقهٔ واقعی همزمانی روی InMemory قابل شبیه‌سازی نیست؛ حلقهٔ retry روی SQL Server است.)
/// </summary>
public class NumberSeriesTests
{
    private static (NumberSeries series, AshianebarghDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<AshianebarghDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AshianebarghModelCacheKeyFactory>()
            .UseInMemoryDatabase($"numbers-{Guid.NewGuid()}")
            .Options;

        var db = new AshianebarghDbContext(options, Array.Empty<IAshianebarghModule>());

        var uow = new Mock<IAshianebarghUnitOfWork>();
        uow.Setup(u => u.CompleteAsync())
            .Callback(() => db.SaveChanges())
            .ReturnsAsync(1);

        return (new NumberSeries(new NumberSequenceRepository(db), uow.Object), db);
    }

    [Fact]
    public async Task NextAsync_StartsAtOne_ThenIncrements()
    {
        var (series, _) = Build();

        Assert.Equal(1, await series.NextAsync("invoice"));
        Assert.Equal(2, await series.NextAsync("invoice"));
        Assert.Equal(3, await series.NextAsync("invoice"));
    }

    [Fact]
    public async Task NextAsync_DifferentNames_AreIndependent()
    {
        var (series, _) = Build();

        Assert.Equal(1, await series.NextAsync("invoice"));
        Assert.Equal(1, await series.NextAsync("contract"));
        Assert.Equal(2, await series.NextAsync("invoice"));
    }

    [Fact]
    public async Task NextFormattedAsync_PadsWithZeros()
    {
        var (series, _) = Build();

        Assert.Equal("1405-000001", await series.NextFormattedAsync("invoice-1405", "1405-"));
        Assert.Equal("1405-000002", await series.NextFormattedAsync("invoice-1405", "1405-"));
    }

    [Fact]
    public async Task NextAsync_BlankName_Throws()
    {
        var (series, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => series.NextAsync("  "));
    }
}

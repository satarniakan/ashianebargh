using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Tests;

/// <summary>
/// audit: زمان از TimeProvider می‌آید (قابل تست)، شناسهٔ پایدارِ کاربر و IP از
/// HttpContext جاری پر می‌شوند، و فراخواننده می‌تواند آن‌ها را صریح override کند.
/// </summary>
public class AuditServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed class Harness
    {
        public AuditLog? Captured { get; private set; }
        public Mock<IAuditLogRepository> Repo { get; } = new();
        public Mock<IAshianebarghUnitOfWork> Uow { get; } = new();
        public Mock<IHttpContextAccessor> Http { get; } = new();

        public Harness()
        {
            Repo.Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
                .Callback<AuditLog>(e => Captured = e)
                .Returns(Task.CompletedTask);
            Uow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        }

        public void SetHttpContext(string? actorId, string? ip)
        {
            var ctx = new DefaultHttpContext();
            if (actorId is not null)
            {
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, actorId) }, "test"));
            }
            if (ip is not null)
                ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            Http.Setup(h => h.HttpContext).Returns(ctx);
        }

        public AuditService Build() => new(Repo.Object, Uow.Object, new FixedTimeProvider(), Http.Object);
    }

    [Fact]
    public async Task LogEvent_UsesTimeProvider_ForOccurredAt()
    {
        var h = new Harness();

        await h.Build().LogEventAsync("test.event", null, "جزئیات");

        Assert.NotNull(h.Captured);
        Assert.Equal(Now, h.Captured!.OccurredAt);
    }

    [Fact]
    public async Task LogEvent_CapturesActorIdAndIp_FromHttpContext()
    {
        var h = new Harness();
        h.SetHttpContext("actor-1", "1.2.3.4");

        await h.Build().LogEventAsync("test.event", null, "جزئیات");

        Assert.Equal("actor-1", h.Captured!.ActorId);
        Assert.Equal("1.2.3.4", h.Captured.IpAddress);
    }

    [Fact]
    public async Task LogEvent_ExplicitActorAndIp_OverrideHttpContext()
    {
        var h = new Harness();
        h.SetHttpContext("actor-1", "1.2.3.4");

        // رویداد جانشینی: actor واقعی ادمین است، نه کاربرِ نشستِ جاری.
        await h.Build().LogEventAsync("impersonation-end", null, "جزئیات",
            actorId: "admin-9", ipAddress: "9.9.9.9");

        Assert.Equal("admin-9", h.Captured!.ActorId);
        Assert.Equal("9.9.9.9", h.Captured.IpAddress);
    }

    [Fact]
    public async Task LogEvent_WithoutHttpContext_LeavesActorAndIpNull()
    {
        var h = new Harness();
        h.Http.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        await h.Build().LogEventAsync("background.event", null, "جزئیات");

        Assert.Null(h.Captured!.ActorId);
        Assert.Null(h.Captured.IpAddress);
        Assert.Equal(Now, h.Captured.OccurredAt);
    }

    [Fact]
    public async Task LogEvent_PersistsFields_AndCommitsOnce()
    {
        var h = new Harness();

        await h.Build().LogEventAsync("user.created", "09120000000", "کاربر ساخته شد.");

        Assert.Equal("user.created", h.Captured!.EventType);
        Assert.Equal("09120000000", h.Captured.UserEmail);
        Assert.Equal("کاربر ساخته شد.", h.Captured.Details);
        h.Uow.Verify(u => u.CompleteAsync(), Times.Once);
    }
}
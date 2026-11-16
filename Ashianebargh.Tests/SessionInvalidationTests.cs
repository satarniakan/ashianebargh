using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Ashianebargh.Application;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون «عزل دسترسی باید نشستِ زنده را بگیرد». نقش‌ها و مجوزها هنگام ورود داخل کوکی
/// احراز هویت پخته می‌شوند (AppUserClaimsPrincipalFactory)، پس هر تغییرشان باید security
/// stamp را عوض کند؛ وگرنه کاربرِ عزل‌شده تا لاگین بعدی همان دسترسی قبلی را نگه می‌دارد.
/// </summary>
public class SessionInvalidationTests
{
    private static Mock<UserManager<ApplicationUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var users = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        return users;
    }

    private static Mock<RoleManager<IdentityRole>> CreateRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(store.Object, null!, null!, null!, null!);
    }

    private static Mock<IAshianebarghUnitOfWork> CreateUnitOfWork()
    {
        var uow = new Mock<IAshianebarghUnitOfWork>();
        // تراکنش در تست همان اجرای مستقیم عملیات است
        uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());
        return uow;
    }

    private static ApplicationUser User(string id = "u1", string phone = "09121111111") => new()
    {
        Id = id,
        UserName = phone,
        PhoneNumber = phone
    };

    private static UserAdminService BuildAdminService(
        Mock<UserManager<ApplicationUser>> users,
        Mock<RoleManager<IdentityRole>> roles,
        Mock<IAuditService> audit,
        Mock<IAshianebarghUnitOfWork> uow) =>
        new(users.Object, roles.Object, audit.Object, Mock.Of<IHttpContextAccessor>(), uow.Object);

    /// <summary>کاربری که تنها ادمین نیست تا گارد «آخرین ادمین» جلوی عزل را نگیرد.</summary>
    private static void SetupDemotableAdmin(
        Mock<UserManager<ApplicationUser>> users,
        Mock<RoleManager<IdentityRole>> roles,
        ApplicationUser user)
    {
        users.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);
        users.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { Roles.Admin });
        users.Setup(u => u.GetUsersInRoleAsync(Roles.Admin))
            .ReturnsAsync(new List<ApplicationUser> { user, User("u2", "09122222222") });
        roles.Setup(r => r.RoleExistsAsync(Roles.User)).ReturnsAsync(true);
        users.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(u => u.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    [Fact]
    public async Task SetRolesAsync_Demotion_InvalidatesLiveSession()
    {
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        var user = User();
        SetupDemotableAdmin(users, roles, user);

        var result = await BuildAdminService(users, roles, audit, uow)
            .SetRolesAsync(user.Id, [Roles.User]);

        Assert.True(result.Succeeded);
        users.Verify(u => u.UpdateSecurityStampAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetRolesAsync_StampFailure_ReportsFailure_WithoutAudit()
    {
        // اگر باطل‌کردن نشست شکست بخورد، نباید «موفق» گزارش شود و رد حسابرسی هم
        // نباید ادعای تغییری بکند که اثر امنیتی‌اش اعمال نشده است.
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        var user = User();
        SetupDemotableAdmin(users, roles, user);
        users.Setup(u => u.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "خطای دیتابیس" }));

        var result = await BuildAdminService(users, roles, audit, uow)
            .SetRolesAsync(user.Id, [Roles.User]);

        Assert.False(result.Succeeded);
        uow.Verify(u => u.ClearChangeTracker(), Times.Once);
        audit.Verify(a => a.LogEventAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_IdentityChange_InvalidatesLiveSession()
    {
        // شماره/UserName شناسهٔ ورود است و نام کاربری در کوکی نشست هم می‌نشیند.
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        var user = User();
        users.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);
        users.Setup(u => u.FindByNameAsync("09123333333")).ReturnsAsync((ApplicationUser?)null);
        users.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await BuildAdminService(users, roles, audit, uow).UpdateUserAsync(new UpdateUserDto
        {
            UserId = user.Id,
            FullName = "نام تازه",
            PhoneNumber = "09123333333"
        });

        Assert.True(result.Succeeded);
        users.Verify(u => u.UpdateSecurityStampAsync(user), Times.Once);
    }

    private static PermissionService BuildPermissionService(
        Mock<RoleManager<IdentityRole>> roles,
        Mock<UserManager<ApplicationUser>> users,
        Mock<IAuditService> audit,
        Mock<IAshianebarghUnitOfWork> uow)
    {
        var catalog = new Mock<IPermissionCatalog>();
        catalog.Setup(c => c.All).Returns(new List<PermissionDescriptor>
        {
            new("test.perm", "مجوز آزمایشی")
        });

        return new PermissionService(roles.Object, catalog.Object, uow.Object,
            users.Object, audit.Object, Mock.Of<IHttpContextAccessor>());
    }

    private static IdentityRole SetupEditableRole(
        Mock<RoleManager<IdentityRole>> roles, string roleName)
    {
        var role = new IdentityRole(roleName);
        roles.Setup(r => r.FindByNameAsync(roleName)).ReturnsAsync(role);
        roles.Setup(r => r.GetClaimsAsync(role)).ReturnsAsync(new List<Claim>());
        roles.Setup(r => r.AddClaimAsync(role, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
        roles.Setup(r => r.RemoveClaimAsync(role, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
        return role;
    }

    [Fact]
    public async Task SetPermissionsForRoleAsync_InvalidatesEveryMemberSession()
    {
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        SetupEditableRole(roles, Roles.User);
        users.Setup(u => u.GetUsersInRoleAsync(Roles.User))
            .ReturnsAsync(new List<ApplicationUser> { User("u1"), User("u2", "09122222222") });

        var result = await BuildPermissionService(roles, users, audit, uow)
            .SetPermissionsForRoleAsync(Roles.User, ["test.perm"]);

        Assert.True(result.Succeeded);
        users.Verify(u => u.UpdateSecurityStampAsync(It.Is<ApplicationUser>(x => x.Id == "u1")), Times.Once);
        users.Verify(u => u.UpdateSecurityStampAsync(It.Is<ApplicationUser>(x => x.Id == "u2")), Times.Once);
    }

    [Fact]
    public async Task SetPermissionsForRoleAsync_WritesAuditEntry()
    {
        // تغییر مجوز یک رویداد امنیتی است و قبلاً هیچ رد حسابرسی نداشت.
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        SetupEditableRole(roles, Roles.User);
        users.Setup(u => u.GetUsersInRoleAsync(Roles.User)).ReturnsAsync(new List<ApplicationUser>());

        var result = await BuildPermissionService(roles, users, audit, uow)
            .SetPermissionsForRoleAsync(Roles.User, ["test.perm"]);

        Assert.True(result.Succeeded);
        audit.Verify(a => a.LogEventAsync("role.permissions-changed", It.IsAny<string?>(),
            It.Is<string>(s => s.Contains(Roles.User))), Times.Once);
    }

    [Fact]
    public async Task SetPermissionsForRoleAsync_StampFailure_ReportsFailure_WithoutAudit()
    {
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var audit = new Mock<IAuditService>();
        var uow = CreateUnitOfWork();
        SetupEditableRole(roles, Roles.User);
        users.Setup(u => u.GetUsersInRoleAsync(Roles.User))
            .ReturnsAsync(new List<ApplicationUser> { User() });
        users.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "خطای دیتابیس" }));

        var result = await BuildPermissionService(roles, users, audit, uow)
            .SetPermissionsForRoleAsync(Roles.User, ["test.perm"]);

        Assert.False(result.Succeeded);
        uow.Verify(u => u.ClearChangeTracker(), Times.Once);
        audit.Verify(a => a.LogEventAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()),
            Times.Never);
    }

    private static ServiceProvider BuildProvider()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Server=(localdb)\\mssqllocaldb;Database=AshianebarghTest;Trusted_Connection=True;TrustServerCertificate=True",
            ["Sms:Provider"] = "Fake",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAshianebarghApplication();
        services.AddAshianebarghInfrastructure(configuration, isDevelopment: true);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void SecurityStamp_RevalidationInterval_IsShortEnoughToRevokeAccess()
    {
        // پیش‌فرض Identity سی دقیقه است؛ برای عزل دسترسی خیلی کند.
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(5), options.ValidationInterval);
    }

    [Fact]
    public void AuthCookie_HasAbsoluteLifetime_WithoutSlidingRenewal()
    {
        // تمدید لغزان یعنی کوکیِ دزدیده‌شده تا وقتی مهاجم فعال است زنده می‌ماند.
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.False(options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
    }
}
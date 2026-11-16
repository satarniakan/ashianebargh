using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Services;
using Ashianebargh.Application.Validators;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون سخت‌سازی مسیرهای ورود: افشای وجود حساب از راه زمان پاسخ، سقف نوشتن رمز
/// در مسیر circuit (که از محدودیت نرخ HTTP رد نمی‌شود)، و برش ورودیِ بلندِ تاریخچهٔ ورود.
/// </summary>
public class AuthHardeningTests
{
    private sealed class Harness
    {
        public Mock<UserManager<ApplicationUser>> Users { get; } = CreateUserManager();
        public Mock<IApiTokenService> Tokens { get; } = new();
        public Mock<ILoginHistoryService> History { get; } = new();
        public Mock<IPasswordHasher<ApplicationUser>> Hasher { get; } = new();
        public Mock<IAshianebarghUnitOfWork> Uow { get; } = CreateUnitOfWork();
        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

        /// <summary>بعد از اولین Build در دسترس است؛ برای تنظیم نتیجهٔ ورود روی آن Setup کنید.</summary>
        public Mock<SignInManager<ApplicationUser>> SignIn { get; private set; } = null!;

        public AuthService Build()
        {
            var signIn = new Mock<SignInManager<ApplicationUser>>(
                Users.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                null!, null!, null!, null!);
            SignIn = signIn;

            return new AuthService(
                Users.Object,
                signIn.Object,
                Mock.Of<IOtpService>(),
                Mock.Of<INotificationService>(),
                History.Object,
                Mock.Of<IHttpContextAccessor>(),
                Tokens.Object,
                Uow.Object,
                new ConfigurationBuilder().Build(),
                NullLogger<AuthService>.Instance,
                Hasher.Object,
                Cache);
        }

        private static Mock<IAshianebarghUnitOfWork> CreateUnitOfWork()
        {
            var uow = new Mock<IAshianebarghUnitOfWork>();
            uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());
            uow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
            return uow;
        }

        private static Mock<UserManager<ApplicationUser>> CreateUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }
    }

    private static ApplicationUser User(string id = "u1") => new()
    {
        Id = id,
        UserName = "09120000000",
        PhoneNumber = "09120000000"
    };

    [Fact]
    public async Task LoginWithPassword_UnknownUser_StillPaysPasswordHashCost()
    {
        // بدون این، مسیر «کاربر ناموجود» بدون هیچ هشی برمی‌گشت و اختلاف زمان پاسخ
        // به مهاجم می‌گفت کدام ایمیل/نام کاربری ثبت شده است.
        var h = new Harness();
        h.Users.Setup(u => u.FindByEmailAsync("ghost@test.ir")).ReturnsAsync((ApplicationUser?)null);
        h.Users.Setup(u => u.FindByNameAsync("ghost@test.ir")).ReturnsAsync((ApplicationUser?)null);

        var result = await h.Build().LoginWithPasswordAsync("ghost@test.ir", "whatever-1");

        Assert.False(result.Succeeded);
        h.Hasher.Verify(x => x.VerifyHashedPassword(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), "whatever-1"), Times.Once);
    }

    [Fact]
    public async Task LoginWithPassword_NonEmailIdentifier_IsResolvedByUserName()
    {
        // شناسهٔ ورود حق ندارد قالب ایمیلی داشته باشد (نام کاربری ساده، شمارهٔ موبایل، …)؛
        // اگر روزی بررسی ایمیل به مسیر ورود برگردد، این تست قرمز می‌شود.
        var h = new Harness();
        var user = new ApplicationUser { Id = "u9", UserName = "modir" };
        h.Users.Setup(u => u.FindByNameAsync("modir")).ReturnsAsync(user);
        var service = h.Build();
        h.SignIn.Setup(s => s.PasswordSignInAsync(user, "secret-1", true, true))
            .ReturnsAsync(SignInResult.Success);

        var result = await service.LoginWithPasswordAsync("modir", "secret-1");

        Assert.True(result.Succeeded);
        h.Users.Verify(u => u.FindByNameAsync("modir"), Times.Once);
    }

    [Fact]
    public async Task LoginWithPassword_OverlongInput_FailsWithoutQueryOrHash()
    {
        var h = new Harness();

        var result = await h.Build().LoginWithPasswordAsync(new string('a', 5000), "whatever-1");

        Assert.False(result.Succeeded);
        h.Users.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        h.Hasher.Verify(x => x.VerifyHashedPassword(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithPassword_OverlongPassword_FailsWithoutHash()
    {
        // رمزِ مگابایتی یعنی PBKDF2 روی ورودیِ مهاجم — یک بردار مصرف CPU.
        var h = new Harness();

        var result = await h.Build().LoginWithPasswordAsync("09120000000", new string('a', 100_000));

        Assert.False(result.Succeeded);
        h.Hasher.Verify(x => x.VerifyHashedPassword(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_PasswordWrites_AreThrottledPerUser()
    {
        // صفحهٔ پروفایل circuit بلazor است؛ policy «profile» روی HTTP اثر می‌کند نه
        // رویدادهای circuit، پس سقف باید داخل سرویس باشد.
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);
        h.Users.Setup(u => u.HasPasswordAsync(user)).ReturnsAsync(false);
        h.Users.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("tok");
        h.Users.Setup(u => u.ResetPasswordAsync(user, "tok", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var service = h.Build();

        for (var i = 0; i < 5; i++)
        {
            var ok = await service.UpdateProfileAsync(user.Id, null, null, "new-pass-1", "new-pass-1");
            Assert.Equal(ProfileUpdateStatus.Success, ok.Status);
        }

        var blocked = await service.UpdateProfileAsync(user.Id, null, null, "new-pass-1", "new-pass-1");

        Assert.Equal(ProfileUpdateStatus.TooManyPasswordAttempts, blocked.Status);
        // تلاش مسدود نباید به هش‌کردن برسد
        h.Users.Verify(u => u.ResetPasswordAsync(user, "tok", It.IsAny<string>()), Times.Exactly(5));
    }

    [Fact]
    public async Task UpdateProfile_QuotaIsPerUser_NotGlobal()
    {
        var h = new Harness();
        var first = User("u1");
        var second = User("u2");
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(first);
        h.Users.Setup(u => u.FindByIdAsync("u2")).ReturnsAsync(second);
        h.Users.Setup(u => u.HasPasswordAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(false);
        h.Users.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("tok");
        h.Users.Setup(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), "tok", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var service = h.Build();

        for (var i = 0; i < 5; i++)
            await service.UpdateProfileAsync("u1", null, null, "new-pass-1", "new-pass-1");

        var other = await service.UpdateProfileAsync("u2", null, null, "new-pass-1", "new-pass-1");

        Assert.Equal(ProfileUpdateStatus.Success, other.Status);
    }

    [Fact]
    public async Task UpdateProfile_MismatchedPassword_DoesNotConsumeQuota()
    {
        // خطای سمت کلاینت نباید سهمیهٔ کاربر را بسوزاند.
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);
        h.Users.Setup(u => u.HasPasswordAsync(user)).ReturnsAsync(false);
        var service = h.Build();

        for (var i = 0; i < 10; i++)
        {
            var mismatch = await service.UpdateProfileAsync(user.Id, null, null, "new-pass-1", "different");
            Assert.Equal(ProfileUpdateStatus.PasswordMismatch, mismatch.Status);
        }

        h.Users.Verify(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task LoginHistory_OverlongFields_AreTruncatedToColumnLimits()
    {
        // نام کاربری و User-Agent از ورودیِ کنترل‌نشدهٔ مهاجم می‌آیند؛ بدون برش،
        // ثبت تاریخچهٔ ورود خودش خطای ذخیره‌سازی و پاسخ ۵۰۰ می‌داد.
        LoginHistory? saved = null;
        var repo = new Mock<ILoginHistoryRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<LoginHistory>()))
            .Callback<LoginHistory>(h => saved = h)
            .Returns(Task.CompletedTask);
        var uow = new Mock<IAshianebarghUnitOfWork>();
        uow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        var service = new LoginHistoryService(repo.Object, uow.Object);

        await service.RecordAsync(
            userId: new string('i', 900),
            userName: new string('u', 5000),
            succeeded: false,
            method: new string('m', 100),
            ipAddress: new string('1', 200),
            userAgent: new string('a', 5000),
            failureReason: new string('r', 5000));

        Assert.NotNull(saved);
        Assert.Equal(450, saved!.UserId!.Length);
        Assert.Equal(256, saved.UserName!.Length);
        Assert.Equal(32, saved.Method.Length);
        Assert.Equal(64, saved.IpAddress!.Length);
        Assert.Equal(512, saved.UserAgent!.Length);
        Assert.Equal(256, saved.FailureReason!.Length);
    }

    [Theory]
    [InlineData("09120000000", true)]
    [InlineData("۰۹۱۲۰۰۰۰۰۰۰", true)]
    [InlineData(" 09120000000 ", true)]
    [InlineData("0912000000", false)]
    [InlineData("+989120000000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AuthInput_PhoneValidation(string? phone, bool expected) =>
        Assert.Equal(expected, AuthInput.NormalizeValidPhone(phone) is not null);

    [Theory]
    [InlineData("123456", true)]
    [InlineData(" 123456 ", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("abcdef", false)]
    [InlineData("۱۲۳۴۵۶", false)]
    [InlineData(null, false)]
    public void AuthInput_CodeValidation(string? code, bool expected) =>
        Assert.Equal(expected, AuthInput.IsValidOtpCode(code));
}
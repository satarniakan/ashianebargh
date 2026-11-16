using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Exceptions;
using Ashianebargh.Domain.Interfaces;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// قواعد امنیتی <see cref="OtpService"/>: کد خام هرگز در دیتابیس ذخیره نمی‌شود،
/// حدس ناموفق محدود می‌شود و پس از سقف، همان شماره موقتاً قفل می‌شود.
/// </summary>
public class OtpServiceTests
{
    private const string Phone = "09120000000";

    private static (OtpService service, Mock<IOtpRepository> repo, Mock<ISmsSender> sms) Build(
        string pepper = "", bool allowLegacyUnpepperedHashes = false)
    {
        var repo = new Mock<IOtpRepository>();
        var sms = new Mock<ISmsSender>();
        var uow = new Mock<IAshianebarghUnitOfWork>();
        uow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        sms.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        // تروتل دیتابیسی با حافظهٔ درون‌تستی: رفتار واقعی شمارنده حفظ می‌شود
        var throttles = new Dictionary<string, OtpThrottle>(StringComparer.Ordinal);
        var throttleRepo = new Mock<IOtpThrottleRepository>();
        throttleRepo.Setup(r => r.GetByPhoneAsync(It.IsAny<string>()))
            .ReturnsAsync((string phone) => throttles.TryGetValue(phone, out var t) ? t : null);
        throttleRepo.Setup(r => r.AddAsync(It.IsAny<OtpThrottle>()))
            .Callback<OtpThrottle>(t => throttles[t.PhoneNumber] = t)
            .Returns(Task.CompletedTask);
        // رفتار اتمیک مخزن واقعی را با حافظهٔ درون‌تستی شبیه‌سازی می‌کند
        throttleRepo.Setup(r => r.RegisterAttemptAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .Returns((string phone, bool generation, DateTime now, TimeSpan window) =>
            {
                if (!throttles.TryGetValue(phone, out var t))
                {
                    t = new OtpThrottle { PhoneNumber = phone };
                    throttles[phone] = t;
                }

                var start = generation ? t.GenerationWindowStartUtc : t.FailedWindowStartUtc;
                var count = generation ? t.GenerationCount : t.FailedCount;
                if (start is null || now - start.Value >= window)
                {
                    count = 1;
                    start = now;
                }
                else
                {
                    count++;
                }

                if (generation)
                {
                    t.GenerationCount = count;
                    t.GenerationWindowStartUtc = start;
                }
                else
                {
                    t.FailedCount = count;
                    t.FailedWindowStartUtc = start;
                }

                return Task.FromResult(t);
            });
        throttleRepo.Setup(r => r.ClearFailedAttemptsAsync(It.IsAny<string>()))
            .Callback<string>(phone =>
            {
                if (throttles.TryGetValue(phone, out var t))
                {
                    t.FailedCount = 0;
                    t.FailedWindowStartUtc = null;
                }
            })
            .Returns(Task.CompletedTask);

        var settings = new Mock<ISettingService>();
        settings.Setup(s => s.GetAsync(It.IsAny<string>()))
            .ReturnsAsync("کد ورود شما: {Code}");
        settings.Setup(s => s.GetEffectiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(pepper);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Security:AllowLegacyUnpepperedHashes"] =
                    allowLegacyUnpepperedHashes ? "true" : "false",
            }).Build();

        var service = new OtpService(
            repo.Object, throttleRepo.Object, sms.Object, settings.Object,
            uow.Object, NullLogger<OtpService>.Instance, configuration: configuration);

        return (service, repo, sms);
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_SendsSixDigitCode_AndStoresOnlyItsHash()
    {
        var (service, repo, sms) = Build();

        string? sentMessage = null;
        sms.Setup(s => s.SendAsync(Phone, It.IsAny<string>()))
           .Callback<string, string>((_, m) => sentMessage = m)
           .Returns(Task.CompletedTask);

        OtpCode? stored = null;
        repo.Setup(r => r.AddAsync(It.IsAny<OtpCode>()))
            .Callback<OtpCode>(o => stored = o)
            .Returns(Task.CompletedTask);

        await service.GenerateAndSendOtpAsync(Phone);

        Assert.NotNull(sentMessage);
        var code = new string(sentMessage!.Where(char.IsAsciiDigit).ToArray());
        Assert.Equal(6, code.Length);

        Assert.NotNull(stored);
        Assert.NotEqual(code, stored!.Code);      // کد خام ذخیره نمی‌شود
        Assert.Equal(64, stored.Code.Length);     // SHA-256 به‌صورت hex
        Assert.Matches("^[0-9A-F]{64}$", stored.Code);
    }

    [Fact]
    public async Task VerifyOtpAsync_InvalidCode_ReturnsFalse()
    {
        var (service, repo, _) = Build();
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);

        Assert.False(await service.VerifyOtpAsync(Phone, "123456"));
    }

    [Fact]
    public async Task VerifyOtpAsync_ValidCode_MarksUsedAndReturnsTrue()
    {
        var (service, repo, _) = Build();
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new OtpCode { Id = 7, PhoneNumber = Phone, Code = "HASH" });
        repo.Setup(r => r.TryMarkAsUsedAsync(7)).ReturnsAsync(true);

        Assert.True(await service.VerifyOtpAsync(Phone, "123456"));
        repo.Verify(r => r.TryMarkAsUsedAsync(7), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_AfterMaxFailures_StopsTouchingRepository()
    {
        var (service, repo, _) = Build();
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);

        for (var i = 0; i < 5; i++)
            Assert.False(await service.VerifyOtpAsync(Phone, "000000"));

        // تلاش ششم باید پیش از هر کوئری، توسط قفل شماره متوقف شود
        Assert.False(await service.VerifyOtpAsync(Phone, "000000"));
        repo.Verify(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()), Times.Exactly(5));
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_WhenPhoneBlocked_ThrowsBusinessRuleException()
    {
        var (service, repo, _) = Build();
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);

        for (var i = 0; i < 5; i++)
            await service.VerifyOtpAsync(Phone, "000000");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GenerateAndSendOtpAsync(Phone));
    }

    /// <summary>هش مستقل از پیاده‌سازی سرویس، تا تست به داخلیِ HashOtp وابسته نباشد.</summary>
    private static string Sha256Hex(string input) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input)));

    [Fact]
    public async Task GenerateAndSendOtpAsync_BindsHashToPurpose()
    {
        // کدِ ورود و کدِ تعیین رمز باید هش متفاوت داشته باشند؛ وگرنه یک کد
        // در هر دو مسیر پذیرفته می‌شود و فیشینگِ کد ورود به تصاحب حساب می‌رسد.
        var (loginService, loginRepo, _) = Build();
        var (resetService, resetRepo, _) = Build();

        OtpCode? loginStored = null;
        OtpCode? resetStored = null;
        loginRepo.Setup(r => r.AddAsync(It.IsAny<OtpCode>()))
            .Callback<OtpCode>(o => loginStored = o).Returns(Task.CompletedTask);
        resetRepo.Setup(r => r.AddAsync(It.IsAny<OtpCode>()))
            .Callback<OtpCode>(o => resetStored = o).Returns(Task.CompletedTask);

        await loginService.GenerateAndSendOtpAsync(Phone, OtpPurpose.Login);
        await resetService.GenerateAndSendOtpAsync(Phone, OtpPurpose.PasswordReset);

        Assert.NotNull(loginStored);
        Assert.NotNull(resetStored);
        Assert.Equal(OtpPurpose.Login, loginStored!.Purpose);
        Assert.Equal(OtpPurpose.PasswordReset, resetStored!.Purpose);
        Assert.NotEqual(loginStored.Code, resetStored.Code);
    }

    [Fact]
    public async Task VerifyOtpAsync_LoginCode_IsNotAcceptedForPasswordReset()
    {
        var (service, repo, _) = Build();

        // مخزن فقط برای هدف Login رکورد برمی‌گرداند؛ هدف تعیین رمز باید خالی بماند
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), OtpPurpose.Login, It.IsAny<DateTime>()))
            .ReturnsAsync(new OtpCode { Id = 7, PhoneNumber = Phone, Code = "HASH", Purpose = OtpPurpose.Login });
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.IsAny<string>(), OtpPurpose.PasswordReset, It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);
        repo.Setup(r => r.TryMarkAsUsedAsync(7)).ReturnsAsync(true);

        Assert.True(await service.VerifyOtpAsync(Phone, "123456", OtpPurpose.Login));
        Assert.False(await service.VerifyOtpAsync(Phone, "123456", OtpPurpose.PasswordReset));
    }

    [Fact]
    public async Task VerifyOtpAsync_LegacyUnpepperedHash_RejectedByDefault()
    {
        // invariant «فقط هش + pepper»: ردیفی که بدون pepper هش شده نباید با
        // pepper تنظیم‌شده پذیرفته شود، وگرنه نشت دیتابیس بدون pepper کافی است.
        const string pepper = "test-pepper-value";
        var (service, repo, _) = Build(pepper);

        var legacyHash = Sha256Hex($"{Phone}:{(int)OtpPurpose.Login}:123456");
        repo.Setup(r => r.GetLatestValidAsync(Phone, legacyHash, OtpPurpose.Login, It.IsAny<DateTime>()))
            .ReturnsAsync(new OtpCode { Id = 9, PhoneNumber = Phone, Code = legacyHash });
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.Is<string>(s => s != legacyHash),
                OtpPurpose.Login, It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);

        Assert.False(await service.VerifyOtpAsync(Phone, "123456"));
        repo.Verify(r => r.TryMarkAsUsedAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_LegacyUnpepperedHash_AcceptedOnlyWithExplicitFlag()
    {
        // پنجرهٔ گذارِ چرخش pepper: فقط با کلید صریح، و فقط برای همان هش قدیمی.
        const string pepper = "test-pepper-value";
        var (service, repo, _) = Build(pepper, allowLegacyUnpepperedHashes: true);

        var legacyHash = Sha256Hex($"{Phone}:{(int)OtpPurpose.Login}:123456");
        repo.Setup(r => r.GetLatestValidAsync(Phone, legacyHash, OtpPurpose.Login, It.IsAny<DateTime>()))
            .ReturnsAsync(new OtpCode { Id = 9, PhoneNumber = Phone, Code = legacyHash });
        repo.Setup(r => r.GetLatestValidAsync(Phone, It.Is<string>(s => s != legacyHash),
                OtpPurpose.Login, It.IsAny<DateTime>()))
            .ReturnsAsync((OtpCode?)null);
        repo.Setup(r => r.TryMarkAsUsedAsync(9)).ReturnsAsync(true);

        Assert.True(await service.VerifyOtpAsync(Phone, "123456"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0912000000")]
    [InlineData("091200000000")]
    [InlineData("+989120000000")]
    [InlineData("abcdefghijk")]
    public async Task GenerateAndSendOtpAsync_InvalidPhone_Throws_WithoutSmsOrStorage(string phone)
    {
        // اعتبارسنجی باید در سرویس باشد نه فقط در فرم: API و circuit هم به همین‌جا می‌رسند
        // و ستون شماره nvarchar(32) است — ورودی بلند یعنی خطای ذخیره‌سازی به‌جای پیام روشن.
        var (service, repo, sms) = Build();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GenerateAndSendOtpAsync(phone));

        sms.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.AddAsync(It.IsAny<OtpCode>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_OverlongPhone_Throws()
    {
        var (service, repo, sms) = Build();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.GenerateAndSendOtpAsync(new string('9', 5000)));

        sms.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.AddAsync(It.IsAny<OtpCode>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_PersianDigits_AreNormalizedAndAccepted()
    {
        // نرمال‌سازی پیش از بررسی قالب است، وگرنه شمارهٔ درستِ فارسی‌تایپ‌شده رد می‌شد.
        var (service, _, sms) = Build();

        await service.GenerateAndSendOtpAsync("۰۹۱۲۰۰۰۰۰۰۰");

        sms.Verify(s => s.SendAsync(Phone, It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("")]
    [InlineData("۱۲۳۴۵۶")]
    public async Task VerifyOtpAsync_MalformedCode_ReturnsFalse_WithoutQuery(string code)
    {
        // ورودی بی‌قالب «تلاش ناموفق» نیست، پس نباید شمارندهٔ قفل شماره را جلو ببرد
        // (وگرنه درخواست‌های آشغالِ مهاجم، شمارهٔ قربانی را قفل می‌کرد).
        var (service, repo, _) = Build();

        Assert.False(await service.VerifyOtpAsync(Phone, code));

        repo.Verify(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()), Times.Never);
        repo.Verify(r => r.TryMarkAsUsedAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_InvalidPhone_ReturnsFalse_WithoutQuery()
    {
        var (service, repo, _) = Build();

        Assert.False(await service.VerifyOtpAsync("not-a-phone", "123456"));

        repo.Verify(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<OtpPurpose>(), It.IsAny<DateTime>()), Times.Never);
    }
}

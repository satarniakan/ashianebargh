using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure;
using Ashianebargh.Infrastructure.Services;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// صحت ثبت سرویس‌های پایه: نمودار DI باید کامل باشد و فرستندهٔ پیامک واقعاً
/// بر اساس «Sms:Provider» انتخاب شود — نه اینکه همیشه شبیه‌ساز بماند.
/// </summary>
public class ServiceRegistrationTests
{
    private const string DummyConnection =
        "Server=(localdb)\\mssqllocaldb;Database=AshianebarghTest;Trusted_Connection=True;TrustServerCertificate=True";

    private sealed class FakeCatalog : IPermissionCatalog
    {
        public IReadOnlyList<PermissionDescriptor> All { get; } =
            [new PermissionDescriptor("test.perm", "مجوز آزمایشی")];
    }

    private static ServiceProvider BuildProvider(string? smsProvider)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = DummyConnection,
            ["Sms:Provider"] = smsProvider,
            ["Sms:Kavenegar:ApiKey"] = "test-key",
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // در یک میزبان واقعی IConfiguration را خودِ وب‌سرور ثبت می‌کند؛ اینجا دستی می‌دهیم
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAshianebarghApplication();
        services.AddAshianebarghInfrastructure(configuration, isDevelopment: true);
        services.AddSingleton<IPermissionCatalog, FakeCatalog>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = false
        });
    }

    [Fact]
    public void BaseServices_AreAllResolvable()
    {
        using var provider = BuildProvider("Fake");
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IAuthService>());
        Assert.NotNull(sp.GetRequiredService<IOtpService>());
        Assert.NotNull(sp.GetRequiredService<IAshianebarghUnitOfWork>());
        Assert.NotNull(sp.GetRequiredService<IAuditService>());
        Assert.NotNull(sp.GetRequiredService<IPermissionService>());
        Assert.NotNull(sp.GetRequiredService<IUserAdminService>());
        Assert.NotNull(sp.GetRequiredService<INotificationService>());
        Assert.NotNull(sp.GetRequiredService<IOutboxService>());
        Assert.NotNull(sp.GetRequiredService<IEmailSender>());
        Assert.NotNull(sp.GetRequiredService<ISmsSender>());
    }

    [Fact]
    public void SmsSender_IsFake_WhenProviderNotSet()
    {
        using var provider = BuildProvider(null);
        using var scope = provider.CreateScope();

        Assert.IsType<FakeSmsSender>(scope.ServiceProvider.GetRequiredService<ISmsSender>());
    }

    [Fact]
    public void SmsSender_IsKavenegar_WhenProviderIsKavenegar()
    {
        using var provider = BuildProvider("Kavenegar");
        using var scope = provider.CreateScope();

        Assert.IsType<KavenegarSmsSender>(scope.ServiceProvider.GetRequiredService<ISmsSender>());
    }

    /// <summary>
    /// کانفیگ Production با همهٔ مقادیر اجباری پر و معتبر؛ هر تست یک کلید را
    /// خالی/نمونه می‌کند. مقادیر عمداً واقع‌نما هستند تا گاردهای «محتوا»
    /// (طول pepper، قالب کلید Turnstile، قالب شماره) هم exercised شوند.
    /// </summary>
    private static Dictionary<string, string?> ProductionSettings() => new()
    {
        ["ConnectionStrings:Default"] = DummyConnection,
        ["Captcha:Provider"] = "Turnstile",
        ["Captcha:Turnstile:SiteKey"] = "0x4AAAAAAABBBBBBBBBBBBBB",
        ["Captcha:Turnstile:SecretKey"] = "0x4AAAAAAACCCCCCCCCCCCCC",
        ["Security:Pepper"] = "a1b2c3d4e5f60718293a4b5c6d7e8f90",
        ["Identity:FirstAdminPhoneNumber"] = "09121234567",
        ["Sms:Provider"] = "Kavenegar",
        ["Sms:Kavenegar:ApiKey"] = "kavenegar-live-key",
    };

    private static void RegisterProduction(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAshianebarghApplication();
        services.AddAshianebarghInfrastructure(configuration, isDevelopment: false);
    }

    [Fact]
    public void Production_Throws_WhenCaptchaProviderNotTurnstile()
    {
        var settings = ProductionSettings();
        settings["Captcha:Provider"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenTurnstileSecretMissing()
    {
        var settings = ProductionSettings();
        settings["Captcha:Turnstile:SecretKey"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenTurnstileSiteKeyMissing()
    {
        var settings = ProductionSettings();
        settings["Captcha:Turnstile:SiteKey"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenPepperMissing()
    {
        var settings = ProductionSettings();
        settings["Security:Pepper"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenPepperIsRepoSample()
    {
        // مقدار عمومیِ .env.example قدیمی: اگر پذیرفته شود، هش OTP/توکن با
        // pepper شناخته‌شدهٔ ریپو کلید می‌خورد و لو رفتن دیتابیس کافی است.
        var settings = ProductionSettings();
        settings["Security:Pepper"] = "ChangeMe_RandomLongSecret";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenPepperTooShort()
    {
        var settings = ProductionSettings();
        settings["Security:Pepper"] = "short-pepper";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenPepperHasLowEntropy()
    {
        var settings = ProductionSettings();
        settings["Security:Pepper"] = new string('a', 40);

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenTurnstileKeyIsRepoSample()
    {
        var settings = ProductionSettings();
        settings["Captcha:Turnstile:SiteKey"] = "0x4AAAAAAA_replace_me";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenTurnstileKeyIsNotARealKey()
    {
        var settings = ProductionSettings();
        settings["Captcha:Turnstile:SecretKey"] = "not-a-turnstile-key";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenFirstAdminPhoneMissing()
    {
        var settings = ProductionSettings();
        settings["Identity:FirstAdminPhoneNumber"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenFirstAdminPhoneIsRepoSample()
    {
        // شمارهٔ نمونه یعنی هر دارندهٔ آن روی دیتابیس تازه Admin می‌شود.
        var settings = ProductionSettings();
        settings["Identity:FirstAdminPhoneNumber"] = "09120000000";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenFirstAdminPhoneIsNotIranianMobile()
    {
        var settings = ProductionSettings();
        settings["Identity:FirstAdminPhoneNumber"] = "+15551234567";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenSmsProviderIsFake()
    {
        // Fake در Production یعنی هیچ کدی ارسال نمی‌شود ولی پاسخ «کد ارسال شد»
        // می‌ماند — بن‌بست خاموش برای ادمین اول.
        var settings = ProductionSettings();
        settings["Sms:Provider"] = "Fake";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Throws_WhenSmsApiKeyMissing()
    {
        var settings = ProductionSettings();
        settings["Sms:Kavenegar:ApiKey"] = "";

        Assert.Throws<InvalidOperationException>(() => RegisterProduction(settings));
    }

    [Fact]
    public void Production_Registers_WhenAllRequiredSecretsPresent()
    {
        // بدون پرتاب استثنا یعنی گاردها با کانفیگ کاملِ Production اجازهٔ استقرار می‌دهند
        RegisterProduction(ProductionSettings());
    }
}
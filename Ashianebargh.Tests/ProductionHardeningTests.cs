using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Ashianebargh.App;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Services;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون سخت‌سازی production (گزارش امنیتی):
/// ۱) خطای شبکهٔ کاوه‌نگار هرگز کلید API/URL را به پیام استثنا — و در نتیجه لاگ/Outbox — نمی‌برد؛
/// ۲) نام برنامهٔ DataProtection پایدار است تا حلقهٔ کلید بین نسخه‌ها مشترک بماند؛
/// ۳) appsettings.Production.json سطح لاگ System/Microsoft را Warning نگه می‌دارد و محرمانه ندارد.
/// </summary>
public class ProductionHardeningTests
{
    // ---- ۱) کاوه‌نگار: پاک‌سازی خطای انتقال ----

    /// <summary>هندلری که عمداً پیام خامِ حاوی URL کامل (شامل کلید) را پرتاب می‌کند.</summary>
    private sealed class ThrowingHandler(string message) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(message));
    }

    private static KavenegarSmsSender SenderWithKey(HttpClient http, string apiKey)
    {
        var settings = new Mock<ISettingService>();
        settings.Setup(s => s.GetEffectiveAsync(
                IntegrationSettingKeys.SmsKavenegarApiKey, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(apiKey);
        settings.Setup(s => s.GetEffectiveAsync(
                IntegrationSettingKeys.SmsKavenegarSender, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("");

        return new KavenegarSmsSender(
            http, settings.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<KavenegarSmsSender>.Instance);
    }

    [Fact]
    public async Task Kavenegar_TransportError_DoesNotLeakApiKeyOrUrl()
    {
        const string apiKey = "SUPER-SECRET-KAVENEGAR-KEY";
        // پیام خام استثنا عمداً URL کامل (شامل کلید) را دارد — همان چیزی که لاگ HttpClient می‌سازد.
        var leakingUrl = $"https://api.kavenegar.com/v1/{apiKey}/sms/send.json";
        var sender = SenderWithKey(
            new HttpClient(new ThrowingHandler($"Connection refused to {leakingUrl}")), apiKey);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync("09121234567", "کد: 1234"));

        Assert.DoesNotContain(apiKey, ex.Message);
        Assert.DoesNotContain("api.kavenegar.com", ex.Message);
        Assert.DoesNotContain("https://", ex.Message);
    }

    // ---- ۲) DataProtection: نام پایدار برنامه ----

    [Fact]
    public void DataProtection_UsesStableApplicationName()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAshianebarghDataProtection(configuration, Path.GetTempPath());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        // نام ثابت یعنی حلقهٔ کلید به مسیر content-root/نام نمونه گره نمی‌خورد و در
        // استقرار چندنسخه‌ای همهٔ نسخه‌ها کلید مشترک دارند (کوکی/توکن ضدجعل نمی‌پرد).
        // SetApplicationName مقدار ApplicationDiscriminator را ست می‌کند.
        Assert.Equal("Ashianebargh", options.ApplicationDiscriminator);
    }

    // ---- ۳) appsettings.Production.json: سطح لاگ و نبود محرمانه ----

    private static string ReadProductionSettings()
    {
        // به‌صورت Content لینک‌شده به خروجی تست کپی می‌شود (مستقل از artifacts-path).
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
        Assert.True(File.Exists(path), $"appsettings.Production.json باید در خروجی تست باشد: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ProductionSettings_SuppressSystemAndMicrosoftLogs()
    {
        using var doc = JsonDocument.Parse(ReadProductionSettings());
        var overrides = doc.RootElement
            .GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Override");

        // کلید API کاوه‌نگار داخل مسیر URL است؛ لاگ HttpClient در سطح Information آن را
        // ثبت می‌کند. پایین آوردن این سطح، کلید را به لاگ/shipper می‌فرستد (مورد H1).
        Assert.Equal("Warning", overrides.GetProperty("System").GetString());
        Assert.Equal("Warning", overrides.GetProperty("Microsoft").GetString());
    }

    [Fact]
    public void ProductionSettings_ContainsNoSecrets()
    {
        var json = ReadProductionSettings();

        // این فایل committed است و به ایمیج production می‌رود؛ هیچ محرمانه‌ای نباید اینجا باشد.
        // کلیدها/رمزها از env (compose) یا ردیف تنظیمات دیتابیس می‌آیند، نه این فایل.
        Assert.DoesNotContain("ConnectionStrings", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pepper", json, StringComparison.OrdinalIgnoreCase);
    }
}
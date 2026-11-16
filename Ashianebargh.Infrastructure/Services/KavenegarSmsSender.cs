// Ashianebargh.Infrastructure/Services/KavenegarSmsSender.cs
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ashianebargh.Infrastructure.Services;

/// <summary>
/// ارسال پیامک واقعی از طریق کاوه‌نگار (REST).
/// مقدار مؤثر هر بار ارسال خوانده می‌شود: ردیف دیتابیس ← appsettings
/// («Sms:Kavenegar:ApiKey» و «Sms:Kavenegar:Sender»). بدون ApiKey، ارسال با
/// خطای روشن شکست می‌خورد تا در Outbox دیده شود.
/// </summary>
public class KavenegarSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly ISettingService _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KavenegarSmsSender> _logger;

    public KavenegarSmsSender(HttpClient http, ISettingService settings,
        IConfiguration configuration, ILogger<KavenegarSmsSender> logger)
    {
        _http = http;
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message)
    {
        var apiKey = await _settings.GetEffectiveAsync(
            IntegrationSettingKeys.SmsKavenegarApiKey, "Sms:Kavenegar:ApiKey");
        var sender = await _settings.GetEffectiveAsync(
            IntegrationSettingKeys.SmsKavenegarSender, "Sms:Kavenegar:Sender");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "پیکربندی پیامک ناقص است (کلید کاوه‌نگار در تنظیمات یا Sms:Kavenegar:ApiKey).");

        var payload = new Dictionary<string, string> { ["receptor"] = phoneNumber, ["message"] = message };
        if (!string.IsNullOrWhiteSpace(sender))
            payload["sender"] = sender;

        // کلید API طبق طراحی REST کاوه‌نگار داخل مسیر URL است و نمی‌توان آن را به هدر برد.
        // پس دو لایه محافظت داریم: (۱) سطح لاگ System/Microsoft=Warning در production تا
        // لاگِ HttpClient آدرس کامل را ثبت نکند؛ (۲) همین try/catch که پیام خام استثنا
        // (که ممکن است URL/کلید را داشته باشد) هرگز به لاگ یا Outbox نرسد.
        var url = $"https://api.kavenegar.com/v1/{apiKey}/sms/send.json";
        KavenegarResponse? body;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(url, new FormUrlEncodedContent(payload));
            body = await response.Content.ReadFromJsonAsync<KavenegarResponse>();
        }
        catch (Exception ex)
        {
            // فقط نوع خطا لاگ می‌شود، نه Message/URL؛ کلید داخل URL است.
            _logger.LogWarning("ارسال پیامک کاوه‌نگار با خطای شبکه/سرویس مواجه شد: {ErrorType}", ex.GetType().Name);
            throw new InvalidOperationException("ارسال پیامک ناموفق بود (خطای شبکه/سرویس).");
        }

        if (body?.Return?.Status == 200)
            return;

        var error = body?.Return?.Message ?? response.StatusCode.ToString();
        _logger.LogWarning("ارسال پیامک کاوه‌نگار ناموفق: {Error}", error);
        throw new InvalidOperationException($"ارسال پیامک ناموفق: {error}");
    }

    private sealed class KavenegarResponse
    {
        [JsonPropertyName("return")] public KavenegarReturn? Return { get; set; }
    }

    private sealed class KavenegarReturn
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}

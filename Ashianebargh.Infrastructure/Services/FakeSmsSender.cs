using Microsoft.Extensions.Logging;
using Ashianebargh.Application.Helpers;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Infrastructure.Services;

/// <summary>
/// جایگزین موقت سرویس پیامک تا وقتی ارائه‌دهنده‌ی واقعی وصل نشده است.
/// متن پیام شامل کد OTP است؛ پس فقط در Development داخل لاگ نوشته می‌شود
/// (لاگ Production روی دیسک می‌ماند و هر کسی به آن دسترسی داشته باشد می‌تواند وارد شود).
/// </summary>
public class FakeSmsSender : ISmsSender
{
    private readonly ILogger<FakeSmsSender> _logger;
    private readonly bool _logMessageBody;

    public FakeSmsSender(ILogger<FakeSmsSender> logger, bool logMessageBody)
    {
        _logger = logger;
        _logMessageBody = logMessageBody;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        if (_logMessageBody)
        {
            _logger.LogWarning("=== SMS SIMULATION === To: {PhoneNumber} | Message: {Message}",
                PiiMask.Phone(phoneNumber), message);
        }
        else
        {
            _logger.LogWarning("SMS to {PhoneNumber} was NOT sent: no SMS provider is configured.",
                PiiMask.Phone(phoneNumber));
        }

        return Task.CompletedTask;
    }
}

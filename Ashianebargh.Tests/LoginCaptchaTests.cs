using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Ashianebargh.App;
using Ashianebargh.App.Middleware;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون «کپچا روی همهٔ مسیرهای ورود»: محدودیت نرخ IP-محور است، پس بدون کپچا
/// مهاجم با IPهای چرخان می‌توانست رمز یا کد پیامکی را بمباران کند.
/// کپچای میزبانِ تست عمداً «ردکننده» است تا اثرِ واقعیِ اعتبارسنجی دیده شود
/// (در Development کپچای پیش‌فرض همیشه موفق است و چیزی را اثبات نمی‌کند).
/// </summary>
public class LoginCaptchaTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient _client = null!;

    private sealed class FakeCatalog : IPermissionCatalog
    {
        public IReadOnlyList<PermissionDescriptor> All { get; } =
            [new PermissionDescriptor("test.perm", "مجوز آزمایشی")];
    }

    private sealed class RejectingCaptcha : ICaptchaValidator
    {
        public string Name => "rejecting";

        public Task<bool> ValidateAsync(string? token, string? remoteIp,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(LoginCaptchaTests).Assembly.GetName().Name
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Server=(localdb)\\mssqllocaldb;Database=AshianebarghTest;Trusted_Connection=True;TrustServerCertificate=True",
            ["Sms:Provider"] = "Fake"
        });

        builder.WebHost.UseTestServer();

        builder.Services.AddAshianebargh(builder.Configuration, isDevelopment: true);
        builder.Services.AddSingleton<IPermissionCatalog, FakeCatalog>();
        builder.Services.RemoveAll<ICaptchaValidator>();
        builder.Services.AddSingleton<ICaptchaValidator, RejectingCaptcha>();
        builder.Services.RemoveAll<IHostedService>();

        _app = builder.Build();

        _app.UseAshianebarghSecurityHeaders();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAntiforgery();
        _app.UseRateLimiter();
        _app.MapAshianebargh();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// فرمِ POST با توکن ضدجعل واقعی: کوکی و توکن از همان endpointای گرفته می‌شوند
    /// که JS مرورگر استفاده می‌کند، وگرنه درخواست پیش از رسیدن به handler رد می‌شود
    /// و تست چیزی دربارهٔ کپچا اثبات نمی‌کند.
    /// </summary>
    private async Task<HttpResponseMessage> PostFormAsync(string path, Dictionary<string, string> fields)
    {
        var tokenResponse = await _client.GetAsync("/api/xsrf-token");
        tokenResponse.EnsureSuccessStatusCode();

        var cookies = tokenResponse.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';')[0])
            .ToList();
        const string xsrfPrefix = "__XSRF-TOKEN=";
        var xsrf = cookies.First(c => c.StartsWith(xsrfPrefix, StringComparison.Ordinal))[xsrfPrefix.Length..];

        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Add("Cookie", string.Join("; ", cookies));
        request.Headers.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(xsrf));

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task PasswordLogin_WhenCaptchaFails_RedirectsWithCaptchaError()
    {
        // شناسهٔ ورود عمداً بدون قالب ایمیل است: endpoint هیچ محدودیت فرمتی روی
        // نام کاربری ندارد و باید تا مرحلهٔ کپچا پیش برود.
        var response = await PostFormAsync("/Account/Login", new Dictionary<string, string>
        {
            ["userName"] = "someone",
            ["password"] = "whatever-1"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login-password?error=captcha", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task VerifyOtp_WhenCaptchaFails_RedirectsWithCaptchaError()
    {
        var response = await PostFormAsync("/Account/VerifyOtp", new Dictionary<string, string>
        {
            ["phoneNumber"] = "09120000000",
            ["code"] = "123456"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("error=captcha", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ApiPasswordLogin_WhenCaptchaFails_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "someone@test.ir",
            password = "whatever-1",
            deviceName = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApiOtpRequest_WhenCaptchaFails_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new
        {
            phoneNumber = "09120000000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
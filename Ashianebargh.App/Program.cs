// Ashianebargh.App/Program.cs
// میزبان خالی: پایه را بدون هیچ دامنه‌ای بالا می‌آورد. دامنهٔ خودتان را با
// IAshianebarghModule / IPermissionCatalog / INavProvider وصل کنید.
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Ashianebargh.Infrastructure;
using Ashianebargh.Infrastructure.Data;
using Ashianebargh.App;
using Ashianebargh.App.Middleware;
using Ashianebargh.App.Endpoints;
using Ashianebargh.App.Data;
using Ashianebargh.Domain.Interfaces;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Razor Components (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.DetailedErrors = builder.Environment.IsDevelopment());

// لاگر نهایی از appsettings خوانده می‌شود؛ سطح لاگ بین Development و Production فرق می‌کند.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: Serilog.RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

// Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

// هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند.
// مایگریشن‌ها در همین مونتاژ میزبان‌اند (پوشهٔ Migrations) تا اسکیمای دامنه وارد پایه نشود.
builder.Services.AddAshianebargh(builder.Configuration, builder.Environment.IsDevelopment(),
    migrationsAssembly: typeof(Program).Assembly.GetName().Name);
builder.Services.AddAppWeb();

// سیاست‌های دسترسی خودکارند: PermissionPolicyProvider پایه هر کلید IPermissionCatalog
// را به policy تبدیل می‌کند، پس ثبت دستی لازم نیست.

builder.Services.AddHealthChecks().AddDbContextCheck<AshianebarghDbContext>();

var app = builder.Build();

// Forwarded Headers — باید اولین middleware باشد تا RemoteIpAddress واقعیِ کاربر
// (پشت reverse proxy) برای Rate Limiter و لاگ در دسترس باشد.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
{
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}
app.UseForwardedHeaders(forwardedHeadersOptions);

// شناسهٔ همبستگی (X-Correlation-ID): اولِ پایپ‌لاین تا همهٔ لاگ‌ها و پاسخ‌ها آن را داشته باشند
app.UseAshianebarghCorrelationId();

// هدرهای امنیتی (nosniff، SAMEORIGIN، Referrer-Policy) — روی همهٔ پاسخ‌ها، از جمله خطاها.
app.UseAshianebarghSecurityHeaders();

// مدیریت خطای سراسری — باید اولِ پایپ‌لاین باشد تا همهٔ خطاها را ببیند.
// پیام فارسیِ امن + کد وضعیت درست؛ جزئیات فنی فقط در لاگ (و در Development در پاسخ).
app.UseAshianebarghExceptionHandler();

// اعمال خودکار مایگریشن‌های EF هنگام استارتاپ — دیگر فراموش نمی‌شوند.
// با «Database:AutoMigrate=false» می‌توان خاموشش کرد تا مایگریشن‌ها کنترل‌شده
// (مثلاً با یک job جدا و قبل از بالا آمدن چند نسخهٔ همزمان) اجرا شوند.
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AshianebarghDbContext>();
    await db.Database.MigrateAsync();
}
else
{
    Log.Information("Auto-migration disabled (Database:AutoMigrate=false); apply migrations manually.");
}

// مجوزها و نقش‌های پایه
await app.Services.InitializeAshianebarghAsync();

// دادهٔ نمونهٔ سایت فقط در Development و با فلگ صریح؛ اجرای دوباره رکورد تکراری نمی‌سازد.
if (app.Environment.IsDevelopment() && args.Contains("--seed-test-content", StringComparer.OrdinalIgnoreCase))
{
    await using var seedScope = app.Services.CreateAsyncScope();
    await TestContentSeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<AshianebarghDbContext>(),
        seedScope.ServiceProvider.GetRequiredService<IFileStorage>(),
        Path.Combine(builder.Environment.ContentRootPath, "SeedAssets", "products"),
        seedScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TestContentSeed"));
}

app.UseSerilogRequestLogging();

app.UseRequestLocalization(AshianebarghSetup.PersianLocalization());

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// endpointهای فایل‌های استاتیک. باید Map شود (نه فقط UseStaticFiles) تا مسیر
// تک‌بخشی مثل /app.css یا /site.css با مسیر صفحهٔ پویای /{Slug} تداخل نکند.
app.MapStaticAssets();

// درخواست‌هایی که با هیچ صفحه‌ای مطابقت ندارند به صفحهٔ طراحی‌شدهٔ not-found هدایت می‌شوند.
app.UseStatusCodePagesWithReExecute("/not-found", "?from={0}");

// Antiforgery به هویت کاربر نیاز دارد، پس باید بعد از Authentication/Authorization بیاید
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// endpointهای پایه (احراز هویت و اعلان‌ها). endpoint دامنه بعد از این می‌آید.
app.MapAshianebargh();
app.MapSiteEndpoints();
app.MapHealthChecks("/health");

// مستندات خودکار API موبایل — فقط در Development (شامل کلید عمومی پوش و مسیرهای auth).
// مرور JSON در: /openapi/v1.json
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapRazorComponents<Ashianebargh.App.Components.App>()
    .AddInteractiveServerRenderMode();

try
{
    Log.Information("Starting Ashianebargh.App application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// نقطهٔ ورود قابل ارجاع برای WebApplicationFactory در تست‌های یکپارچگی
public partial class Program;

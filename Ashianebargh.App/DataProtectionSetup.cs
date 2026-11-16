using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Ashianebargh.App;

/// <summary>
/// پیکربندی DataProtection میزبان: نام پایدار برنامه + ماندگاری کلیدها + رمزنگاری
/// اختیاریِ کلیدها در حال سکون. جدا از Program.cs تا بدون بالا آوردن کل میزبان
/// (و دیتابیس) قابل تست باشد.
/// </summary>
public static class DataProtectionSetup
{
    public static IServiceCollection AddAshianebarghDataProtection(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        // SetApplicationName حلقهٔ کلید را از مسیر content-root/نام نمونه مستقل می‌کند تا
        // در استقرار چندنسخه‌ای همهٔ نسخه‌ها کلید مشترک داشته باشند و کوکی‌ها بین آن‌ها معتبر بمانند.
        var keysPath = Path.Combine(contentRootPath, "DataProtection-Keys");
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("Ashianebargh")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        // رمزنگاری کلیدها در حال سکون (اختیاری): با تنظیم «DataProtection:CertificatePath»
        // (گواهی دارای کلید خصوصی، مثلاً PFX) کلیدهای XML روی ولوِل رمزنگاری می‌شوند.
        // بدون آن کلیدها plaintext روی ولوِل‌اند؛ دسترسی به ولوِل تقریباً هم‌سطح میزبان است.
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            // X509CertificateLoader جایگزین غیرمنسوخِ سازندهٔ X509Certificate2 در .NET 10 است.
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath, configuration["DataProtection:CertificatePassword"]);
            dataProtection.ProtectKeysWithCertificate(certificate);
        }

        return services;
    }
}
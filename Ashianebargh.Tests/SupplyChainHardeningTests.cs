using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون سخت‌سازی زنجیرهٔ تأمین و استقرار (گزارش امنیتی، مرحلهٔ CI/قالب/بسته‌ها):
/// نسخه‌های وصله‌شدهٔ بسته‌های آسیب‌پذیر، منبع یکتای نقشه‌دار NuGet، پین‌بودن ایمیج پایه،
/// دسترسی کمینه و اکشن‌های SHA-پین در CI، و پیکربندی قالبی که محرمانه/خروجی build را حمل نکند.
/// فایل‌ها به‌صورت Content لینک‌شده در خروجی تست کپی می‌شوند (مستقل از artifacts-path).
/// </summary>
public class SupplyChainHardeningTests
{
    private static string ReadRepoFile(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), $"فایل پیکربندی باید در خروجی تست باشد: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void VulnerablePackages_ArePinnedToPatchedVersions()
    {
        var doc = XDocument.Parse(ReadRepoFile("Directory.Packages.props"));

        string VersionOf(string id) => doc.Descendants("PackageVersion")
            .First(e => e.Attribute("Include")?.Value == id).Attribute("Version")!.Value;

        // Pkcs: آسیب‌پذیری HIGH/Moderate در ۱۰.۰.۰؛ وصله از ۱۰.۰.۱۳.
        Assert.True(Version.Parse(VersionOf("System.Security.Cryptography.Pkcs")) >= new Version(10, 0, 13),
            "System.Security.Cryptography.Pkcs باید دست‌کم ۱۰.۰.۱۳ باشد (وصلهٔ GHSA-846f-f764-76mx).");
        // AngleSharp: آسیب‌پذیری Moderate در < ۱.۵.۰.
        Assert.True(Version.Parse(VersionOf("AngleSharp")) >= new Version(1, 5, 0),
            "AngleSharp باید دست‌کم ۱.۵.۰ باشد (وصلهٔ GHSA-pgww-w46g-26qg).");

        // پین گذرا باید روشن بماند وگرنه نسخهٔ درخواستیِ MimeKit (۱۰.۰.۰) دوباره غالب می‌شود.
        var pinning = doc.Descendants("CentralPackageTransitivePinningEnabled").Single();
        Assert.Equal("true", pinning.Value, ignoreCase: true);
    }

    [Fact]
    public void NuGetConfig_UsesSingleMappedAuthoritativeSource()
    {
        var doc = XDocument.Parse(ReadRepoFile("NuGet.config"));

        var sources = doc.Descendants("packageSources").Single();
        // پاک‌کردن منابع ماشین (mirrorهای محلی) تا restore در CI و توسعه یکسان باشد.
        Assert.NotNull(sources.Element("clear"));
        var adds = sources.Elements("add").ToList();
        Assert.Single(adds);
        Assert.Equal("nuget.org", adds[0].Attribute("key")?.Value);
        // نقشهٔ بسته الزامی است وگرنه NU1507 برمی‌گردد و ریسک زنجیرهٔ تأمین می‌ماند.
        Assert.NotEmpty(doc.Descendants("packageSourceMapping"));
    }

    [Fact]
    public void Template_RenamesCorrectly_AndExcludesSecretsAndBuildOutput()
    {
        // پروژه‌ای که خودش از این قالب ساخته شده، .template.config را ندارد (عمداً exclude
        // شده است)؛ این تست فقط در مخزنِ قالب معنا دارد و در پروژهٔ تولیدشده بی‌اثر است.
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "template.json"))) return;

        using var doc = JsonDocument.Parse(ReadRepoFile("template.json"));
        var root = doc.RootElement;

        Assert.Equal("empty-ashianebargh", root.GetProperty("shortName").GetString());
        Assert.Equal("Ashianebargh", root.GetProperty("sourceName").GetString());
        Assert.True(root.GetProperty("preferNameDirectory").GetBoolean());

        var exclude = root.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("exclude");
        var patterns = exclude.EnumerateArray().Select(e => e.GetString()).ToHashSet();

        // محرمانهٔ محلی و خروجی build هرگز نباید وارد قالب شوند.
        Assert.Contains("**/appsettings.json", patterns);
        Assert.Contains("**/DataProtection-Keys/**", patterns);
        Assert.Contains("**/bin/**", patterns);
        Assert.Contains("**/obj/**", patterns);
    }

    [Fact]
    public void CI_HasLeastPrivilege_ShaPinnedActions_AndVulnerabilityGate()
    {
        var yaml = ReadRepoFile("ci.yml");

        // توکن پیش‌فرض نباید دسترسی نوشتن داشته باشد.
        Assert.Contains("permissions:", yaml);
        Assert.Contains("contents: read", yaml);

        // هر اکشن باید به SHA پین شده باشد (تگ شناور قابل‌جابه‌جایی است).
        var uses = Regex.Matches(yaml, @"uses:\s*(\S+)");
        Assert.NotEmpty(uses);
        foreach (Match m in uses)
            Assert.Matches(@"^[^@]+@[0-9a-f]{40}$", m.Groups[1].Value);

        // دروازهٔ بستهٔ آسیب‌پذیر (NU190x) باید بماند.
        Assert.Contains("NU190", yaml);
    }

    [Fact]
    public void Dockerfile_BaseImages_AreDigestPinned()
    {
        var dockerfile = ReadRepoFile("Dockerfile");

        var fromLines = Regex.Matches(dockerfile, @"^FROM\s+(\S+)", RegexOptions.Multiline);
        Assert.NotEmpty(fromLines);
        foreach (Match m in fromLines)
            Assert.Contains("@sha256:", m.Groups[1].Value);
    }
}
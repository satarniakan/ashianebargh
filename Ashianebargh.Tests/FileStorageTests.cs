using System.Text;
using Microsoft.Extensions.Configuration;
using Ashianebargh.Infrastructure.Services;

namespace Ashianebargh.Tests;

/// <summary>
/// LocalFileStorage: رفت‌وبرگشت بایت‌ها، whitelist پسوند، سقف حجم و
/// ماندن مسیر زیر Root (دفاع در برابر ../).
/// </summary>
public class FileStorageTests : IDisposable
{
    private readonly string _root;

    public FileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* تمیزکاری تست نباید خطا بدهد */ }
    }

    private LocalFileStorage Build(Dictionary<string, string?>? settings = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Storage:RootPath"] = _root,
            ["Storage:MaxFileSizeBytes"] = "1024",
            ["Storage:AllowedExtensions"] = "txt,png"
        };
        if (settings is not null)
        {
            foreach (var kv in settings)
                defaults[kv.Key] = kv.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();

        return new LocalFileStorage(configuration);
    }

    private static MemoryStream StreamOf(string text) =>
        new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task SaveAndOpen_RoundTripsBytes()
    {
        var storage = Build();

        var id = await storage.SaveAsync("owner-1", "doc", StreamOf("سلام"), "note.txt");
        await using var read = await storage.OpenReadAsync("owner-1", "doc", id);

        Assert.NotNull(read);
        using var reader = new StreamReader(read!);
        Assert.Equal("سلام", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Save_DisallowedExtension_Throws()
    {
        var storage = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "doc", StreamOf("x"), "evil.exe"));
    }

    [Fact]
    public async Task Save_OverSizeLimit_Throws()
    {
        var storage = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "doc", StreamOf(new string('x', 2000)), "big.txt"));
    }

    [Fact]
    public async Task Save_PathTraversalInFileType_Throws()
    {
        var storage = Build();

        // دو سطح بالاتر می‌رود و از Root خارج می‌شود — باید رد شود
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "../../escape", StreamOf("x"), "note.txt"));

        // و هیچ فایل نیمه‌کاره‌ای هم نباید مانده باشد
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Delete_RemovesFile_AndOpenReturnsNull()
    {
        var storage = Build();

        var id = await storage.SaveAsync("owner-1", "doc", StreamOf("bye"), "note.txt");
        Assert.True(await storage.ExistsAsync("owner-1", "doc", id));

        await storage.DeleteAsync("owner-1", "doc", id);

        Assert.False(await storage.ExistsAsync("owner-1", "doc", id));
        Assert.Null(await storage.OpenReadAsync("owner-1", "doc", id));
    }

    private static MemoryStream StreamOfBytes(byte[] bytes) => new(bytes);

    // امضای واقعی PNG (۸ بایت اول) + چند بایت ادامه؛ برای بررسی magic-byte کافی است.
    private static byte[] PngHeader() => new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    };

    [Fact]
    public async Task Save_ValidPngSignature_IsAccepted()
    {
        var storage = Build();

        var id = await storage.SaveAsync("owner-1", "img", StreamOfBytes(PngHeader()), "photo.png");

        Assert.True(await storage.ExistsAsync("owner-1", "img", id));
    }

    [Fact]
    public async Task Save_SpoofedPng_TextContent_Throws()
    {
        // پسوند png ولی محتوا متن است → امضا نمی‌خواند → رد (ضد جعل پسوند).
        var storage = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "img", StreamOf("سلام"), "fake.png"));
    }

    [Fact]
    public async Task Save_HtmlDisguisedAsJpg_Throws()
    {
        var storage = Build(new Dictionary<string, string?>
        {
            ["Storage:AllowedExtensions"] = "jpg"
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "img", StreamOf("<html><script>alert(1)</script></html>"), "evil.jpg"));
    }

    [Fact]
    public async Task Save_DangerousContentType_Throws()
    {
        // حتی با بایت‌های معتبر PNG، contentType وب/اجرایی رد می‌شود.
        var storage = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "img", StreamOfBytes(PngHeader()), "photo.png", "text/html"));
    }

    [Fact]
    public async Task Save_EmptyFile_Throws()
    {
        var storage = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("owner-1", "doc", StreamOfBytes(Array.Empty<byte>()), "empty.txt"));
    }
}

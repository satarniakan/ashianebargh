using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Ashianebargh.App.Services;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// ساخت نسخه‌های کوچک‌شدهٔ تصویر برای srcset: عرض درست، خروجی WebP، رد کردن
/// بزرگ‌نمایی و بی‌اثر بودن عرض غیرمجاز (که در غیر این‌صورت اجازهٔ ساخت فایل
/// بی‌نهایت می‌داد).
/// </summary>
public class ImageVariantTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ashvariants-{Guid.NewGuid():N}");

    private ImageVariantService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = _root })
            .Build();

        return new ImageVariantService(configuration, NullLogger<ImageVariantService>.Instance);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(30, 90, 200));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static Task<Stream?> OpenBytes(byte[] bytes) =>
        Task.FromResult<Stream?>(new MemoryStream(bytes));

    [Fact]
    public async Task Create_ProducesSmallerWebpWithRequestedWidth()
    {
        var service = CreateService();
        var original = CreatePng(1000, 500);

        var path = await service.GetOrCreateAsync("photo.jpg", _ => OpenBytes(original), "image/jpeg", 320, default);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        using var codec = SKCodec.Create(path!);
        Assert.NotNull(codec);
        Assert.Equal(320, codec!.Info.Width);
        Assert.Equal(160, codec.Info.Height);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);

        // نسخهٔ WebP باید از PNG اصلی کوچک‌تر باشد (هدف اصلی همین است).
        Assert.True(new FileInfo(path).Length < original.Length);
    }

    [Fact]
    public async Task Create_RefusesToUpscale_ServingOriginalInstead()
    {
        var service = CreateService();
        var original = CreatePng(200, 120);

        var path = await service.GetOrCreateAsync("small.png", _ => OpenBytes(original), "image/png", 800, default);

        Assert.Null(path);
    }

    [Fact]
    public async Task Create_IgnoresWidthOutsideAllowList()
    {
        var service = CreateService();
        var original = CreatePng(1000, 500);

        var path = await service.GetOrCreateAsync("photo.jpg", _ => OpenBytes(original), "image/jpeg", 777, default);

        Assert.Null(path);
        Assert.False(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.webp").Any());
    }

    [Fact]
    public async Task Create_ReusesCachedFile_WithoutReopeningOriginal()
    {
        var service = CreateService();
        var original = CreatePng(1000, 500);

        var first = await service.GetOrCreateAsync("photo.jpg", _ => OpenBytes(original), "image/jpeg", 160, default);
        Assert.NotNull(first);

        var openedAgain = false;
        var second = await service.GetOrCreateAsync("photo.jpg", _ =>
        {
            openedAgain = true;
            return OpenBytes(original);
        }, "image/jpeg", 160, default);

        Assert.Equal(first, second);
        Assert.False(openedAgain);
    }

    [Fact]
    public async Task Create_SkipsAnimatedGif()
    {
        var service = CreateService();

        var path = await service.GetOrCreateAsync("anim.gif", _ => OpenBytes(CreatePng(600, 400)), "image/gif", 320, default);

        Assert.Null(path);
    }

    [Fact]
    public async Task Prune_DeletesVariantsOfImagesThatAreNoLongerPublic()
    {
        var service = CreateService();
        var original = CreatePng(1000, 500);

        var kept = await service.GetOrCreateAsync("kept.png", _ => OpenBytes(original), "image/png", 320, default);
        var orphan = await service.GetOrCreateAsync("removed.png", _ => OpenBytes(original), "image/png", 320, default);
        Assert.NotNull(kept);
        Assert.NotNull(orphan);

        // فایل تازه‌تر از مهلت است: حتی یتیم هم فعلاً حذف نمی‌شود.
        Assert.Equal(0, service.Prune(new HashSet<string> { "kept.png" }, DateTime.UtcNow.AddDays(-30)));
        Assert.True(File.Exists(orphan));

        // با مهلت گذشته، فقط نسخهٔ تصویر غیرعمومی پاک می‌شود.
        var removed = service.Prune(new HashSet<string> { "kept.png" }, DateTime.UtcNow.AddDays(1));

        Assert.Equal(1, removed);
        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public async Task Prune_DeletesLeftoverTempFiles()
    {
        var service = CreateService();
        var original = CreatePng(1000, 500);

        var variant = await service.GetOrCreateAsync("kept.png", _ => OpenBytes(original), "image/png", 320, default);
        Assert.NotNull(variant);

        var tempPath = Path.Combine(Path.GetDirectoryName(variant!)!, "half-written.tmp");
        await File.WriteAllTextAsync(tempPath, "partial");
        File.SetLastWriteTimeUtc(tempPath, DateTime.UtcNow.AddDays(-2));

        var removed = service.Prune(new HashSet<string> { "kept.png" }, DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, removed);
        Assert.False(File.Exists(tempPath));
        Assert.True(File.Exists(variant));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // پاک‌سازی فایل موقت نباید نتیجهٔ تست را عوض کند.
        }
    }
}

using System.Collections.Concurrent;
using SkiaSharp;

namespace Ashianebargh.App.Services;

/// <summary>
/// ساخت و کش نسخه‌های کوچک‌شدهٔ تصاویر محصول برای <c>srcset</c>.
/// <para>
/// چرا فایل روی دیسک: تصاویر اصلی ممکن است چند مگابایت باشند و سرو همان فایل برای
/// کارت ۳۰۰ پیکسلی، پهنای باند موبایل را هدر می‌دهد. نسخه‌ها یک‌بار ساخته و به‌صورت
/// WebP ذخیره می‌شوند (کش طولانی + شناسهٔ تغییرناپذیر تصویر یعنی بی‌نیاز از revalidate).
/// </para>
/// <para>
/// امنیت: فقط عرض‌های مجاز پذیرفته می‌شود (جلوگیری از ساخت بی‌نهایت فایل)، از بزرگ‌کردن
/// تصویر پرهیز می‌شود و سقف پیکسل برای جلوگیری از «بمبِ فشرده‌سازی» اعمال می‌شود.
/// </para>
/// </summary>
public sealed class ImageVariantService
{
    /// <summary>عرض‌های مجاز؛ هر مقدار دیگری در URL نادیده گرفته می‌شود.</summary>
    public static readonly int[] AllowedWidths = [160, 320, 480, 800, 1200];

    /// <summary>سقف پیکسل ورودی (≈ ۴۰ مگاپیکسل) تا یک فایل کوچک، حافظه را نبلعد.</summary>
    private const long MaxPixels = 40_000_000;

    private const int WebpQuality = 82;

    private readonly string _cacheRoot;
    private readonly ILogger<ImageVariantService> _logger;

    // یک قفل به‌ازای هر (شناسه، عرض) تا درخواست‌های هم‌زمان فایل نیمه‌ساخته نسازند.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);

    public ImageVariantService(IConfiguration configuration, ILogger<ImageVariantService> logger)
    {
        var root = configuration["Storage:RootPath"] ?? "AppData";
        _cacheRoot = Path.GetFullPath(Path.Combine(root, "MediaVariants"));
        _logger = logger;
    }

    public static bool IsAllowedWidth(int width) => Array.IndexOf(AllowedWidths, width) >= 0;

    /// <summary>مسیر نسخهٔ کش‌شده (بدون بررسی وجود).</summary>
    public string GetPath(string storageId, int width) =>
        Path.Combine(_cacheRoot, $"{storageId}.{width}.webp");

    /// <summary>
    /// خانه‌تکانی کش: نسخه‌های تصاویری که دیگر عمومی نیستند و فایل‌های موقت
    /// رهاشده پاک می‌شوند. مهلت زمانی (mtime قدیمی‌تر از <paramref name="olderThanUtc"/>)
    /// عمدی است تا رقابت با رندر هم‌زمان یا نوسان ساعت، تصویر تازه را حذف نکند.
    /// </summary>
    /// <returns>تعداد فایل‌های حذف‌شده.</returns>
    public int Prune(IReadOnlySet<string> keepStorageIds, DateTime olderThanUtc)
    {
        if (!Directory.Exists(_cacheRoot)) return 0;

        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(_cacheRoot))
        {
            try
            {
                var name = Path.GetFileName(file);
                var isTemp = name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);

                if (!isTemp && TryParseVariantName(name, out var storageId) && keepStorageIds.Contains(storageId))
                    continue;

                if (File.GetLastWriteTimeUtc(file) >= olderThanUtc) continue;

                File.Delete(file);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // فایل در حال استفاده یا بدون دسترسی: دورهٔ بعد دوباره امتحان می‌شود.
                _logger.LogDebug(ex, "حذف فایل کش تصویر ناموفق بود: {File}", file);
            }
        }

        return removed;
    }

    /// <summary>نام فایل الگوی <c>{storageId}.{width}.webp</c> است؛ شناسه خودش می‌تواند نقطه داشته باشد.</summary>
    private static bool TryParseVariantName(string fileName, out string storageId)
    {
        storageId = string.Empty;
        if (!fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return false;

        var withoutExtension = fileName[..^5];
        var separator = withoutExtension.LastIndexOf('.');
        if (separator <= 0) return false;

        if (!int.TryParse(withoutExtension[(separator + 1)..], out var width) || !IsAllowedWidth(width))
            return false;

        storageId = withoutExtension[..separator];
        return storageId.Length > 0;
    }

    /// <summary>
    /// نسخهٔ کوچک‌شده را برمی‌گرداند؛ اگر لازم نباشد (تصویر اصلی کوچک‌تر است،
    /// فرمن GIF متحرک است یا فایل قابل رمزگشایی نیست) <c>null</c> برمی‌گردد تا
    /// فراخوان همان فایل اصلی را سرو کند.
    /// </summary>
    public async Task<string?> GetOrCreateAsync(
        string storageId,
        Func<CancellationToken, Task<Stream?>> openOriginal,
        string contentType,
        int width,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedWidth(width)) return null;

        // GIF متحرک با کوچک‌کردن، انیمیشن را از دست می‌دهد؛ دست نمی‌زنیم.
        if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return null;

        var targetPath = GetPath(storageId, width);
        if (File.Exists(targetPath)) return targetPath;

        var gate = Locks.GetOrAdd($"{storageId}:{width}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(targetPath)) return targetPath;

            await using var original = await openOriginal(cancellationToken);
            if (original is null) return null;

            var created = await CreateAsync(storageId, original, width, targetPath, cancellationToken);
            return created ? targetPath : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // شکست در ساخت نسخهٔ سبک هرگز نباید نمایش تصویر را بشکند.
            _logger.LogWarning(ex, "ساخت نسخهٔ {Width}px تصویر ناموفق بود: {StorageId}", width, storageId);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<bool> CreateAsync(
        string storageId,
        Stream source,
        int width,
        string targetPath,
        CancellationToken cancellationToken)
    {
        // SKCodec برای خواندن ابعاد و رد کردن فایل‌های غیرتصویری/غول‌پیکر پیش از رمزگشایی.
        Stream seekable = source;
        MemoryStream? buffer = null;
        if (!source.CanSeek)
        {
            buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            seekable = buffer;
        }

        try
        {
            using var codec = SKCodec.Create(seekable);
            if (codec is null) return false;

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0) return false;
            if ((long)info.Width * info.Height > MaxPixels) return false;

            // بزرگ‌کردن تصویر معنا ندارد (هم حجم و هم نرمیِ کاذب)؛ همان اصلی سرو می‌شود.
            if (info.Width <= width) return false;

            seekable.Position = 0;
            using var decoded = SKBitmap.Decode(seekable);
            if (decoded is null) return false;

            var targetHeight = Math.Max(1, (int)Math.Round(decoded.Height * (width / (double)decoded.Width)));
            var targetInfo = new SKImageInfo(width, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var resized = decoded.Resize(targetInfo, new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized is null) return false;

            using var image = SKImage.FromBitmap(resized);
            using var data = image?.Encode(SKEncodedImageFormat.Webp, WebpQuality);
            if (data is null) return false;

            Directory.CreateDirectory(_cacheRoot);

            // نوشتن اتمیک: فایل موقت + جابه‌جایی، تا درخواست هم‌زمان فایل نصفه نبیند.
            var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var file = File.Create(tempPath))
                await using (var encoded = data.AsStream())
                {
                    await encoded.CopyToAsync(file, cancellationToken);
                }

                File.Move(tempPath, targetPath, overwrite: true);
                return true;
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        finally
        {
            if (buffer is not null) await buffer.DisposeAsync();
        }
    }
}

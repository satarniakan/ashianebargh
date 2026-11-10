using Ashianebargh.Domain.Content;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ashianebargh.App.Data;

/// <summary>
/// دادهٔ نمونهٔ توسعه برای صفحه‌های عمومی سایت. فقط با فلگ
/// <c>--seed-test-content</c> و در محیط Development اجرا می‌شود و تکراری نمی‌سازد.
/// </summary>
public static class TestContentSeeder
{
    private sealed record ProductSeed(
        string Slug,
        string Name,
        string Code,
        decimal Price,
        bool IsFeatured,
        string CategorySlug,
        string ShortDescription,
        string Description,
        string[] Tags,
        string ImageFile);

    private sealed record CategorySeed(string Slug, string Name, string Description, int SortOrder);

    private sealed record TagSeed(string Slug, string Name);

    private sealed record PageSeed(string Slug, string Title, int SortOrder, string Content);

    // تصویر لحظه‌ای قدیمی که از قبل روی محصول نمونه ثبت شده بود.
    private const string LegacyBulbImage = "8379a31c-c041-4fe2-9040-8110bbeb4e4e.png";

    private static readonly CategorySeed[] Categories =
    [
        new("key-and-socket", "کلید و پریز", "کلید و پریزهای ساختمانی در طرح‌های مدرن و کلاسیک.", 2),
        new("panel-board", "تابلو برق", "کلیدهای مینیاتوری، تابلوهای توزیع و تجهیزات حفاظتی.", 3),
        new("cable-wire", "کابل و سیم", "کابل مفتول، سیم افشان و کابل‌های صنعتی با استاندارد.", 4),
        new("tools", "ابزار و لوازم نصب", "ابزارهای ایمن و حرفه‌ای برای سیم‌کشی و نصب تجهیزات برقی.", 5),
        new("smart-home", "خانه هوشمند", "کلید، سنسور و تجهیزات هوشمند برای کنترل نور و مصرف انرژی.", 6),
        new("ventilation", "تهویه و هواکش", "هواکش، فن و تجهیزات تهویه برای خانه، دفتر و کارگاه.", 7),
        new("solar", "انرژی خورشیدی", "پنل و اینورتر خورشیدی برای تولید برق پاک و پایدار.", 8)
    ];

    private static readonly TagSeed[] Tags =
    [
        new("recessed", "توکار"),
        new("industrial", "صنعتی"),
        new("residential", "مسکونی"),
        new("smart", "هوشمند"),
        new("outdoor", "محیط باز")
    ];

    private static readonly ProductSeed[] Products =
    [
        new(
            "led-panel-60x60", "پنل نور LED توکار ۶۰×۶۰", "AB-LP6060", 1_850_000, true, "روشنایی",
            "پنل توکار با نور یکنواخت، مصرف کم و عمر طولانی؛ مناسب دفتر، فروشگاه و فضای مسکونی.",
            "پنل نور توکار ۶۰×۶۰ با قاب آلومینیومی باریک و دیفیوزر مات، نور را بدون خیرگی پخش می‌کند.\nمصرف پایین و تولید حرارت کم، این پنل را برای سقف‌های کناف و فضاهای کاری ایده‌آل می‌کند.\nدرایور داخلی با محافظت در برابر نوسان برق عرضه می‌شود.",
            ["led", "recessed"],
            "led-panel.png"),
        new(
            "led-floodlight-100w", "پروژکتور LED ۱۰۰ وات", "AB-FL100", 3_250_000, true, "روشنایی",
            "پروژکتور پرنور با بدنهٔ ضدآب؛ مناسب محوطه، پارکینگ و نورپردازی صنعتی.",
            "پروژکتور ۱۰۰ وات با بدنهٔ آلومینیومی ریخته‌گری‌شده و شیشهٔ سکوریت، گرمای تولیدی را سریع تخلیه می‌کند.\nدرجهٔ حفاظت IP65 آن را برای فضای باز، محوطهٔ ساختمان و سالن‌های صنعتی مناسب می‌کند.\nبازهٔ تنظیم زاویهٔ پایه، نصب روی دیوار و سقف را ساده می‌کند.",
            ["led", "industrial"],
            "led-floodlight.png"),
        new(
            "led-chandelier-ring", "چراغ لوستری LED مدرن", "AB-CH24", 5_900_000, false, "روشنایی",
            "لوستر حلقه‌ای با نور گرم و طراحی مینیمال برای پذیرایی و نشیمن.",
            "لوستر حلقه‌ای مدرن با بدنهٔ فلزی مات و نورپردازی یکنواخت، نقطهٔ تمرکز فضای پذیرایی می‌شود.\nارتفاع آویز قابل تنظیم است و با سقف‌های بلند و کوتاه هماهنگ می‌شود.\nنور گرم آن برای نشیمن و فضای مهمانی دلپذیر است.",
            ["led", "residential"],
            "led-chandelier.png"),
        new(
            "glass-switch-socket", "کلید و پریز شیشه‌ای دوگانه", "AB-GS2", 420_000, true, "key-and-socket",
            "پلیت شیشه‌ای سفید با دو کلید و پریز ارت‌دار؛ ترکیب ساده و مدرن.",
            "پلیت شیشهٔ سکوریت سفید با لبهٔ بدون قاب، نمای تمیزی روی دیوار ایجاد می‌کند.\nمکانیزم کلیدها با کیفیت بالا و صدای کم ساخته شده و پریز ارت‌دار برای لوازم حساس مناسب است.\nنصب آن با قوطی‌های استاندارد ساختمانی سازگار است.",
            ["residential"],
            "glass-switch.png"),
        new(
            "mcb-25a", "کلید مینیاتوری تک‌پل ۲۵ آمپر", "AB-MCB25", 185_000, true, "panel-board",
            "کلید مینیاتوری تک‌پل با قطع سریع؛ حفاظت مدارهای روشنایی و پریز.",
            "کلید مینیاتوری ۲۵ آمپر با منحنی قطع C، از مدار در برابر اضافه‌بار و اتصال کوتاه محافظت می‌کند.\nمکانیزم اهرمی روان و نشانگر وضعیت، عیب‌یابی تابلو را ساده می‌کند.\nروی ریل DIN استاندارد نصب می‌شود.",
            ["industrial"],
            "mcb.png"),
        new(
            "distribution-board-24", "تابلو برق ایستاده ۲۴ خط", "AB-DB24", 8_750_000, false, "panel-board",
            "تابلو ایستاده با درب شفاف و فضای کافی برای کلیدها و شینه‌بندی.",
            "تابلو برق ایستاده ۲۴ خط با بدنهٔ ورق روغنی رنگ‌شده و درب شفاف، دسترسی به کلیدها را بدون باز کردن کامل درب ممکن می‌کند.\nفضای داخلی برای شینه‌بندی، ارت و سیم‌کشی منظم طراحی شده است.\nمناسب واحدهای مسکونی بزرگ و کارگاه‌های کوچک.",
            ["industrial"],
            "distribution-board.png"),
        new(
            "cable-25", "کابل مفتول ۲.۵", "AB-CB25", 1_150_000, false, "cable-wire",
            "کابل مسی مفتول ۲.۵ با عایق استاندارد؛ مناسب سیم‌کشی ثابت.",
            "کابل مفتول مسی ۲.۵ با هادی تمام‌مسی و عایق PVC استاندارد عرضه می‌شود.\nبرای مدارهای روشنایی و پریز در سیم‌کشی ثابت ساختمان مناسب است.\nبسته‌بندی رول کامل، خرید پروژه‌ای را اقتصادی می‌کند.",
            ["industrial"],
            "cable-roll.png"),
        new(
            "flexible-wire-15", "سیم افشان ۱.۵", "AB-FW15", 780_000, false, "cable-wire",
            "سیم افشان مسی ۱.۵ با انعطاف بالا برای مدارهای روشنایی.",
            "سیم افشان ۱.۵ با رشته‌های مسی نرم، انعطاف بالایی برای عبور از لوله و گوشه‌ها دارد.\nعایق مقاوم، آن را برای مدارهای روشنایی و لوازم خانگی مناسب می‌کند.\nدر رنگ‌های استاندارد فاز، نول و ارت قابل سفارش است.",
            ["residential"],
            "flexible-wire.png"),
        new(
            "digital-multimeter", "مولتی‌متر دیجیتال حرفه‌ای", "AB-DMM800", 2_450_000, false, "tools",
            "مولتی‌متر دیجیتال با نمایشگر روشن و اندازه‌گیری دقیق ولتاژ، جریان و مقاومت.",
            "مولتی‌متر دیجیتال با نمایشگر بزرگ و نور پس‌زمینه، خواندن مقدارها را در محیط کم‌نور ساده می‌کند.\nاندازه‌گیری ولتاژ AC/DC، جریان، مقاومت و تست پیوستگی برای عیب‌یابی مدارهای ساختمانی کافی است.\nبدنهٔ لاستیکی مقاوم و پروب‌های استاندارد، استفادهٔ روزمرهٔ تعمیرکار را ایمن‌تر می‌کند.",
            ["industrial"],
            "multimeter.png"),
        new(
            "wire-stripper-auto", "سیم‌لخت‌کن خودکار", "AB-WS01", 690_000, false, "tools",
            "ابزار سیم‌لخت‌کن با تیغهٔ تنظیم‌شده برای سیم‌های افشان و مفتول.",
            "سیم‌لخت‌کن خودکار با تیغهٔ فولادی و فک تنظیم‌شو، روکش سیم را بدون آسیب به هادی جدا می‌کند.\nدستهٔ عایق‌دار و ارگونومیک، فشار دست را کم و کار طولانی را راحت می‌کند.\nبرای سیم‌های ۱.۵ تا ۶ میلی‌متر در نصب روشنایی و پریز مناسب است.",
            ["residential", "industrial"],
            "wire-stripper.png"),
        new(
            "smart-switch-1gang", "کلید هوشمند تک‌پل", "AB-SW1", 1_320_000, true, "smart-home",
            "کلید هوشمند با کنترل از راه دور، زمان‌بندی و هماهنگی با دستیار صوتی.",
            "کلید هوشمند تک‌پل روی همان قوطی استاندارد نصب می‌شود و نیازی به تغییر سیم‌کشی ندارد.\nروشن و خاموش کردن از راه دور، زمان‌بندی و سناریوی ورود و خروج از طریق اپلیکیشن انجام می‌شود.\nمصرف در حالت آماده‌به‌کار بسیار کم است و با کلیدهای مکانیکی موجود هماهنگ می‌شود.",
            ["smart", "residential"],
            "smart-switch.png"),
        new(
            "motion-sensor", "سنسور حرکت هوشمند", "AB-MS01", 980_000, false, "smart-home",
            "سنسور تشخیص حرکت با تنظیم حساسیت و زمان قطع خودکار برای راهرو و پارکینگ.",
            "سنسور حرکت مادون‌قرمز با زاویهٔ پوشش گسترده، حضور افراد را در راهرو و ورودی تشخیص می‌دهد.\nحساسیت و زمان تأخیر قطع با پیچ تنظیم روی بدنه تغییر می‌کند تا روشنایی بی‌دلیل رخ ندهد.\nنصب سقفی و دیواری آن با پایهٔ همراه ساده است و برای فضاهای کم‌تردد مناسب است.",
            ["smart", "industrial"],
            "motion-sensor.png"),
        new(
            "exhaust-fan-20", "هواکش دیواری ۲۰ سانتی", "AB-EF20", 1_890_000, false, "ventilation",
            "هواکش دیواری با موتور کم‌مصرف و تیغهٔ پرفشار برای تخلیهٔ سریع هوا.",
            "هواکش دیواری ۲۰ سانتی با تیغهٔ متعادل، هوای آشپزخانه و سرویس را سریع تخلیه می‌کند.\nموتور کم‌مصرف با بلبرینگ استاندارد، صدای کار را پایین و عمر مفید را بالا نگه می‌دارد.\nشبکهٔ محافظ جلو و دریچهٔ یک‌طرفه، ورود گرد و غبار و حشرات را کاهش می‌دهد.",
            ["residential"],
            "exhaust-fan.png"),
        new(
            "ceiling-fan-4blade", "پنکهٔ سقفی چهارپره", "AB-CF48", 4_300_000, false, "ventilation",
            "پنکهٔ سقفی چهارپره با سه سرعت باد و موتور پرتوان برای فضای بزرگ.",
            "پنکهٔ سقفی چهارپره با پره‌های متعادل، جریان هوا را در پذیرایی و سالن یکنواخت پخش می‌کند.\nسه سرعت باد و کشش زنجیری، تنظیم جریان هوا را بدون کنترل اضافه ممکن می‌کند.\nبدنهٔ رنگ‌شده و موتور خنک‌شونده، کارکرد طولانی و کم‌صدا را تضمین می‌کند.",
            ["residential"],
            "ceiling-fan.png"),
        new(
            "solar-panel-550", "پنل خورشیدی ۵۵۰ وات", "AB-SP550", 9_800_000, true, "solar",
            "پنل مونوکریستال ۵۵۰ وات با بازدهی بالا برای سیستم‌های خانگی و صنعتی.",
            "پنل مونوکریستال ۵۵۰ وات با سلول‌های بازدهی بالا، در فضای محدود توان بیشتری تولید می‌کند.\nقاب آلومینیومی و شیشهٔ سکوریت، پنل را برای نصب روی بام و فضای باز مقاوم می‌کند.\nعملکرد در نور کم و تحمل دمایی بالا، تولید انرژی را در طول سال پایدار نگه می‌دارد.",
            ["outdoor", "industrial"],
            "solar-panel.png"),
        new(
            "solar-inverter-3k", "اینورتر خورشیدی ۳ کیلووات", "AB-INV3K", 24_500_000, false, "solar",
            "اینورتر هیبرید ۳ کیلووات با مانیتورینگ و خروجی پایدار برای سیستم خورشیدی.",
            "اینورتر هیبرید ۳ کیلووات، برق پنل خورشیدی را به برق شهری با کیفیت تبدیل می‌کند.\nپشتیبانی از باتری و مانیتورینگ مصرف، مدیریت انرژی خانه را ممکن می‌سازد.\nحفاظت در برابر اضافه‌بار و اتصال کوتاه، تجهیزات متصل را ایمن نگه می‌دارد.",
            ["outdoor", "industrial"],
            "solar-inverter.png")
    ];

    private static readonly PageSeed[] Pages =
    [
        new("garranty", "گارانتی و خدمات پس از فروش", 2,
            "همهٔ محصولات آشیانه برق با ضمانت اصالت و سلامت فیزیکی کالا عرضه می‌شوند.\nلوازم روشنایی، کلید و پریز تا ۱۸ ماه و تجهیزات صنعتی تا ۱۲ ماه گارانتی تعویض یا تعمیر دارند.\nبرای استفاده از گارانتی، فاکتور خرید و بسته‌بندی سالم محصول را نگه دارید.\nخرابی ناشی از نصب نادرست یا نوسان شدید برق مشمول گارانتی نیست؛ تیم پشتیبانی برای بررسی و راهنمایی در دسترس است."),
        new("shipping", "نحوه ارسال و تحویل", 3,
            "سفارش‌های ثبت‌شده پس از تأیید موجودی و هماهنگی تلفنی، در همان روز کاری بسته‌بندی و آماده‌سازی می‌شوند.\nارسال در تهران با پیک اختصاصی و معمولاً در همان روز یا روز کاری بعد انجام می‌شود.\nارسال به سایر شهرها با باربری مورد اعتماد و بیمهٔ بار انجام می‌شود و معمولاً ۲ تا ۴ روز کاری زمان می‌برد.\nهزینهٔ ارسال پیش از تأیید نهایی سفارش به‌صورت شفاف اعلام می‌شود.\nکالاهای شکستنی با بسته‌بندی محافظ اضافه ارسال می‌شوند؛ در صورت آسیب هنگام تحویل، همان روز با پشتیبانی تماس بگیرید."),
        new("faq", "سوالات متداول", 4,
            "آیا امکان خرید عمده و صدور پیش‌فاکتور وجود دارد؟ بله؛ برای پروژه‌ها و خرید عمده پیش‌فاکتور رسمی صادر می‌شود.\nآیا محصولات ضمانت دارند؟ بله؛ شرایط ضمانت هر محصول در صفحهٔ آن و صفحهٔ گارانتی آمده است.\nچطور از موجودی باخبر شوم؟ کافی است از صفحهٔ تماس، شمارهٔ تماس یا پیام خود را ارسال کنید.\nآیا امکان مرجوعی وجود دارد؟ کالای سالم و بسته‌بندی‌نشده تا ۷ روز، با هماهنگی پشتیبانی قابل مرجوع است.\nآیا نصب و سیم‌کشی هم انجام می‌دهید؟ برای پروژه‌های بزرگ، مشاورهٔ فنی و معرفی نصاب معتبر ارائه می‌شود.")
    ];

    public static async Task SeedAsync(
        AshianebarghDbContext db,
        IFileStorage storage,
        string assetsPath,
        ILogger logger)
    {
        var categories = await db.Set<Category>().ToListAsync();
        var tags = await db.Set<Tag>().ToListAsync();
        var addedCategories = 0;
        var addedTags = 0;
        var addedProducts = 0;
        var addedPages = 0;
        var updatedPages = 0;

        foreach (var seed in Categories)
        {
            if (categories.Any(c => c.Slug == seed.Slug))
                continue;

            var category = new Category
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Description = seed.Description,
                SortOrder = seed.SortOrder,
                IsActive = true
            };
            db.Set<Category>().Add(category);
            categories.Add(category);
            addedCategories++;
        }

        foreach (var seed in Tags)
        {
            if (tags.Any(t => t.Slug == seed.Slug))
                continue;

            var tag = new Tag { Name = seed.Name, Slug = seed.Slug };
            db.Set<Tag>().Add(tag);
            tags.Add(tag);
            addedTags++;
        }

        await db.SaveChangesAsync();

        // دستهٔ روشنایی از قبل وجود دارد؛ اگر نامک آن تغییر کرده باشد، اولین دسته فعال جایگزین است.
        var lighting = categories.FirstOrDefault(c => c.Slug == "روشنایی")
            ?? categories.FirstOrDefault(c => c.Id == 1)
            ?? categories.FirstOrDefault(c => c.IsActive);

        var existingSlugs = await db.Set<Product>().Select(p => p.Slug).ToListAsync();
        var tagMap = tags.ToDictionary(t => t.Slug, StringComparer.Ordinal);
        var categoryMap = categories.ToDictionary(c => c.Slug, StringComparer.Ordinal);

        foreach (var seed in Products)
        {
            if (existingSlugs.Contains(seed.Slug))
                continue;

            Category? category = null;
            if (!categoryMap.TryGetValue(seed.CategorySlug, out category))
                category = lighting;
            if (category is null)
                continue;

            var product = new Product
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Code = seed.Code,
                Category = category,
                ShortDescription = seed.ShortDescription,
                Description = seed.Description,
                Price = seed.Price,
                IsActive = true,
                IsFeatured = seed.IsFeatured,
                SortOrder = 10 + Array.IndexOf(Products, seed)
            };

            foreach (var tagSlug in seed.Tags)
            {
                if (tagMap.TryGetValue(tagSlug, out var tag))
                    product.Tags.Add(tag);
            }

            var imagePath = Path.Combine(assetsPath, seed.ImageFile);
            if (File.Exists(imagePath))
            {
                await using var stream = File.OpenRead(imagePath);
                product.MainImageStorageId = await storage.SaveAsync(
                    ContentStorage.Owner, ContentStorage.ProductImageType,
                    stream, seed.ImageFile, "image/png");
            }
            else
            {
                logger.LogWarning("تصویر نمونهٔ {Image} پیدا نشد؛ محصول بدون تصویر درج می‌شود.", seed.ImageFile);
            }

            db.Set<Product>().Add(product);
            addedProducts++;
        }

        foreach (var seed in Pages)
        {
            var existing = await db.Set<Page>().FirstOrDefaultAsync(p => p.Slug == seed.Slug);
            if (existing is null)
            {
                db.Set<Page>().Add(new Page
                {
                    Title = seed.Title,
                    Slug = seed.Slug,
                    Content = seed.Content,
                    IsPublished = true,
                    ShowInMenu = true,
                    SortOrder = seed.SortOrder
                });
                addedPages++;
                continue;
            }

            if (existing.Title == seed.Title
                && existing.Content == seed.Content
                && existing.SortOrder == seed.SortOrder
                && existing.IsPublished
                && existing.ShowInMenu)
            {
                continue;
            }

            existing.Title = seed.Title;
            existing.Content = seed.Content;
            existing.SortOrder = seed.SortOrder;
            existing.IsPublished = true;
            existing.ShowInMenu = true;
            updatedPages++;
        }

        await db.SaveChangesAsync();

        // تصویر قدیمیِ محصول نمونه با تصویر هم‌سبک جایگزین می‌شود.
        var bulbFile = Path.Combine(assetsPath, "bulb.png");
        var legacyProduct = await db.Set<Product>()
            .FirstOrDefaultAsync(p => p.MainImageStorageId == LegacyBulbImage);
        if (legacyProduct is not null && File.Exists(bulbFile))
        {
            await using var bulbStream = File.OpenRead(bulbFile);
            legacyProduct.MainImageStorageId = await storage.SaveAsync(
                ContentStorage.Owner, ContentStorage.ProductImageType,
                bulbStream, "bulb.png", "image/png");
            await db.SaveChangesAsync();
            await storage.DeleteAsync(
                ContentStorage.Owner, ContentStorage.ProductImageType, LegacyBulbImage);
            logger.LogInformation("تصویر قدیمی محصول نمونه با تصویر جایگزین عوض شد.");
        }

        logger.LogInformation(
            "دادهٔ نمونهٔ سایت: {Categories} دسته، {Tags} برچسب، {Products} محصول، {AddedPages} صفحهٔ جدید و {UpdatedPages} صفحهٔ به‌روزشده.",
            addedCategories, addedTags, addedProducts, addedPages, updatedPages);
    }
}

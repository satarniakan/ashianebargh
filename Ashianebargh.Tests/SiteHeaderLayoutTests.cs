using Ashianebargh.App.Services;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون «باکس جست‌وجو در سربرگ ناپدید می‌شود». سربرگ یک سطر nowrap است؛ وقتی مجموع
/// برچسب‌های منو (به‌ویژه فارسی و منوی پویای دیتابیس) از بودجه بیشتر می‌شد، فشرده‌سازی
/// به .site-search می‌خورد و چون آن overflow:hidden دارد باکس تا عرض صفر جمع و
/// عملاً غیب می‌شد — پس با تغییر زبان «ناپدید شدن» به نظر می‌رسید. حالا منوی بلند
/// به سطر دوم می‌رود و جست‌وجو/دکمه‌ها ثابت می‌مانند.
/// </summary>
public class SiteHeaderLayoutTests
{
    private static readonly string[] TemplateMenu = ["صفحه اصلی", "محصولات", "درباره ما", "تماس با ما"];
    private static readonly string[] EnglishMenu = ["Home", "Products", "About", "Contact"];

    // منوی واقعیِ مصرف‌کننده: سه صفحهٔ پویای دیتابیس به منوی پایه اضافه شده است.
    private static readonly string[] LongPersianMenu =
    [
        "صفحه اصلی", "محصولات", "گارانتی و خدمات پس از فروش",
        "نحوه ارسال و تحویل", "سوالات متداول", "درباره ما", "تماس با ما"
    ];

    [Fact]
    public void ShortMenu_StaysOnOneRow()
    {
        Assert.False(SiteHeaderLayout.IsMenuTooWideForOneRow(TemplateMenu));
        Assert.False(SiteHeaderLayout.IsMenuTooWideForOneRow(EnglishMenu));
    }

    [Fact]
    public void LongPersianMenu_MovesToSecondRow() =>
        Assert.True(SiteHeaderLayout.IsMenuTooWideForOneRow(LongPersianMenu));

    [Fact]
    public void BlankLabels_AreNotCountedAsWidth()
    {
        // ترجمهٔ انگلیسیِ نبود عنوان خالی است؛ نباید بودجه را پر کند.
        var withBlanks = TemplateMenu.Concat(["", "   ", null!]);

        Assert.False(SiteHeaderLayout.IsMenuTooWideForOneRow(withBlanks));
    }

    [Fact]
    public void Decision_ChangesAtTheBudgetBoundary()
    {
        Assert.False(SiteHeaderLayout.IsMenuTooWideForOneRow(Enumerable.Repeat(new string('ا', 9), 4)));
        Assert.True(SiteHeaderLayout.IsMenuTooWideForOneRow(Enumerable.Repeat(new string('ا', 10), 4)));
    }
}

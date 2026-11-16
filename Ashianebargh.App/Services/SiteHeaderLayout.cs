namespace Ashianebargh.App.Services;

/// <summary>
/// تصمیم چیدمان سربرگ سایت. سربرگ یک سطر است (برند + منو + جست‌وجو + دکمه‌ها) و
/// در تنگنا همهٔ عناصر فشرده می‌شوند؛ باکس جست‌وجو با overflow:hidden تا عرض صفر
/// جمع می‌شد و برای کاربر «ناپدید» به نظر می‌رسید. چون جا شدن به طول برچسب‌های منو
/// بستگی دارد و CSS از عرض محتوا بی‌خبر است، اینجا برآورد می‌شود تا منوی بلند به
/// سطر دوم برود و جست‌وجو و دکمه‌ها سر جای خود بمانند.
/// </summary>
public static class SiteHeaderLayout
{
    // برآورد عرض هر لینک: ۲۶px حاشیهٔ چپ/راست + ۸px به ازای هر نویسه (فونت ۱۴ وزیرمتن).
    private const int LinkPaddingPx = 26;
    private const int CharWidthPx = 8;

    // بودجهٔ منو در باریک‌ترین حالت دسکتاپ (۱۰۸۱px) پس از برند، جست‌وجو و دکمه‌ها.
    private const int OneRowBudgetPx = 420;

    /// <summary>آیا منو در یک سطر جا نمی‌شود و باید به سطر دوم برود؟</summary>
    public static bool IsMenuTooWideForOneRow(IEnumerable<string> labels)
    {
        var widths = labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim().Length * CharWidthPx + LinkPaddingPx)
            .ToList();

        return widths.Sum() > OneRowBudgetPx;
    }
}

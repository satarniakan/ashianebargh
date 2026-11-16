using System.Globalization;

namespace Ashianebargh.App.Services;

/// <summary>
/// زبان رابط عمومی سایت. زبان از کوکی site-lang خوانده و توسط
/// UseRequestLocalization به CultureInfo درخواست (و مدار Blazor) تبدیل می‌شود.
/// متن‌های دیتابیس (محصول، صفحه) ترجمه نمی‌شوند.
/// </summary>
public sealed class SiteLanguage
{
    public const string CookieName = "site-lang";

    private static readonly Dictionary<string, string> Persian = new(StringComparer.Ordinal)
    {
        ["brand.tagline"] = "تأمین تجهیزات برق و روشنایی",
        ["nav.home"] = "صفحه اصلی",
        ["nav.products"] = "محصولات",
        ["nav.about"] = "درباره ما",
        ["nav.contact"] = "تماس با ما",
        ["nav.skipToContent"] = "پرش به محتوای اصلی",
        ["action.quote"] = "درخواست استعلام",
        ["action.dashboard"] = "پنل مدیریت",
        ["action.login"] = "ورود",
        ["tool.language"] = "تغییر زبان",
        ["tool.theme"] = "تغییر تم روشن/تاریک",
        ["aria.mainMenu"] = "منوی اصلی",
        ["aria.breadcrumb"] = "مسیر",
        ["aria.categories"] = "دسته‌بندی محصولات",
        ["aria.pagination"] = "صفحه‌بندی",
        ["footer.fallback"] = "همراه شما برای انتخاب درست",
        ["footer.catalog"] = "دانلود کاتالوگ",
        ["home.title"] = "آشیانه برق | معرفی محصولات",
        ["home.eyebrow"] = "تأمین تخصصی تجهیزات برق",
        ["home.lead"] = "معرفی و عرضهٔ محصولات برقی و روشنایی؛ همراه با راهنمایی برای انتخاب درست، پشتیبانی فنی و ارسال به سراسر کشور.",
        ["home.viewProducts"] = "مشاهدهٔ محصولات",
        ["home.contact"] = "تماس با ما",
        ["feat.variety"] = "تنوع محصولات",
        ["feat.varietySub"] = "برقی و روشنایی",
        ["feat.advice"] = "مشاورهٔ فنی",
        ["feat.adviceSub"] = "برای انتخاب درست",
        ["feat.shipping"] = "ارسال سراسری",
        ["feat.shippingSub"] = "به همهٔ شهرها",
        ["feat.support"] = "پشتیبانی",
        ["feat.supportSub"] = "پس از فروش",
        ["home.categories"] = "دسته‌بندی محصولات",
        ["home.featured"] = "محصولات ویژه",
        ["home.emptyFeatured"] = "هنوز محصول ویژه‌ای ثبت نشده است.",
        ["common.allProducts"] = "همهٔ محصولات",
        ["common.home"] = "خانه",
        ["card.callForPrice"] = "برای قیمت تماس بگیرید",
        ["products.title"] = "محصولات | آشیانه برق",
        ["products.h1"] = "محصولات",
        ["products.lead"] = "فهرست محصولات موجود؛ برای مشاهدهٔ جزئیات روی هر کارت کلیک کنید.",
        ["side.categories"] = "دسته‌بندی‌ها",
        ["side.tags"] = "برچسب‌ها",
        ["side.all"] = "همه",
        ["products.empty"] = "محصولی با این فیلتر پیدا نشد.",
        ["products.clear"] = "حذف فیلترها",
        ["search.placeholder"] = "جست‌وجوی محصول…",
        ["search.button"] = "جست‌وجو",
        ["search.resultsFor"] = "نتایج جست‌وجو برای «{0}»",
        ["search.noResult"] = "نتیجه‌ای برای «{0}» یافت نشد.",
        ["detail.files"] = "فایل‌های پیوست",
        ["detail.filesEmpty"] = "برای این محصول فایلی پیوست نشده است.",
        ["detail.download"] = "دانلود",
        ["pager.previous"] = "قبلی",
        ["pager.next"] = "بعدی",
        ["detail.notFoundTitle"] = "محصول یافت نشد | آشیانه برق",
        ["detail.notFound"] = "این محصول وجود ندارد یا فعلاً غیرفعال است.",
        ["detail.backToProducts"] = "بازگشت به محصولات",
        ["detail.codePrefix"] = "کد محصول:",
        ["detail.callForPrice"] = "برای اطلاع از قیمت تماس بگیرید",
        ["detail.callUs"] = "تماس بگیرید",
        ["detail.quote"] = "استعلام و سفارش",
        ["detail.other"] = "سایر محصولات",
        ["detail.specs"] = "مشخصات محصول",
        ["detail.summary"] = "اطلاعات کلی",
        ["detail.attribute"] = "ویژگی",
        ["detail.value"] = "مقدار",
        ["detail.code"] = "کد محصول",
        ["detail.category"] = "دسته‌بندی",
        ["detail.price"] = "قیمت",
        ["detail.tags"] = "برچسب‌ها",
        ["detail.description"] = "توضیحات",
        ["contact.title"] = "تماس با ما | آشیانه برق",
        ["contact.h1"] = "تماس با ما",
        ["contact.lead"] = "برای استعلام قیمت، مشاورهٔ فنی یا پیگیری سفارش، از راه‌های زیر با ما در ارتباط باشید.",
        ["contact.infoTitle"] = "اطلاعات تماس ما",
        ["contact.address"] = "نشانی",
        ["contact.phone"] = "تلفن",
        ["contact.mobile"] = "همراه",
        ["contact.email"] = "ایمیل",
        ["contact.hours"] = "ساعات کاری",
        ["contact.formTitle"] = "برای ما پیام بفرستید",
        ["contact.success"] = "پیام شما ثبت شد؛ در اولین فرصت پاسخ می‌دهیم.",
        ["contact.sendAnother"] = "ارسال پیام دیگر",
        ["contact.map"] = "موقعیت ما",
        ["contact.mapTitle"] = "نقشهٔ محل شرکت",
        ["social.instagram"] = "اینستاگرام",
        ["social.telegram"] = "تلگرام",
        ["social.whatsapp"] = "واتس‌اپ",
        ["form.name"] = "نام و نام خانوادگی",
        ["form.phone"] = "شماره تماس",
        ["form.email"] = "ایمیل (اختیاری)",
        ["form.subject"] = "موضوع (اختیاری)",
        ["form.message"] = "متن پیام",
        ["form.sending"] = "در حال ارسال…",
        ["form.send"] = "ارسال پیام",
        ["about.title"] = "درباره ما | آشیانه برق",
        ["about.empty"] = "محتوای «درباره ما» هنوز تنظیم نشده است.",
        ["page.notFoundTitle"] = "صفحه یافت نشد | آشیانه برق",
        ["page.notFound"] = "صفحه‌ای با این آدرس وجود ندارد یا منتشر نشده است.",
        ["page.backHome"] = "بازگشت به صفحهٔ اول",
        ["page.empty"] = "متنی برای این صفحه ثبت نشده است.",
        ["notFound.title"] = "صفحه پیدا نشد",
        ["notFound.lead"] = "آدرسی که دنبالش بودید وجود ندارد یا جابه‌جا شده است. از مسیرهای زیر ادامه دهید.",
        ["notFound.home"] = "صفحهٔ اصلی",
        ["nav.menu"] = "منو",
        ["nav.close"] = "بستن منو",
        ["sort.label"] = "مرتب‌سازی",
        ["sort.default"] = "پیشنهاد ما",
        ["sort.newest"] = "جدیدترین",
        ["sort.priceAsc"] = "ارزان‌ترین",
        ["sort.priceDesc"] = "گران‌ترین",
        ["sort.name"] = "بر اساس نام",
        ["filter.active"] = "فیلترهای فعال",
        ["filter.remove"] = "حذف این فیلتر",
        ["pager.page"] = "صفحه",
        ["pager.of"] = "از",
        ["category.lead"] = "همهٔ محصولات فعال این دسته در یک نگاه.",
        ["category.empty"] = "هنوز محصولی در این دسته ثبت نشده است.",
        ["detail.related"] = "محصولات مرتبط",
        ["detail.gallery"] = "تصاویر محصول",
        ["detail.galleryAlt"] = "تصویر {0} از {1} — {2}",
        ["home.statsProducts"] = "محصول فعال",
        ["home.statsCategories"] = "دسته‌بندی تخصصی",
        ["home.statsSupport"] = "پشتیبانی و مشاوره",
        ["home.aboutTitle"] = "چرا آشیانه برق؟",
        ["home.aboutLead"] = "تجهیزات برق و روشنایی را با مشاورهٔ فنی، تأمین مطمئن و پشتیبانی پس از فروش عرضه می‌کنیم تا انتخاب شما درست از آب دربیاید.",
        ["home.aboutMore"] = "بیشتر دربارهٔ ما",
        ["home.ctaTitle"] = "برای انتخاب درست، مشاورهٔ رایگان بگیرید",
        ["home.ctaLead"] = "کارشناسان ما در انتخاب مدل، محاسبهٔ بار و تأمین تجهیزات کنار شما هستند.",
        ["home.ctaCall"] = "تماس با کارشناس",
        ["contact.quoteNotice"] = "درخواست شما برای محصول «{0}» است؛ متن پیام آماده شده و می‌توانید آن را ویرایش کنید.",
        ["contact.subjectQuote"] = "استعلام قیمت: {0}",
        ["contact.quoteMessage"] = "سلام، برای محصول «{0}» استعلام قیمت و موجودی دارم."
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["brand.tagline"] = "Electrical & lighting equipment",
        ["nav.home"] = "Home",
        ["nav.products"] = "Products",
        ["nav.about"] = "About",
        ["nav.contact"] = "Contact",
        ["nav.skipToContent"] = "Skip to main content",
        ["action.quote"] = "Request a quote",
        ["action.dashboard"] = "Dashboard",
        ["action.login"] = "Sign in",
        ["tool.language"] = "Switch language",
        ["tool.theme"] = "Toggle light/dark theme",
        ["aria.mainMenu"] = "Main menu",
        ["aria.breadcrumb"] = "Breadcrumb",
        ["aria.categories"] = "Product categories",
        ["aria.pagination"] = "Pagination",
        ["footer.fallback"] = "Helping you make the right choice",
        ["footer.catalog"] = "Download catalog",
        ["home.title"] = "Ashianebargh | Products",
        ["home.eyebrow"] = "Specialized electrical supplies",
        ["home.lead"] = "Electrical and lighting products with expert guidance, technical support, and nationwide delivery.",
        ["home.viewProducts"] = "Browse products",
        ["home.contact"] = "Contact us",
        ["feat.variety"] = "Wide range",
        ["feat.varietySub"] = "Electrical & lighting",
        ["feat.advice"] = "Technical advice",
        ["feat.adviceSub"] = "For the right choice",
        ["feat.shipping"] = "Nationwide delivery",
        ["feat.shippingSub"] = "To every city",
        ["feat.support"] = "Support",
        ["feat.supportSub"] = "After sales",
        ["home.categories"] = "Product categories",
        ["home.featured"] = "Featured products",
        ["home.emptyFeatured"] = "No featured products yet.",
        ["common.allProducts"] = "All products",
        ["common.home"] = "Home",
        ["card.callForPrice"] = "Call for price",
        ["products.title"] = "Products | Ashianebargh",
        ["products.h1"] = "Products",
        ["products.lead"] = "Browse our available products; click any card to see the details.",
        ["side.categories"] = "Categories",
        ["side.tags"] = "Tags",
        ["side.all"] = "All",
        ["products.empty"] = "No products match this filter.",
        ["products.clear"] = "Clear filters",
        ["search.placeholder"] = "Search products…",
        ["search.button"] = "Search",
        ["search.resultsFor"] = "Search results for “{0}”",
        ["search.noResult"] = "No results found for “{0}”.",
        ["detail.files"] = "Attachments",
        ["detail.filesEmpty"] = "No files are attached to this product.",
        ["detail.download"] = "Download",
        ["pager.previous"] = "Previous",
        ["pager.next"] = "Next",
        ["detail.notFoundTitle"] = "Product not found | Ashianebargh",
        ["detail.notFound"] = "This product doesn't exist or is currently inactive.",
        ["detail.backToProducts"] = "Back to products",
        ["detail.codePrefix"] = "Product code:",
        ["detail.callForPrice"] = "Call us for pricing",
        ["detail.callUs"] = "Call us",
        ["detail.quote"] = "Request & order",
        ["detail.other"] = "More products",
        ["detail.specs"] = "Specifications",
        ["detail.summary"] = "General information",
        ["detail.attribute"] = "Attribute",
        ["detail.value"] = "Value",
        ["detail.code"] = "Product code",
        ["detail.category"] = "Category",
        ["detail.price"] = "Price",
        ["detail.tags"] = "Tags",
        ["detail.description"] = "Description",
        ["contact.title"] = "Contact | Ashianebargh",
        ["contact.h1"] = "Contact us",
        ["contact.lead"] = "For pricing, technical advice, or order follow-up, reach us through the channels below.",
        ["contact.infoTitle"] = "Our contact details",
        ["contact.address"] = "Address",
        ["contact.phone"] = "Phone",
        ["contact.mobile"] = "Mobile",
        ["contact.email"] = "Email",
        ["contact.hours"] = "Working hours",
        ["contact.formTitle"] = "Send us a message",
        ["contact.success"] = "Your message was received. We'll get back to you soon.",
        ["contact.sendAnother"] = "Send another message",
        ["contact.map"] = "Our location",
        ["contact.mapTitle"] = "Company location map",
        ["social.instagram"] = "Instagram",
        ["social.telegram"] = "Telegram",
        ["social.whatsapp"] = "WhatsApp",
        ["form.name"] = "Full name",
        ["form.phone"] = "Phone number",
        ["form.email"] = "Email (optional)",
        ["form.subject"] = "Subject (optional)",
        ["form.message"] = "Message",
        ["form.sending"] = "Sending…",
        ["form.send"] = "Send message",
        ["about.title"] = "About | Ashianebargh",
        ["about.empty"] = "About content hasn't been set yet.",
        ["page.notFoundTitle"] = "Page not found | Ashianebargh",
        ["page.notFound"] = "This page doesn't exist or hasn't been published.",
        ["page.backHome"] = "Back to home",
        ["page.empty"] = "No content has been entered for this page.",
        ["notFound.title"] = "Page not found",
        ["notFound.lead"] = "The address you followed doesn't exist or has moved. Continue from one of the links below.",
        ["notFound.home"] = "Home page",
        ["nav.menu"] = "Menu",
        ["nav.close"] = "Close menu",
        ["sort.label"] = "Sort",
        ["sort.default"] = "Recommended",
        ["sort.newest"] = "Newest",
        ["sort.priceAsc"] = "Lowest price",
        ["sort.priceDesc"] = "Highest price",
        ["sort.name"] = "By name",
        ["filter.active"] = "Active filters",
        ["filter.remove"] = "Remove this filter",
        ["pager.page"] = "Page",
        ["pager.of"] = "of",
        ["category.lead"] = "All active products in this category at a glance.",
        ["category.empty"] = "No products have been added to this category yet.",
        ["detail.related"] = "Related products",
        ["detail.gallery"] = "Product images",
        ["detail.galleryAlt"] = "Image {0} of {1} — {2}",
        ["home.statsProducts"] = "active products",
        ["home.statsCategories"] = "specialist categories",
        ["home.statsSupport"] = "support & advice",
        ["home.aboutTitle"] = "Why Ashianebargh?",
        ["home.aboutLead"] = "We supply electrical and lighting equipment with technical advice, reliable sourcing, and after-sales support so your choice is the right one.",
        ["home.aboutMore"] = "More about us",
        ["home.ctaTitle"] = "Get free advice for the right choice",
        ["home.ctaLead"] = "Our specialists help with model selection, load calculation, and sourcing.",
        ["home.ctaCall"] = "Talk to a specialist",
        ["contact.quoteNotice"] = "Your request is for “{0}”; the message is pre-filled and you can edit it.",
        ["contact.subjectQuote"] = "Price request: {0}",
        ["contact.quoteMessage"] = "Hello, I'd like a price and availability quote for “{0}”."
    };

    public SiteLanguage()
    {
        IsEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("en", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEnglish { get; }

    public string Code => IsEnglish ? "en" : "fa";

    public string Dir => IsEnglish ? "ltr" : "rtl";

    public string this[string key] =>
        (IsEnglish ? English : Persian).TryGetValue(key, out var value) ? value : key;

    public string ProductCount(int count) =>
        IsEnglish ? $"{count} {(count == 1 ? "product" : "products")}" : $"{count} محصول";

    public string PageOf(int page, int total) =>
        IsEnglish ? $"Page {page} of {total}" : $"صفحهٔ {page} از {total}";

    public string FormatPrice(decimal price) =>
        IsEnglish
            ? price.ToString("N0", CultureInfo.InvariantCulture) + " Toman"
            : Ashianebargh.Application.Helpers.CurrencyFormatter.ToToman(price);

    /// <summary>اندازهٔ فایل با واحد خوانا (KB/MB) برای نمایش در فهرست پیوست‌ها.</summary>
    public string FileSize(long bytes)
    {
        if (bytes <= 0) return "—";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024d):0.#} KB";
        return $"{(bytes / (1024d * 1024d)):0.#} MB";
    }

    /// <summary>آیکون «رفتن به جلو» که در RTL چپ و در LTR راست است.</summary>
    public string ForwardIcon => IsEnglish ? "bi-arrow-right" : "bi-arrow-left";

    public string PreviousIcon => IsEnglish ? "bi-chevron-left" : "bi-chevron-right";

    public string NextIcon => IsEnglish ? "bi-chevron-right" : "bi-chevron-left";
}

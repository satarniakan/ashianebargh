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
        ["page.empty"] = "متنی برای این صفحه ثبت نشده است."
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["brand.tagline"] = "Electrical & lighting equipment",
        ["nav.home"] = "Home",
        ["nav.products"] = "Products",
        ["nav.about"] = "About",
        ["nav.contact"] = "Contact",
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
        ["page.empty"] = "No content has been entered for this page."
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

    /// <summary>آیکون «رفتن به جلو» که در RTL چپ و در LTR راست است.</summary>
    public string ForwardIcon => IsEnglish ? "bi-arrow-right" : "bi-arrow-left";

    public string PreviousIcon => IsEnglish ? "bi-chevron-left" : "bi-chevron-right";

    public string NextIcon => IsEnglish ? "bi-chevron-right" : "bi-chevron-left";
}

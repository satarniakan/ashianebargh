// اینتراپ ادیتور متن غنی صفحات (Jodit 4.20.2 در wwwroot/lib/jodit).
// بارگذاری تنبل هم‌الگوی priceInput.js: ناوبری افزانه اسکریپت لایوت مقصد را اجرا نمی‌کند،
// پس دارایی‌ها همین‌جا هنگام ساخت اولین ادیتور تزریق می‌شوند.

function ensureCss() {
    if (document.getElementById('ash-jodit-css')) return;
    const link = document.createElement('link');
    link.id = 'ash-jodit-css';
    link.rel = 'stylesheet';
    link.href = '/lib/jodit/jodit.min.css';
    document.head.appendChild(link);
}

let joditPromise = null;
function ensureJodit() {
    if (window.Jodit) return Promise.resolve();
    // بستهٔ ES5 یک UMD است و نمونهٔ سراسری Jodit را می‌سازد؛ با تگ script بارگذاری می‌شود.
    joditPromise ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = '/lib/jodit/jodit.min.js';
        script.onload = async () => {
            try {
                // بستهٔ فارسی داخل باندل ES5 نیست؛ فایل ESM فقط یک شیء ترجمه است (بدون import)
                const fa = await import('/lib/jodit/fa.js');
                window.Jodit.extendLang({ fa: fa.default });
                // سه لغزش در بستهٔ فارسیِ بالادستی: tooltipهای تورفتگی جابه‌جا و غلط املایی «باز کردن»
                window.Jodit.lang.fa['Increase Indent'] = 'افزودن فرورفتگی';
                window.Jodit.lang.fa['Decrease Indent'] = 'کاهش فرورفتگی';
                window.Jodit.lang.fa['Open link in new tab'] = 'باز کردن لینک در تب جدید';
                // کلیدهای جامانده/غلط در بستهٔ فارسی (فرم پیوند)؛ کلیدها باید دقیقاً مطابق باندل باشند
                window.Jodit.lang.fa['Class name'] = 'نام کلاس';
                window.Jodit.lang.fa['No follow'] = 'بدون دنبال‌کردن';
                window.Jodit.lang.fa['URL'] = 'نشانی';
                resolve();
            } catch (err) {
                reject(err);
            }
        };
        script.onerror = () => reject(new Error('بارگذاری Jodit ناموفق بود'));
        document.head.appendChild(script);
    });
    return joditPromise;
}

const instances = new WeakMap();

export async function createEditor(element, dotNetRef, options) {
    ensureCss();
    await ensureJodit();
    await destroyEditor(element);

    const jodit = new window.Jodit(element, {
        language: 'fa',
        direction: options.direction === 'ltr' ? 'ltr' : 'rtl',
        theme: document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'default',
        height: 380,
        minHeight: 260,
        value: options.value || '',
        placeholder: options.placeholder || 'چیزی بنویسید',
        buttons: [
            'undo', 'redo', '|',
            'bold', 'italic', 'underline', 'strikethrough', '|',
            'ul', 'ol', '|',
            'font', 'fontsize', 'brush', '|',
            'paragraph', 'align', '|',
            'link', 'image', 'table', '|',
            'hr', 'indent', 'outdent', '|',
            'source', 'fullsize'
        ],
        showXPathInStatusbar: false,
        showCharsCounter: false,
        showWordsCounter: false,
        // محتوا گاهی از سایت بیرونی کپی می‌شود؛ بدون پرسش، HTML پاک‌شده درج شود
        askBeforePasteHTML: false,
        askBeforePasteFromWord: false,
        defaultActionOnPaste: 'insert_clear_html'
        // آپلود تصویر عمداً فعال نیست: endpoint آپلود عمومی نداریم و SafeUrl هم data: را رد می‌کند؛
        // دکمهٔ تصویر فقط تب URL را نشان می‌دهد (تب آپلود تنها با uploader.url ظاهر می‌شود).
    });

    // گزینهٔ value در config خوانده نمی‌شود؛ مقدار اولیه فقط از setter می‌نشیند
    if (options.value)
        jodit.value = options.value;

    // تم فقط هنگام ساخت خوانده می‌شود؛ تغییر تم پنل باید زنده روی ظرف ادیتور بنشیند
    // (پاپاپ‌های بعدی از o.theme می‌خوانند). تم داخلی Jodit همین دو کلاس است.
    const applyTheme = () => {
        const theme = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'default';
        jodit.o.theme = theme;
        jodit.container.classList.remove('jodit_theme_default', 'jodit_theme_dark');
        jodit.container.classList.add('jodit_theme_' + theme);
    };
    const themeObserver = new MutationObserver(applyTheme);
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });

    let timer = null;
    const flush = () => {
        timer = null;
        dotNetRef.invokeMethodAsync('UpdateValue', jodit.value).catch(() => { });
    };

    jodit.e.on('change', () => {
        clearTimeout(timer);
        timer = setTimeout(flush, 300);
    });
    jodit.e.on('blur', () => {
        clearTimeout(timer);
        flush();
    });

    instances.set(element, { jodit, flush, themeObserver });
}

export async function destroyEditor(element) {
    const entry = instances.get(element);
    if (!entry) return;
    instances.delete(element);
    try {
        entry.themeObserver.disconnect();
        entry.flush();
        entry.jodit.destruct();
    } catch {
        // مدار ممکن است بسته باشد؛ تمیزکاری سمت JS دیگر معنا ندارد
    }
}
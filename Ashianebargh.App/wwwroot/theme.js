// تم روشن/تاریک — باید در <head> و قبل از رندر بدنه بارگذاری شود تا صفحه بدون پرش رنگ بالا بیاید
(function () {
    const KEY = 'ashianebargh-theme';
    const docEl = document.documentElement;

    // آیکون کلید تم همیشه تم «مقابل» را نشان می‌دهد (در تاریک: خورشید، در روشن: ماه)
    function syncToggleIcons(theme) {
        document.querySelectorAll('.theme-toggle i').forEach((icon) => {
            icon.className = 'bi ' + (theme === 'dark' ? 'bi-sun-fill' : 'bi-moon-stars-fill');
        });
    }

    function apply(theme) {
        if (!theme) theme = 'light';
        docEl.setAttribute('data-theme', theme);
        docEl.setAttribute('data-bs-theme', theme);
        syncToggleIcons(theme);
        // site.js برای همگام‌سازی aria-pressed دکمه‌ها به این رویداد گوش می‌دهد.
        document.dispatchEvent(new CustomEvent('ashianebargh:theme', { detail: { theme } }));
    }

    // دسترسی به localStorage در حالت خصوصی/وب‌ویوهای بسته قابل پرتاب استثناست؛
    // اگر اینجا خطا بدهد کل themeInterop تعریف نمی‌شود و دکمهٔ تم بی‌کار می‌ماند.
    const readStored = () => {
        try { return localStorage.getItem(KEY); } catch { return null; }
    };
    const writeStored = (theme) => {
        try { localStorage.setItem(KEY, theme); } catch { /* بدون ماندگاری، تم فقط برای همین صفحه عوض می‌شود */ }
    };

    // تم ذخیره‌شده یا تم سیستم‌عامل
    const saved = readStored();
    const systemDark = window.matchMedia('(prefers-color-scheme: dark)');
    const initialTheme = saved === 'dark' || saved === 'light' ? saved : (systemDark.matches ? 'dark' : 'light');
    apply(initialTheme);

    // آیکون‌ها و خودِ تم بعد از رندر بدنه یکبار دوباره اعمال می‌شوند (ریدایرکت‌های
    // خطا و رندر مجدد می‌توانند data-theme را پاک کنند).
    document.addEventListener('DOMContentLoaded', () => apply(readStored() || initialTheme));

    // اگر کاربر تم دستی انتخاب نکرده باشد، تغییر سیستم‌عامل زنده اعمال شود
    if (saved !== 'dark' && saved !== 'light') {
        systemDark.addEventListener('change', (e) => {
            if (readStored() !== 'dark' && readStored() !== 'light') {
                apply(e.matches ? 'dark' : 'light');
            }
        });
    }

    // Blazor هنگام ناوبری، <html> را از پاسخ تازه می‌سازد و data-theme را پاک می‌کند.
    // رویداد navigate زودتر از این جایگزینی اجرا می‌شود، پس خودِ ویژگی رصد می‌شود تا
    // تم بعد از هر ناوبری و رندر مجدد همان چیزی بماند که کاربر انتخاب کرده است.
    let selfApply = false;
    new MutationObserver(() => {
        if (selfApply) return;
        const wanted = readStored() || initialTheme;
        if (docEl.getAttribute('data-theme') !== wanted) {
            selfApply = true;
            apply(wanted);
            selfApply = false;
        }
    }).observe(docEl, { attributes: true, attributeFilter: ['data-theme', 'data-bs-theme'] });

    window.themeInterop = {
        get: () => docEl.getAttribute('data-theme') || 'light',
        set: (theme) => {
            writeStored(theme);
            apply(theme);
            return theme;
        },
        reapply: () => {
            const t = readStored() || initialTheme;
            apply(t);
            return t;
        },
        toggle: () => {
            const next = window.themeInterop.get() === 'dark' ? 'light' : 'dark';
            return window.themeInterop.set(next);
        }
    };
})();

// فراخوانی ساده‌ی endpoint های JSON برای کامپوننت‌های تعاملی (مثل زنگ اعلان‌ها)
// توکن ضدجعل (CSRF) از کوکی __Host-XSRF-TOKEN یا __XSRF-TOKEN خوانده و در هدر
// X-XSRF-TOKEN فرستاده می‌شود؛ endpointهای کوکیِ POST بدون آن 400 می‌گیرند.
// اگر کوکی هنوز ست نشده باشد، اول از /api/xsrf-token گرفته می‌شود (lazy).
window.ashianebarghApi = {
    get: async (url) => (await fetch(url)).json(),
    post: async (url) => {
        let token = window.ashianebarghApi.getXsrfToken();
        if (!token) {
            try { await fetch('/api/xsrf-token'); } catch { /* شبکه قطع است؛ همان بدون توکن تلاش می‌شود */ }
            token = window.ashianebarghApi.getXsrfToken();
        }
        await fetch(url, {
            method: 'POST',
            headers: token ? { 'X-XSRF-TOKEN': token } : {}
        });
    },
    getXsrfToken: () => {
        const parts = document.cookie.split(';');
        for (const part of parts) {
            const trimmed = part.trim();
            for (const name of ['__Host-XSRF-TOKEN', '__XSRF-TOKEN']) {
                if (trimmed.startsWith(name + '=')) {
                    return decodeURIComponent(trimmed.substring(name.length + 1));
                }
            }
        }
        return null;
    }
};

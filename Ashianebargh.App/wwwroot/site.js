// رفتارهای پوستهٔ عمومی سایت: دکمهٔ تم (تم سراسری theme.js) و تغییر زبان.
(function () {
    // زبان را مستقیم در کوکی می‌نویسیم و صفحه را کامل بارگذاری می‌کنیم؛
    // این‌گونه نیازی به ریدایرکت سرور و ناوبری پیشرفتهٔ Blazor نیست.
    document.addEventListener('click', function (event) {
        const langLink = event.target.closest('[data-lang-toggle]');
        if (!langLink) return;

        const lang = langLink.getAttribute('data-lang-toggle');
        if (lang !== 'fa' && lang !== 'en') return;

        event.preventDefault();
        event.stopPropagation();
        document.cookie = 'site-lang=' + lang + '; path=/; max-age=31536000; samesite=lax';
        window.location.reload();
    }, true);

    // همهٔ دکمه‌های تم (سایت و پنل) باید وضعیت فعلی را نشان دهند؛ ناوبری Blazor و
    // رندر دوباره، aria-pressed را به مقدار اولیه برمی‌گرداند.
    function syncThemeButtons() {
        const pressed = (document.documentElement.getAttribute('data-theme') || 'light') === 'dark' ? 'true' : 'false';
        document.querySelectorAll('[data-theme-toggle]').forEach((button) => button.setAttribute('aria-pressed', pressed));
    }

    document.addEventListener('click', function (event) {
        const button = event.target.closest('[data-theme-toggle]');
        if (!button) return;

        event.preventDefault();
        if (window.themeInterop) window.themeInterop.toggle();
        syncThemeButtons();
    });

    document.addEventListener('DOMContentLoaded', syncThemeButtons);

    // تم ممکن است بیرون از این فایل عوض شود (ناوبری Blazor، رندر مجدد پنل)؛
    // theme.js بعد از هر اعمال، رویداد می‌فرستد تا وضعیت دکمه‌ها همگام بماند.
    document.addEventListener('ashianebargh:theme', syncThemeButtons);

    window.siteInterop = {
        getLang: function () {
            const shell = document.querySelector('.site-shell');
            return shell ? (shell.getAttribute('data-lang') || 'fa') : 'fa';
        },
        // خواندن امن توکن Turnstile برای فرم تماس (مدار Blazor). اگر ویجت/اسکریپت
        // بار نشده باشد (توسعهٔ بدون کلید) رشتهٔ خالی برمی‌گردد تا اعتبارسنج غیرفعال بپذیرد.
        getCaptchaToken: function () {
            try {
                if (window.turnstile && typeof window.turnstile.getResponse === 'function') {
                    return window.turnstile.getResponse() || '';
                }
            } catch (e) { /* ویجت هنوز آماده نیست */ }
            return '';
        }
    };
})();

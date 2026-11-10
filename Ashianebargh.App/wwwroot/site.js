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

    document.addEventListener('click', function (event) {
        const button = event.target.closest('[data-theme-toggle]');
        if (!button) return;

        event.preventDefault();
        const theme = window.themeInterop ? window.themeInterop.toggle() : 'light';
        button.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
    });

    document.addEventListener('DOMContentLoaded', function () {
        const button = document.querySelector('[data-theme-toggle]');
        if (!button) return;
        const theme = document.documentElement.getAttribute('data-theme') || 'light';
        button.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
    });

    window.siteInterop = {
        getLang: function () {
            const shell = document.querySelector('.site-shell');
            return shell ? (shell.getAttribute('data-lang') || 'fa') : 'fa';
        }
    };
})();

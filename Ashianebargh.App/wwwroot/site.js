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

    // اسلایدر صفحهٔ اول: اسکریپتش فقط وقتی دانلود می‌شود که اسلایدر در صفحه باشد.
    // ناوبری افزانه اسکریپت‌های لایوت را دوباره اجرا نمی‌کند، پس هر enhancedload
    // نمونه‌های قبلی (تایمرها) را پاک و اسلایدر صفحهٔ جدید را راه‌اندازی می‌کند.
    let sliderScript = null;
    function ensureSliderScript() {
        if (!sliderScript) {
            sliderScript = new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = '/homeSlider.js';
                script.onload = resolve;
                script.onerror = () => reject(new Error('بارگذاری اسکریپت اسلایدر ناموفق بود'));
                document.head.appendChild(script);
            });
        }
        return sliderScript;
    }

    function refreshSlider() {
        if (window.ashHomeSlider) window.ashHomeSlider.destroy();
        if (!document.querySelector('[data-home-slider]')) return;
        ensureSliderScript()
            .then(() => window.ashHomeSlider.init())
            .catch(() => { /* بدون JS هم اسلاید اول به‌صورت SSR دیده می‌شود */ });
    }

    document.addEventListener('DOMContentLoaded', refreshSlider);

    // لایهٔ حرکتی: ظاهرشدن تدریجی بخش‌ها/کارت‌ها هنگام اسکرول + محو کوتاه ناوبری
    // افزانه. فقط opacity/translate؛ برای prefers-reduced-motion کاملاً خاموش
    // (CSS هم همان حالت را تحمیل می‌کند، پس تغییر زندهٔ تنظیم سیستم هم ایمن است).
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const REVEAL_SELECTOR = '.site-section, .site-cat, .site-product-card, .site-detail-layout, .content-prose';
    let revealObserver = null;

    function ensureRevealObserver() {
        if (revealObserver || !('IntersectionObserver' in window)) return revealObserver;
        revealObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-revealed');
                revealObserver.unobserve(entry.target);
            });
        }, { rootMargin: '0px 0px -40px 0px', threshold: 0.05 });
        return revealObserver;
    }

    function scanReveal() {
        if (reduceMotion.matches) return;
        const observer = ensureRevealObserver();
        if (!observer) return;
        document.querySelectorAll(REVEAL_SELECTOR).forEach(function (el) {
            if (el.classList.contains('site-reveal')) return;
            // بخشِ دربرگیرنده خودش reveal می‌شود؛ کارتِ داخلش دوباره پنهان نشود
            if (el.parentElement && el.parentElement.closest('.site-reveal')) return;
            el.classList.add('site-reveal');
            observer.observe(el);
        });
    }

    function navFadeStart() {
        if (reduceMotion.matches) return;
        document.documentElement.classList.add('site-nav-leaving');
    }

    function navFadeEnd() {
        document.documentElement.classList.remove('site-nav-leaving');
        scanReveal();
    }

    document.addEventListener('DOMContentLoaded', scanReveal);

    // enhancedload در blazor.web.js فقط به ثبت‌کنندهٔ داخلیِ Blazor می‌رسد، نه به‌صورت
    // رویداد DOM روی document؛ خودِ Blazor.addEventListener هم پس از start ساخته می‌شود
    // (site.js پیش از blazor.web.js بار می‌شود)، پس ثبت را تا آماده‌شدن آن به تعویق می‌اندازیم.
    (function registerEnhancedLoad() {
        if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', function () { refreshSlider(); navFadeEnd(); });
            // در این نسخه رویداد شروعِ ناوبری enhancednavigationstart است (enhancedloadstart وجود ندارد)
            window.Blazor.addEventListener('enhancednavigationstart', navFadeStart);
        }
        else
            setTimeout(registerEnhancedLoad, 100);
    })();

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

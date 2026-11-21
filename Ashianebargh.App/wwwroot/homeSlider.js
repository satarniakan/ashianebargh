// اسلایدر صفحهٔ اول: چرخش خودکار + ناوبری دستی؛ انیمیشن fade/slide/none از data-transition.
// بدون این اسکریپت هم اسلاید اول به‌صورت SSR نمایش داده می‌شود.
(function () {
    const instances = [];

    function create(root) {
        const track = root.querySelector('.site-slider-track');
        const slides = Array.from(root.querySelectorAll('.site-slide'));
        if (!track || slides.length === 0) return null;

        const transition = root.getAttribute('data-transition') || 'fade';
        const interval = Math.max(2000, parseInt(root.getAttribute('data-interval'), 10) || 5000);
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const dots = Array.from(root.querySelectorAll('[data-slider-dot]'));
        let index = 0;
        let timer = null;
        let paused = false;

        const isRtl = () =>
            (root.closest('[dir]') || document.documentElement).getAttribute('dir') === 'rtl';

        function apply() {
            if (transition === 'slide') {
                // در RTL ترک به راست می‌رود تا اسلاید بعدی (که سمت چپ چیده شده) دیده شود.
                const sign = isRtl() ? 1 : -1;
                track.style.transform = 'translateX(' + (sign * index * 100) + '%)';
            }
            slides.forEach((slide, i) => {
                slide.classList.toggle('is-active', i === index);
                slide.setAttribute('aria-hidden', i === index ? 'false' : 'true');
            });
            dots.forEach((dot, i) => {
                dot.classList.toggle('is-active', i === index);
                dot.setAttribute('aria-current', i === index ? 'true' : 'false');
            });
        }

        function stop() {
            if (timer) { clearInterval(timer); timer = null; }
        }

        function restart() {
            stop();
            if (slides.length < 2 || reduceMotion || paused || document.hidden) return;
            timer = setInterval(() => go(index + 1), interval);
        }

        function go(next) {
            index = (next + slides.length) % slides.length;
            apply();
            restart();
        }

        root.querySelector('[data-slider-prev]')?.addEventListener('click', () => go(index - 1));
        root.querySelector('[data-slider-next]')?.addEventListener('click', () => go(index + 1));
        dots.forEach((dot, i) => dot.addEventListener('click', () => go(i)));

        root.addEventListener('keydown', (event) => {
            const forward = isRtl() ? 'ArrowLeft' : 'ArrowRight';
            const backward = isRtl() ? 'ArrowRight' : 'ArrowLeft';
            if (event.key === forward) { event.preventDefault(); go(index + 1); }
            else if (event.key === backward) { event.preventDefault(); go(index - 1); }
        });

        // مکث هنگام اشاره/فوکوس تا کاربر فرصت خواندن متن اسلاید را داشته باشد.
        const pause = () => { paused = true; stop(); };
        const resume = (event) => {
            if (event && event.relatedTarget && root.contains(event.relatedTarget)) return;
            paused = false;
            restart();
        };
        root.addEventListener('mouseenter', pause);
        root.addEventListener('mouseleave', resume);
        root.addEventListener('focusin', pause);
        root.addEventListener('focusout', resume);

        const onVisibility = () => restart();
        document.addEventListener('visibilitychange', onVisibility);

        apply();
        restart();

        return {
            destroy() {
                stop();
                document.removeEventListener('visibilitychange', onVisibility);
            }
        };
    }

    window.ashHomeSlider = {
        init() {
            document.querySelectorAll('[data-home-slider]').forEach((root) => {
                if (root.dataset.sliderReady) return;
                const instance = create(root);
                if (instance) {
                    root.dataset.sliderReady = '1';
                    instances.push(instance);
                }
            });
        },
        destroy() {
            while (instances.length) instances.pop().destroy();
            document.querySelectorAll('[data-slider-ready]').forEach((root) => {
                delete root.dataset.sliderReady;
            });
        }
    };
})();
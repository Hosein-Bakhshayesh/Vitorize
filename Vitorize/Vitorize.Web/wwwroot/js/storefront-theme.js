// Vitorize storefront theme controller (light/dark, persisted in localStorage)
(function () {
    function hideInitialLoader() {
        var loader = document.getElementById('vz-initial-loader');
        if (!loader) return;
        loader.style.opacity = '0';
        setTimeout(function () {
            if (loader.parentNode) loader.parentNode.removeChild(loader);
        }, 250);
    }

    // Interactive public routes (cart, checkout and authentication) do not load
    // admin.js, so their SSR splash must be released by the shared bundle.
    window.addEventListener('load', function () { setTimeout(hideInitialLoader, 500); });
    document.addEventListener('click', hideInitialLoader, { once: true });

    document.addEventListener('click', function (event) {
        var link = event.target.closest && event.target.closest('.st-skip-link');
        if (!link) return;
        var target = document.getElementById('main-content');
        if (!target) return;
        event.preventDefault();
        target.focus({ preventScroll: true });
        target.scrollIntoView({ block: 'start' });
    });

    window.vzTheme = {
        get: function () {
            return document.documentElement.getAttribute('data-theme') || 'light';
        },
        set: function (t) {
            var theme = (t === 'dark') ? 'dark' : 'light';
            document.documentElement.setAttribute('data-theme', theme);
            try { localStorage.setItem('vitorize-theme', theme); } catch (e) { }
            return theme;
        },
        toggle: function () {
            return this.set(this.get() === 'dark' ? 'light' : 'dark');
        }
    };

    window.vzStorefront = {
        focusAndScroll: function (id) {
            try {
                var element = document.getElementById(id);
                if (!element) return false;
                element.scrollIntoView({ behavior: 'smooth', block: 'center' });
                if (typeof element.focus === 'function') element.focus({ preventScroll: true });
                return true;
            } catch (e) { return false; }
        }
    };

    // کمک‌کننده‌های ورود با کد یکبار‌مصرف (OTP): ارسال فرم نهایی برای ست‌کردن کوکی و فوکوس خودکار.
    window.vzOtp = {
        submitForm: function (id) {
            var f = document.getElementById(id);
            if (f) f.submit();
        },
        focus: function (id) {
            var el = document.getElementById(id);
            if (el) { try { el.focus(); el.select(); } catch (e) { } }
        }
    };
})();

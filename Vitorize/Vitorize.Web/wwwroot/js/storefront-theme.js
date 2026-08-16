// Vitorize storefront theme controller (light/dark, persisted in localStorage)
(function () {
    // The initial loading overlay is released by js/initial-loader.js (FIX-17), which is emitted
    // only on the routes that actually render it.

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

    window.vzStorefront = window.vzStorefront || {};
    window.vzStorefront.focusAndScroll = function (id) {
            try {
                var element = document.getElementById(id);
                if (!element) return false;
                element.scrollIntoView({ behavior: 'smooth', block: 'center' });
                if (typeof element.focus === 'function') element.focus({ preventScroll: true });
                return true;
            } catch (e) { return false; }
    };
    window.vzStorefront.focus = function (id) {
        try {
            var element = document.getElementById(id);
            if (!element || typeof element.focus !== 'function') return false;
            element.focus({ preventScroll: true });
            return true;
        } catch (e) { return false; }
    };
    window.vzStorefront.setFilterScrollLock = function (locked) {
        document.documentElement.classList.toggle('st-filter-scroll-locked', !!locked);
        document.body.classList.toggle('st-filter-scroll-locked', !!locked);
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

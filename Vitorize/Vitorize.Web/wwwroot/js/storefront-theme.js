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
            // Also stored as a cookie so the SERVER can render data-theme on <html>. localStorage is
            // invisible to the server, which is why the theme used to depend on a script running
            // before first paint on every single navigation.
            try {
                document.cookie = 'vitorize-theme=' + theme +
                    ';path=/;max-age=31536000;samesite=lax';
            } catch (e) { }
            return theme;
        },
        toggle: function () {
            return this.set(this.get() === 'dark' ? 'light' : 'dark');
        }
    };

    // Enhanced navigation swaps the document without a full load. The incoming <html> carries the
    // server-rendered theme, but a change made since that response was generated would be lost, so
    // the persisted choice is re-applied once the new page is in place.
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        try {
            window.Blazor.addEventListener('enhancedload', function () {
                if (typeof window.vzApplyTheme === 'function') window.vzApplyTheme();
            });
        } catch (e) { }
    }

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

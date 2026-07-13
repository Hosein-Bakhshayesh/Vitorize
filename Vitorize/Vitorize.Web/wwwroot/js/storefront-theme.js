// Vitorize storefront theme controller (light/dark, persisted in localStorage)
(function () {
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

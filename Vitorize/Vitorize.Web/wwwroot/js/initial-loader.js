// FIX-17 — release of the Vitorize initial loading overlay.
//
// This replaces the previous `window.load + setTimeout(500)` logic that lived, duplicated, in
// storefront-theme.js and admin.js. That approach was wrong twice over: `load` waits for every
// image and font (so the overlay outlived a usable app), and the extra 500ms was pure artificial
// delay. It is also the reason a click-anywhere escape hatch had to exist.
//
// The loader now disappears as soon as the app has actually rendered its shell, with no minimum
// duration, and a failsafe guarantees it can never trap the user if startup fails.
(function () {
    var loader = document.getElementById('vz-initial-loader');
    if (!loader) return;

    // The three layout roots in this app: StoreLayout and CustomerLayout render .st-shell,
    // AdminLayout renders .vz-shell, and BlankLayout (access-denied / admin login) renders
    // .vz-blank. Any of them means Blazor is interactive and there is something useful to see.
    var READY_SELECTOR = '.st-shell, .vz-shell, .vz-blank';
    var released = false;
    var observer = null;
    var failsafe = 0;

    function release() {
        if (released) return;
        released = true;
        if (observer) observer.disconnect();
        if (failsafe) clearTimeout(failsafe);
        window.removeEventListener('error', release);

        // Hidden and click-through immediately; the node is dropped after the fade so a
        // reduced-motion user (transition: none) is never left with a dead element on top.
        loader.classList.add('is-done');
        window.setTimeout(function () {
            if (loader.parentNode) loader.parentNode.removeChild(loader);
        }, 250);
    }

    function appReady() {
        return !!document.querySelector(READY_SELECTOR);
    }

    if (appReady()) {
        release();
        return;
    }

    observer = new MutationObserver(function () {
        if (appReady()) release();
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });

    // A startup error must surface the page, not a permanent overlay.
    window.addEventListener('error', release);

    // Last-resort failsafe. This is a ceiling, never a minimum: if the shell renders in 80ms the
    // loader is gone in 80ms. It exists only so a failed boot cannot leave a blocking overlay.
    failsafe = window.setTimeout(release, 10000);
})();

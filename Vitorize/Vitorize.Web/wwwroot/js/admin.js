// Vitorize Admin — minimal client helpers (no external dependencies)
(function () {
    function hideInitialLoader() {
        var loader = document.getElementById('vz-initial-loader');
        if (loader) {
            loader.style.opacity = '0';
            setTimeout(function () { if (loader && loader.parentNode) loader.parentNode.removeChild(loader); }, 250);
        }
    }

    // Remove the SSR splash once Blazor has started rendering.
    window.addEventListener('load', function () { setTimeout(hideInitialLoader, 500); });
    document.addEventListener('click', hideInitialLoader, { once: true });

    // Blazor default error UI handlers
    document.addEventListener('click', function (e) {
        if (e.target && e.target.classList && e.target.classList.contains('dismiss')) {
            var ui = document.getElementById('blazor-error-ui');
            if (ui) ui.style.display = 'none';
        }
    });

    window.vzAdmin = {
        // Registers an outside-click handler that invokes a .NET method to close a popup.
        registerOutside: function (element, dotNetRef, methodName) {
            if (!element) return;
            const handler = function (ev) {
                if (!element.contains(ev.target)) {
                    dotNetRef.invokeMethodAsync(methodName);
                }
            };
            setTimeout(function () { document.addEventListener('mousedown', handler); }, 0);
            element._vzOutside = handler;
        },
        unregisterOutside: function (element) {
            if (element && element._vzOutside) {
                document.removeEventListener('mousedown', element._vzOutside);
                element._vzOutside = null;
            }
        },
        focus: function (element) { if (element) try { element.focus(); } catch (e) { } }
    };
})();

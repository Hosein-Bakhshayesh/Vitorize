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
        focus: function (element) { if (element) try { element.focus(); } catch (e) { } },
        focusById: function (id) { var el = document.getElementById(id); if (el) try { el.focus(); if (el.select) el.select(); } catch (e) { } },
        // Client-side file download (used for CSV export — no backend endpoint required).
        downloadText: function (fileName, text, mime) {
            try {
                var blob = new Blob(["﻿" + text], { type: (mime || 'text/csv') + ';charset=utf-8;' });
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = fileName;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
            } catch (e) { }
        },
        // Copy text to clipboard, with a legacy fallback for non-secure contexts.
        copy: async function (text) {
            try {
                if (navigator.clipboard && window.isSecureContext) {
                    await navigator.clipboard.writeText(text);
                    return true;
                }
            } catch (e) { /* fall through */ }
            try {
                var ta = document.createElement('textarea');
                ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0';
                document.body.appendChild(ta); ta.focus(); ta.select();
                var ok = document.execCommand('copy');
                document.body.removeChild(ta);
                return ok;
            } catch (e) { return false; }
        },
        // Global keyboard shortcuts. Forwards an allow-list of keys to .NET, ignoring
        // keystrokes typed inside form fields (except Escape).
        registerShortcuts: function (dotNetRef, methodName) {
            this.unregisterShortcuts();
            var allowed = ['/', 'r', 'n', '?', 'Escape'];
            var handler = function (e) {
                if (e.ctrlKey || e.metaKey || e.altKey) return;
                if (allowed.indexOf(e.key) === -1) return;
                var t = e.target;
                var tag = (t && t.tagName || '').toLowerCase();
                var typing = tag === 'input' || tag === 'textarea' || tag === 'select' || (t && t.isContentEditable);
                if (typing && e.key !== 'Escape') return;
                if (e.key === '/' || e.key === '?') e.preventDefault();
                dotNetRef.invokeMethodAsync(methodName, e.key);
            };
            window._vzKeys = handler;
            document.addEventListener('keydown', handler);
        },
        unregisterShortcuts: function () {
            if (window._vzKeys) {
                document.removeEventListener('keydown', window._vzKeys);
                window._vzKeys = null;
            }
        }
    };
})();

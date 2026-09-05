// Mobile browsers commonly suspend an open WebSocket while their app is backgrounded. Blazor's
// stock handler stops after a small number of retries and leaves an unusable page behind. This
// handler retries promptly after the browser becomes visible again, retains the normal circuit
// when it is still available, and reloads only after a genuinely unrecoverable connection loss.
(function () {
    var state = { reconnecting: false, attempts: 0, maxAttempts: 60, retryTimer: null };

    function statusElement() {
        var element = document.getElementById("vz-reconnect-status");
        if (element) return element;

        element = document.createElement("div");
        element.id = "vz-reconnect-status";
        element.setAttribute("role", "status");
        element.setAttribute("aria-live", "polite");
        element.textContent = "در حال برقراری دوباره ارتباط…";
        element.style.cssText = [
            "position:fixed", "inset-block-start:12px", "inset-inline:12px", "z-index:10000",
            "display:none", "margin-inline:auto", "width:fit-content", "max-width:calc(100vw - 24px)",
            "padding:9px 14px", "border-radius:12px", "background:rgba(15,23,42,.88)",
            "color:#fff", "font:600 13px/1.5 sans-serif", "box-shadow:0 8px 24px rgba(15,23,42,.24)"
        ].join(";");
        document.body.appendChild(element);
        return element;
    }

    function showStatus(visible) {
        statusElement().style.display = visible ? "block" : "none";
    }

    function clearRetryTimer() {
        if (state.retryTimer !== null) {
            window.clearTimeout(state.retryTimer);
            state.retryTimer = null;
        }
    }

    function finish() {
        clearRetryTimer();
        state.reconnecting = false;
        state.attempts = 0;
        showStatus(false);
    }

    function scheduleRetry(delay) {
        clearRetryTimer();
        state.retryTimer = window.setTimeout(attemptReconnect, delay);
    }

    async function attemptReconnect() {
        state.retryTimer = null;
        if (!state.reconnecting) return;

        // Do not burn retries while Android/iOS deliberately pauses the page. Visibility and
        // online events below immediately resume the attempt as soon as a return is possible.
        if (document.hidden || navigator.onLine === false) return;

        if (state.attempts >= state.maxAttempts) {
            window.location.reload();
            return;
        }

        state.attempts++;
        try {
            if (await Blazor.reconnect()) {
                finish();
                return;
            }
        } catch (_) {
            // A temporary network or proxy failure is retried below.
        }

        scheduleRetry(2000);
    }

    function retryWhenPossible() {
        if (!state.reconnecting || document.hidden || navigator.onLine === false) return;
        clearRetryTimer();
        attemptReconnect();
    }

    window.vzBlazorReconnectHandler = {
        onConnectionDown: function (options) {
            if (state.reconnecting) return;
            state.reconnecting = true;
            state.attempts = 0;
            state.maxAttempts = Number.isFinite(options && options.maxRetries)
                ? options.maxRetries
                : 60;
            showStatus(true);
            retryWhenPossible();
        },
        onConnectionUp: finish
    };

    window.addEventListener("online", retryWhenPossible);
    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) retryWhenPossible();
    });
})();

// Mobile browsers and VPN clients can replace an open WebSocket without sending the browser's
// `online` event. Blazor's stock overlay is both intrusive and can leave the page stuck after
// that handoff. Keep retrying silently for the whole retained-circuit window, and reload only
// when the server has definitively rejected the old circuit.
(function () {
    var RetryDelayMs = 2000;
    var OfflineRetryDelayMs = 3000;
    var DelayedNoticeMs = 8000;
    var RetainedCircuitAttempts = 900;
    var state = { reconnecting: false, attempts: 0, maxAttempts: RetainedCircuitAttempts, retryTimer: null, noticeTimer: null };

    function statusElement() {
        var element = document.getElementById("vz-reconnect-status");
        if (element) return element;

        element = document.createElement("div");
        element.id = "vz-reconnect-status";
        element.className = "vz-reconnect-status";
        element.setAttribute("role", "status");
        element.setAttribute("aria-live", "polite");
        element.textContent = "ارتباط موقتاً قطع شده؛ بازیابی خودکار ادامه دارد.";
        element.hidden = true;
        document.body.appendChild(element);
        return element;
    }

    function showStatus(visible) {
        statusElement().hidden = !visible;
    }

    function clearRetryTimer() {
        if (state.retryTimer !== null) {
            window.clearTimeout(state.retryTimer);
            state.retryTimer = null;
        }
    }

    function clearNoticeTimer() {
        if (state.noticeTimer !== null) {
            window.clearTimeout(state.noticeTimer);
            state.noticeTimer = null;
        }
    }

    function finish() {
        clearRetryTimer();
        clearNoticeTimer();
        state.reconnecting = false;
        state.attempts = 0;
        showStatus(false);
    }

    function scheduleRetry(delay) {
        clearRetryTimer();
        state.retryTimer = window.setTimeout(attemptReconnect, delay);
    }

    function scheduleDelayedNotice() {
        clearNoticeTimer();
        state.noticeTimer = window.setTimeout(function () {
            if (state.reconnecting) showStatus(true);
        }, DelayedNoticeMs);
    }

    async function attemptReconnect() {
        state.retryTimer = null;
        if (!state.reconnecting) return;

        // A backgrounded tab cannot reliably reconnect. The visibility handler below tries at
        // once on return. Crucially, an offline report still schedules another attempt: VPN
        // switches often never fire an online event even after the route is usable again.
        if (document.hidden) return;
        if (navigator.onLine === false) {
            scheduleRetry(OfflineRetryDelayMs);
            return;
        }

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
            // `false` means the server no longer has this circuit; retries cannot restore its
            // state, so recover a usable page without making the customer press refresh.
            window.location.reload();
            return;
        } catch (_) {
            // A temporary network or proxy failure is retried below.
        }

        scheduleRetry(RetryDelayMs);
    }

    function retryWhenPossible() {
        if (!state.reconnecting || document.hidden) return;
        if (navigator.onLine === false) {
            scheduleRetry(OfflineRetryDelayMs);
            return;
        }
        clearRetryTimer();
        attemptReconnect();
    }

    window.vzBlazorReconnectHandler = {
        onConnectionDown: function (options) {
            if (state.reconnecting) return;
            state.reconnecting = true;
            state.attempts = 0;
            state.maxAttempts = Math.max(RetainedCircuitAttempts, Number.isFinite(options && options.maxRetries)
                ? options.maxRetries
                : RetainedCircuitAttempts);
            showStatus(false);
            scheduleDelayedNotice();
            retryWhenPossible();
        },
        onConnectionUp: finish
    };

    // The stock template only wires this dismiss affordance on some layouts. Keep it usable on
    // storefront, customer, and admin routes alike; a recoverable error should not pin a banner
    // to the screen until the next navigation.
    document.addEventListener("click", function (event) {
        var dismiss = event.target && event.target.closest && event.target.closest("#blazor-error-ui .dismiss");
        if (!dismiss) return;
        event.preventDefault();
        var errorUi = document.getElementById("blazor-error-ui");
        if (errorUi) errorUi.style.display = "none";
    });

    window.addEventListener("online", retryWhenPossible);
    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) retryWhenPossible();
    });
})();

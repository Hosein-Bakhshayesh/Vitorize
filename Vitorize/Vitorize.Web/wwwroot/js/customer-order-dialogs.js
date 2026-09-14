// Only order-page dialogs: leave shared/admin modal behavior untouched.
(() => {
    let active = null, trigger = null, inertNodes = [], overflow = '';
    const focusable = () => [...active.querySelectorAll('button:not(:disabled), a[href], input:not(:disabled), textarea:not(:disabled), select:not(:disabled), [tabindex="0"]')].filter(el => el.getClientRects().length);
    function sync() {
        const next = document.querySelector('.customer-orders-page .vz-dialog');
        if (next === active) return;
        if (active) {
            for (const el of inertNodes) el.inert = false;
            inertNodes = [];
            document.body.style.overflow = overflow;
            if (trigger?.isConnected) trigger.focus({ preventScroll: true });
        }
        active = next;
        if (!active) return;
        trigger = document.activeElement;
        overflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        for (let branch = active.closest('.vz-overlay'); branch && branch !== document.body; branch = branch.parentElement) {
            for (const sibling of branch.parentElement.children) {
                if (sibling !== branch && !sibling.inert && !sibling.matches('script, style, link')) {
                    sibling.inert = true; inertNodes.push(sibling);
                }
            }
        }
        (focusable()[0] ?? active).focus({ preventScroll: true });
    }
    document.addEventListener('keydown', event => {
        if (!active) return;
        const items = focusable(), first = items[0], last = items.at(-1);
        if (event.key === 'Escape') {
            event.preventDefault(); event.stopImmediatePropagation();
            // Confirmation's first footer button is the safe cancel action.
            const cancel = active.querySelector('.vz-dialog__foot button');
            if (cancel && !cancel.disabled) cancel.click();
        } else if (event.key === 'Tab') {
            if (!first) { event.preventDefault(); active.focus(); }
            else if (!active.contains(document.activeElement) || document.activeElement === active) { event.preventDefault(); (event.shiftKey ? last : first).focus(); }
            else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
        }
    }, true);
    new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
    sync();
})();

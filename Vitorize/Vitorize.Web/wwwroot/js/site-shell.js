// Listeners live on this header instance, never on the admin or homepage shell.
export function install(header) {
    header.dataset.shellReady = "true";
    header.addEventListener("keydown", event => {
        if (event.key !== "Tab") return;
        const panel = header.querySelector(".sf-search-panel, .st-mobile-nav__sheet, .st-catmenu, .st-dropdown");
        if (!panel) return;
        const items = [...panel.querySelectorAll("a[href], button:not([disabled]), input:not([disabled])")]
            .filter(el => el.getClientRects().length && getComputedStyle(el).visibility !== "hidden");
        const first = items[0], last = items.at(-1);
        if (!first) return;
        if (!panel.contains(document.activeElement)) {
            event.preventDefault(); (event.shiftKey ? last : first).focus();
        } else if (event.shiftKey && document.activeElement === first) {
            event.preventDefault(); last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault(); first.focus();
        }
    });
}
export function focus(header, selector) {
    header.querySelector(selector)?.focus({ preventScroll: true });
}

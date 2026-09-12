// The handoff shows a review rail with both neighbouring cards peeking in.
// Scroll real content to that initial position instead of permanently clipping a card.
export function positionReviews(element) {
    if (!element?.isConnected) return;
    element.scrollLeft = window.matchMedia("(max-width: 767px)").matches
        ? -168 : -148 * Math.min(1, window.innerWidth / 1728);
}

export function installMenuKeyboardNavigation(header) {
    if (header.dataset.keyboardReady) return;
    header.dataset.keyboardReady = "true";
    header.addEventListener("keydown", event => {
        if (event.key !== "Tab" || !header.classList.contains("is-focused")) return;
        const items = [...header.querySelectorAll("a[href], button:not([disabled])")]
            .filter(element => element.getClientRects().length && getComputedStyle(element).visibility !== "hidden");
        const first = items[0], last = items.at(-1);
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault(); last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault(); first?.focus();
        }
    });
}

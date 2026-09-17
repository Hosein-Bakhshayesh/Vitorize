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

// Rotates the homepage promo deck in the browser. Doing this on the server would push a re-render
// down every open circuit every few seconds for a change nobody needs to round-trip.
export function startSlideshow(root, intervalMs) {
    if (!root?.isConnected || root.dataset.slideshowReady) return;
    root.dataset.slideshowReady = "true";

    const slides = [...root.querySelectorAll(".hp-promo__card")];
    const dots = [...root.querySelectorAll(".hp-dots button")];
    if (slides.length < 2) return;

    // Someone who asked for less motion still gets the dots; they just do not fire on their own.
    const calm = window.matchMedia("(prefers-reduced-motion: reduce)");
    let current = 0;
    let timer = null;
    let paused = false;

    const show = index => {
        current = (index + slides.length) % slides.length;
        slides.forEach((slide, i) => {
            const on = i === current;
            slide.classList.toggle("is-active", on);
            slide.setAttribute("aria-hidden", on ? "false" : "true");
        });
        dots.forEach((dot, i) => {
            const on = i === current;
            dot.classList.toggle("is-active", on);
            dot.setAttribute("aria-selected", on ? "true" : "false");
        });
    };

    const stop = () => { clearInterval(timer); timer = null; };
    // Arming has to respect the paused state: a dot clicked while the pointer rests on the deck
    // must not start the clock again under the reader's hands.
    const arm = () => {
        stop();
        if (!paused && !calm.matches) timer = setInterval(() => show(current + 1), intervalMs);
    };
    const pause = () => { paused = true; stop(); };
    const resume = () => { paused = false; arm(); };

    dots.forEach((dot, i) => dot.addEventListener("click", () => { show(i); arm(); }));

    // Hold while a visitor is reading or tabbing through, and while the tab is in the background.
    root.addEventListener("pointerenter", pause);
    root.addEventListener("pointerleave", resume);
    root.addEventListener("focusin", pause);
    root.addEventListener("focusout", resume);
    document.addEventListener("visibilitychange", () => document.hidden ? pause() : resume());
    calm.addEventListener("change", arm);

    show(0);
    arm();
}
// The handoff shows a review rail with both neighbouring cards peeking in.
// Scroll real content to that initial position instead of permanently clipping a card.
export function positionReviews(element) {
    if (!element?.isConnected) return;
    element.scrollLeft = window.matchMedia("(max-width: 767px)").matches
        ? -168 : -148 * Math.min(1, window.innerWidth / 1728);
}

// The brand rail is a measured, duplicated cycle. A cycle is expanded to cover two viewports
// before it moves, so a small configured brand list never leaves an empty tail at the hand-off.
const brandRailStops = new WeakMap();

export function startBrandRail(track) {
    if (!track?.isConnected || brandRailStops.has(track)) return;

    const groups = [...track.querySelectorAll(".hp-brands__group")];
    const cycle = groups[0];
    const viewport = track.parentElement;
    if (!cycle || groups.length < 2 || !viewport) return;

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let frame = 0;
    let previous = 0;
    let offset = 0;
    let cycleWidth = 0;
    let running = false;
    let hovered = false;
    let focused = false;

    const makeDuplicateInert = element => {
        element.setAttribute("aria-hidden", "true");
        element.querySelectorAll("a").forEach(link => {
            link.classList.add("hp-brand-clone");
            link.setAttribute("tabindex", "-1");
            link.removeAttribute("aria-label");
            link.querySelectorAll("img").forEach(image => image.alt = "");
        });
        return element;
    };
    const ensureCoverage = () => {
        const viewportWidth = viewport.getBoundingClientRect().width;
        if (!viewportWidth) return;
        const seeds = groups.map(group => group.querySelector(".hp-brands__sequence"));
        const sequenceWidth = seeds[0]?.getBoundingClientRect().width ?? 0;
        if (!sequenceWidth) return;
        const missing = Math.max(0, Math.ceil((viewportWidth * 2 - cycle.getBoundingClientRect().width) / sequenceWidth));
        for (let i = 0; i < missing; i++) {
            groups.forEach((group, index) => group.append(makeDuplicateInert(seeds[index].cloneNode(true))));
        }
    };
    // Preserve subpixel precision: rounded scrollWidth accumulates a visible seam jump.
    const measure = () => { cycleWidth = cycle.getBoundingClientRect().width; };
    const paint = () => { track.style.transform = offset ? `translate3d(${-offset}px, 0, 0)` : "translate3d(0, 0, 0)"; };
    const tick = now => {
        if (!running) return;
        if (!previous) previous = now;
        const elapsed = Math.min(100, now - previous);
        previous = now;
        if (cycleWidth > 0) {
            // 54px/s stays readable but gives the rail an unmistakably continuous movement.
            offset = (offset + elapsed * 0.054) % cycleWidth;
            paint();
        }
        frame = requestAnimationFrame(tick);
    };
    const start = () => {
        if (running || hovered || focused || reduceMotion.matches || document.hidden) return;
        ensureCoverage();
        measure();
        if (!cycleWidth) return;
        running = true;
        previous = 0;
        frame = requestAnimationFrame(tick);
    };
    const stop = () => {
        running = false;
        if (frame) cancelAnimationFrame(frame);
        frame = 0;
    };
    const onVisibility = () => document.hidden ? stop() : start();
    const pause = () => { hovered = true; stop(); };
    const resume = () => { hovered = false; start(); };
    const onFocusIn = () => { focused = true; stop(); };
    const onFocusOut = event => {
        focused = viewport.contains(event.relatedTarget);
        if (!focused) start();
    };
    const onMotionChange = () => {
        stop();
        if (reduceMotion.matches) { offset = 0; paint(); }
        else start();
    };
    const observer = new ResizeObserver(() => {
        const priorWidth = cycleWidth;
        ensureCoverage();
        measure();
        if (cycleWidth && priorWidth) offset = (offset / priorWidth * cycleWidth) % cycleWidth;
        paint();
        start();
    });

    observer.observe(cycle);
    observer.observe(viewport);
    document.addEventListener("visibilitychange", onVisibility);
    reduceMotion.addEventListener("change", onMotionChange);
    // Listen on the stationary viewport, not the translating track.
    viewport.addEventListener("pointerenter", pause);
    viewport.addEventListener("pointerleave", resume);
    viewport.addEventListener("focusin", onFocusIn);
    viewport.addEventListener("focusout", onFocusOut);
    brandRailStops.set(track, () => {
        stop();
        observer.disconnect();
        document.removeEventListener("visibilitychange", onVisibility);
        reduceMotion.removeEventListener("change", onMotionChange);
        viewport.removeEventListener("pointerenter", pause);
        viewport.removeEventListener("pointerleave", resume);
        viewport.removeEventListener("focusin", onFocusIn);
        viewport.removeEventListener("focusout", onFocusOut);
        track.style.transform = "";
        brandRailStops.delete(track);
    });
    ensureCoverage();
    measure();
    paint();
    start();
}

export function stopBrandRail(track) {
    brandRailStops.get(track)?.();
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

// Scoped to the catalog filter dialog. Never changes header or other dialogs.
let panel;
let inertElements = [];
function onKeyDown(event) {
    if (event.key !== 'Tab' || !panel) return;
    const elements = [...panel.querySelectorAll('button, input, select, a[href], [tabindex="0"]')]
        .filter(el => !el.disabled && el.getClientRects().length);
    const first = elements[0], last = elements.at(-1);
    if (!first) { event.preventDefault(); panel.focus(); return; }
    if (event.shiftKey && (document.activeElement === first || document.activeElement === panel)) {
        event.preventDefault(); last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault(); first.focus();
    }
}
export function release() {
    panel?.removeEventListener('keydown', onKeyDown);
    for (const el of inertElements) el.inert = false;
    inertElements = [];
    panel = null;
}
export function trap() {
    release();
    panel = document.querySelector('.catalog-page .st-filter-sheet__panel');
    if (!panel) return;
    panel.addEventListener('keydown', onKeyDown);
    // Make sibling branches inaccessible while leaving the dialog's ancestors active.
    for (let branch = panel; branch.parentElement && branch !== document.body; branch = branch.parentElement) {
        for (const sibling of branch.parentElement.children) {
            if (sibling !== branch && !sibling.inert && !sibling.classList.contains('st-filter-sheet__backdrop')) {
                sibling.inert = true;
                inertElements.push(sibling);
            }
        }
    }
}

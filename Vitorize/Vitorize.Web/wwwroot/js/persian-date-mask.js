// Progressive yyyy/MM/dd mask for the shared Persian date input.
//
// The separators are inserted for the customer as they type: four digits of year, then a slash, two of
// month, then a slash, two of day. Nobody should have to reach for "/" on a numeric keypad.
//
// This runs in the browser rather than on the server on purpose. A Blazor Server round trip per
// keystroke would rewrite the input's value after the caret had already moved, which is exactly how
// masked inputs end up jumping the cursor mid-word. Formatting locally keeps typing predictable; the
// value still reaches the component through the normal input/change events, and the component parses
// and validates it server-side, so the mask is a convenience and never the authority.
window.vzPersianDateMask = window.vzPersianDateMask || (function () {
    const PERSIAN_ZERO = 0x06F0;   // ۰
    const ARABIC_ZERO = 0x0660;    // ٠

    // Persian and Arabic-Indic digits are accepted as readily as ASCII, then normalised.
    function toAsciiDigits(text) {
        let out = "";
        for (const ch of text) {
            const code = ch.codePointAt(0);
            if (code >= PERSIAN_ZERO && code <= PERSIAN_ZERO + 9) out += String(code - PERSIAN_ZERO);
            else if (code >= ARABIC_ZERO && code <= ARABIC_ZERO + 9) out += String(code - ARABIC_ZERO);
            else if (ch >= "0" && ch <= "9") out += ch;
        }
        return out;
    }

    /** yyyy, yyyy/MM, yyyy/MM/dd - built from at most eight digits. */
    function format(digits) {
        const d = digits.slice(0, 8);
        if (d.length <= 4) return d;
        if (d.length <= 6) return d.slice(0, 4) + "/" + d.slice(4);
        return d.slice(0, 4) + "/" + d.slice(4, 6) + "/" + d.slice(6);
    }

    /** How many digits precede this caret position in the formatted text. */
    function digitsBefore(text, caret) {
        return toAsciiDigits(text.slice(0, caret)).length;
    }

    /** Caret position that sits just after the given number of digits. */
    function caretAfterDigits(text, count) {
        if (count <= 0) return 0;
        let seen = 0;
        for (let i = 0; i < text.length; i++) {
            if (/[0-9]/.test(text[i])) {
                seen++;
                if (seen === count) return i + 1;
            }
        }
        return text.length;
    }

    function apply(input, digits, caretDigits) {
        const formatted = format(digits);
        if (input.value !== formatted) {
            input.value = formatted;
            // Tell Blazor the value changed; @oninput/@onchange both bind through these.
            //
            // Blazor also receives the browser's own input event, so a keystroke costs two round trips
            // on a Server circuit. An attempt to suppress the original with a capture-phase
            // stopPropagation did remove the duplicate - and stopped the value reaching the component
            // at all, so a typed birth date saved as empty. The duplicate is a cost; losing the value
            // is a defect, so the duplicate stays.
            input.dispatchEvent(new Event("input", { bubbles: true }));
        }
        const caret = caretAfterDigits(formatted, caretDigits);
        try { input.setSelectionRange(caret, caret); } catch { /* not a text input */ }
    }

    function onBeforeInput(event) {
        const input = event.target;
        // Only the cases the browser would get wrong are intercepted; everything else falls through
        // to the normal input event below.
        if (event.inputType !== "deleteContentBackward") return;
        const start = input.selectionStart ?? 0;
        const end = input.selectionEnd ?? 0;
        if (start !== end || start === 0) return;

        // Backspace immediately after a separator should remove the digit before it, not the slash -
        // otherwise the slash is deleted and instantly re-inserted, and nothing appears to happen.
        if (input.value[start - 1] !== "/") return;
        event.preventDefault();
        const digits = toAsciiDigits(input.value);
        const keep = Math.max(0, digitsBefore(input.value, start) - 1);
        apply(input, digits.slice(0, keep) + digits.slice(digitsBefore(input.value, start)), keep);
    }

    function onInput(event) {
        const input = event.target;
        if (input.dataset.vzMasking === "1") return;   // our own dispatched event
        input.dataset.vzMasking = "1";
        try {
            const caret = input.selectionStart ?? input.value.length;
            const caretDigits = digitsBefore(input.value, caret);
            apply(input, toAsciiDigits(input.value), caretDigits);
        } finally {
            delete input.dataset.vzMasking;
        }
    }

    return {
        attach: function (input) {
            if (!input || input.dataset.vzDateMask === "1") return;
            input.dataset.vzDateMask = "1";
            input.setAttribute("dir", "ltr");
            input.setAttribute("maxlength", "10");
            input.addEventListener("beforeinput", onBeforeInput);
            input.addEventListener("input", onInput);
            // A pasted value is normalised the same way, with or without separators.
            input.addEventListener("paste", function () { window.setTimeout(() => onInput({ target: input }), 0); });
        }
    };
})();

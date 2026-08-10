// Localized, accessible validation for storefront forms that submit by normal HTML POST.
// Server validation remains authoritative after client-side validity is established.
(() => {
    const invalidClass = 'is-invalid';
    const messageFor = (field) => {
        if (field.validity.valueMissing) return 'وارد کردن این فیلد الزامی است.';
        if (field.type === 'email' && field.validity.typeMismatch) return 'ایمیل واردشده معتبر نیست.';
        return 'مقدار واردشده معتبر نیست.';
    };
    const errorId = (field) => `${field.id || field.name}-error`;
    const fieldGroup = (field) => field.closest('.st-field') || field.parentElement;
    const clear = (field) => {
        const id = errorId(field);
        field.classList.remove(invalidClass);
        field.setAttribute('aria-invalid', 'false');
        field.removeAttribute('aria-describedby');
        fieldGroup(field)?.querySelector(`#${CSS.escape(id)}`)?.remove();
    };
    const show = (field) => {
        const id = errorId(field);
        const group = fieldGroup(field);
        field.classList.add(invalidClass);
        field.setAttribute('aria-invalid', 'true');
        field.setAttribute('aria-describedby', id);
        let error = group?.querySelector(`#${CSS.escape(id)}`);
        if (!error && group) {
            error = document.createElement('span');
            error.id = id;
            error.className = 'st-field__error';
            error.setAttribute('role', 'alert');
            group.append(error);
        }
        if (error) error.textContent = messageFor(field);
    };
    const validate = (form) => {
        const fields = [...form.querySelectorAll('input:not([type="hidden"]), select, textarea')]
            .filter((field) => !field.disabled && (field.required || (field.type === 'email' && field.value)));
        let firstInvalid = null;
        for (const field of fields) {
            if (field.checkValidity()) clear(field);
            else { show(field); firstInvalid ??= field; }
        }
        if (!firstInvalid) return true;
        firstInvalid.focus({ preventScroll: true });
        firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
        return false;
    };
    document.addEventListener('input', (event) => {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement)) return;
        if (field.closest('form[data-vz-validate]') && field.getAttribute('aria-invalid') === 'true' && field.checkValidity()) clear(field);
    });
    document.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.matches('form[data-vz-validate]')) return;
        if (!validate(form)) event.preventDefault();
    }, true);
})();

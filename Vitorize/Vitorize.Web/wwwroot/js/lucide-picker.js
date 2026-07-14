(function () {
    "use strict";

    const prefix = "vitorize-lucide:";
    const triggers = new WeakMap();

    function storageKey(scope) {
        return prefix + String(scope || "admin").replace(/[^a-zA-Z0-9_.:@-]/g, "_");
    }

    function load(scope) {
        try {
            const value = JSON.parse(localStorage.getItem(storageKey(scope)) || "{}");
            return {
                favorites: Array.isArray(value.favorites) ? value.favorites.slice(0, 100) : [],
                recent: Array.isArray(value.recent) ? value.recent.slice(0, 24) : []
            };
        } catch (_) {
            return { favorites: [], recent: [] };
        }
    }

    function save(scope, value) {
        try { localStorage.setItem(storageKey(scope), JSON.stringify(value)); } catch (_) { }
    }

    function installGridKeys(dialog) {
        if (dialog.dataset.gridKeys === "true") return;
        dialog.dataset.gridKeys = "true";
        dialog.addEventListener("keydown", function (event) {
            if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Enter", " "].includes(event.key)) return;
            const active = document.activeElement;
            if (!(active instanceof HTMLElement) || !active.classList.contains("vz-icon-picker__cell-main")) return;

            if (event.key === "Enter" || event.key === " ") return;
            const buttons = Array.from(dialog.querySelectorAll(".vz-icon-picker__cell-main:not([disabled])"));
            const index = buttons.indexOf(active);
            if (index < 0) return;
            const grid = active.closest(".vz-icon-picker__grid");
            const width = active.getBoundingClientRect().width || 88;
            const columns = Math.max(1, Math.floor((grid?.clientWidth || width) / width));
            const rtl = getComputedStyle(grid || dialog).direction === "rtl";
            let next = index;
            if (event.key === "ArrowLeft") next += rtl ? 1 : -1;
            if (event.key === "ArrowRight") next += rtl ? -1 : 1;
            if (event.key === "ArrowUp") next -= columns;
            if (event.key === "ArrowDown") next += columns;
            next = Math.max(0, Math.min(buttons.length - 1, next));
            if (next !== index) {
                event.preventDefault();
                buttons[next].focus();
            }
        });
        dialog.addEventListener("click", function (event) {
            if (event.target !== dialog) return;
            const rect = dialog.getBoundingClientRect();
            const inside = event.clientX >= rect.left && event.clientX <= rect.right &&
                event.clientY >= rect.top && event.clientY <= rect.bottom;
            if (!inside) dialog.dispatchEvent(new Event("cancel", { cancelable: true }));
        });
    }

    window.vitorizeLucide = {
        showDialog: function (dialog, trigger) {
            if (!dialog) return;
            triggers.set(dialog, trigger || document.activeElement);
            installGridKeys(dialog);
            if (!dialog.open) dialog.showModal();
            requestAnimationFrame(() => dialog.querySelector("input[type=search]")?.focus());
        },
        closeDialog: function (dialog) {
            if (!dialog) return;
            if (dialog.open) dialog.close();
            const trigger = triggers.get(dialog);
            if (trigger && typeof trigger.focus === "function") requestAnimationFrame(() => trigger.focus());
        },
        loadPreferences: load,
        setFavorite: function (scope, key, selected) {
            const value = load(scope);
            value.favorites = value.favorites.filter(x => x !== key);
            if (selected) value.favorites.unshift(key);
            value.favorites = value.favorites.slice(0, 100);
            save(scope, value);
            return value;
        },
        recordRecent: function (scope, key) {
            const value = load(scope);
            value.recent = [key, ...value.recent.filter(x => x !== key)].slice(0, 24);
            save(scope, value);
            return value;
        },
        copy: async function (value) {
            if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(value || "");
        }
    };
})();

// Vitorize Admin — minimal client helpers (no external dependencies)
(function () {
    // The initial loading overlay is released by js/initial-loader.js (FIX-17).

    // Blazor default error UI handlers
    document.addEventListener('click', function (e) {
        if (e.target && e.target.classList && e.target.classList.contains('dismiss')) {
            var ui = document.getElementById('blazor-error-ui');
            if (ui) ui.style.display = 'none';
        }
    });

    window.vzAdmin = {
        // Registers an outside-click handler that invokes a .NET method to close a popup.
        registerOutside: function (element, dotNetRef, methodName) {
            if (!element) return;
            const handler = function (ev) {
                if (!element.contains(ev.target)) {
                    dotNetRef.invokeMethodAsync(methodName);
                }
            };
            setTimeout(function () { document.addEventListener('mousedown', handler); }, 0);
            element._vzOutside = handler;
        },
        unregisterOutside: function (element) {
            if (element && element._vzOutside) {
                document.removeEventListener('mousedown', element._vzOutside);
                element._vzOutside = null;
            }
        },
        focus: function (element) { if (element) try { element.focus(); } catch (e) { } },
        focusById: function (id) { var el = document.getElementById(id); if (el) try { el.focus(); if (el.select) el.select(); } catch (e) { } },
        toggleContextMenu: function (trigger, menu, dotNetRef) {
            if (!trigger || !menu) return false;
            if (menu.matches(':popover-open') || menu.dataset.vzOpen === 'true') {
                this.closeContextMenu(menu, trigger);
                return false;
            }

            var rect = trigger.getBoundingClientRect();
            menu.style.position = 'fixed';
            menu.style.margin = '0';
            menu.style.visibility = 'hidden';
            menu.style.display = 'block';
            menu.dataset.vzOpen = 'true';
            menu._vzTrigger = trigger;
            menu._vzDotNet = dotNetRef;
            try { if (menu.showPopover) menu.showPopover(); } catch (e) { }

            var width = Math.max(menu.offsetWidth || 190, 190);
            var height = menu.offsetHeight || 180;
            var gap = 6, pad = 8;
            var rtl = getComputedStyle(trigger).direction === 'rtl';
            var left = rtl ? rect.right - width : rect.left;
            left = Math.max(pad, Math.min(left, window.innerWidth - width - pad));
            var below = window.innerHeight - rect.bottom;
            var top = below >= height + gap || rect.top < height + gap
                ? rect.bottom + gap : rect.top - height - gap;
            top = Math.max(pad, Math.min(top, window.innerHeight - height - pad));
            menu.style.left = left + 'px';
            menu.style.top = top + 'px';
            menu.style.visibility = 'visible';

            menu._vzToggle = function () {
                if (!menu.matches(':popover-open')) {
                    menu.dataset.vzOpen = 'false';
                    try { dotNetRef.invokeMethodAsync('OnContextMenuClosed'); } catch (e) { }
                }
            };
            menu.addEventListener('toggle', menu._vzToggle);
            requestAnimationFrame(function () {
                var focusable = menu.querySelector('a[href],button:not([disabled]),[tabindex="0"]');
                if (focusable) focusable.focus({ preventScroll: true });
            });
            return true;
        },
        closeContextMenu: function (menu, trigger) {
            if (!menu) return;
            try { if (menu.matches(':popover-open') && menu.hidePopover) menu.hidePopover(); } catch (e) { }
            menu.dataset.vzOpen = 'false';
            menu.style.display = '';
            if (trigger) try { trigger.focus({ preventScroll: true }); } catch (e) { }
        },
        disposeContextMenu: function (menu) {
            if (!menu) return;
            if (menu._vzToggle) menu.removeEventListener('toggle', menu._vzToggle);
            menu._vzToggle = null;
            menu._vzDotNet = null;
        },
        // Client-side file download (used for CSV export — no backend endpoint required).
        downloadText: function (fileName, text, mime) {
            try {
                var blob = new Blob(["﻿" + text], { type: (mime || 'text/csv') + ';charset=utf-8;' });
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = fileName;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
            } catch (e) { }
        },
        // Copy text to clipboard, with a legacy fallback for non-secure contexts.
        copy: async function (text) {
            try {
                if (navigator.clipboard && window.isSecureContext) {
                    await navigator.clipboard.writeText(text);
                    return true;
                }
            } catch (e) { /* fall through */ }
            try {
                var ta = document.createElement('textarea');
                ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0';
                document.body.appendChild(ta); ta.focus(); ta.select();
                var ok = document.execCommand('copy');
                document.body.removeChild(ta);
                return ok;
            } catch (e) { return false; }
        },
        // Opens a self-contained, print-ready order invoice. Dynamic values are escaped before
        // HTML is assembled, so customer/product data can never become executable markup.
        openInvoice: function (invoice) {
            var popup = window.open('', '_blank', 'width=900,height=760');
            if (!popup) return false;
            try { popup.opener = null; } catch (e) { }

            var escape = function (value) {
                return String(value == null ? '' : value)
                    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
            };
            var row = function (label, value) {
                return '<div class="summary-row"><span>' + escape(label) + '</span><strong>' + escape(value) + '</strong></div>';
            };
            var items = (invoice.items || []).map(function (item) {
                var title = escape(item.title) + (item.variant ? '<small>' + escape(item.variant) + '</small>' : '');
                return '<tr><td>' + title + '</td><td>' + escape(item.quantity) + '</td><td>' + escape(item.unitPrice) + '</td><td>' + escape(item.totalPrice) + '</td></tr>';
            }).join('');
            var customer = row('نام مشتری', invoice.customerName) + row('موبایل', invoice.customerMobile) +
                (invoice.customerEmail ? row('ایمیل', invoice.customerEmail) : '');
            var totals = row('جمع اقلام', invoice.subtotal) + row('تخفیف', invoice.discount) +
                (invoice.vat ? row(invoice.vatLabel, invoice.vat) : '') + row('مبلغ نهایی', invoice.total);
            var completion = invoice.completedAt ? '<div><b>زمان نهایی شدن:</b> ' + escape(invoice.completedAt) + '</div>' : '';

            popup.document.open();
            popup.document.write('<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><title>' + escape(invoice.title) + ' ' + escape(invoice.orderNumber) + '</title><style>' +
                '@page{size:A4;margin:16mm}*{box-sizing:border-box}body{font-family:Tahoma,Arial,sans-serif;color:#172033;margin:0;font-size:13px;line-height:1.8}.invoice{max-width:800px;margin:auto}.head{display:flex;justify-content:space-between;align-items:flex-start;border-bottom:2px solid #0f766e;padding-bottom:16px;margin-bottom:20px}.brand{font-size:24px;font-weight:800;color:#0f766e}.muted{color:#64748b}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-bottom:20px}.box{border:1px solid #dbe3ee;border-radius:8px;padding:14px}.box h2{font-size:14px;margin:0 0 8px}.summary-row{display:flex;justify-content:space-between;gap:16px;border-bottom:1px dashed #e2e8f0;padding:5px 0}.summary-row:last-child{border:0}.summary-row:last-child strong{color:#0f766e;font-size:15px}table{width:100%;border-collapse:collapse;margin:18px 0}th{background:#f1f5f9}th,td{border:1px solid #dbe3ee;padding:9px;text-align:right;vertical-align:top}small{display:block;color:#64748b;font-size:11px;margin-top:2px}.foot{border-top:1px solid #dbe3ee;padding-top:10px;color:#64748b;font-size:11px}@media print{.invoice{max-width:none}}</style></head><body><main class="invoice">' +
                '<header class="head"><div><div class="brand">ویتورایز</div><div class="muted">' + escape(invoice.title) + '</div></div><div><div><b>شماره سفارش:</b> ' + escape(invoice.orderNumber) + '</div><div><b>تاریخ ثبت:</b> ' + escape(invoice.createdAt) + '</div>' + completion + '</div></header>' +
                '<section class="grid"><div class="box"><h2>اطلاعات مشتری</h2>' + customer + '</div><div class="box"><h2>وضعیت سفارش</h2>' + row('وضعیت سفارش', invoice.orderStatus) + row('وضعیت پرداخت', invoice.paymentStatus) + '</div></section>' +
                '<table><thead><tr><th>محصول</th><th>تعداد</th><th>قیمت واحد</th><th>جمع</th></tr></thead><tbody>' + items + '</tbody></table>' +
                '<section class="box" style="max-width:360px;margin-right:auto"><h2>جمع‌بندی مبلغ</h2>' + totals + '</section><footer class="foot">این فاکتور از پنل مدیریت ویتورایز در ' + escape(new Date().toLocaleString('fa-IR')) + ' صادر شده است.</footer></main><script>window.onload=function(){window.print();};<\/script></body></html>');
            popup.document.close();
            return true;
        },
        // Global keyboard shortcuts. Forwards an allow-list of keys to .NET, ignoring
        // keystrokes typed inside form fields (except Escape).
        registerShortcuts: function (dotNetRef, methodName) {
            this.unregisterShortcuts();
            var allowed = ['/', 'r', 'n', '?', 'Escape'];
            var handler = function (e) {
                if (e.ctrlKey || e.metaKey || e.altKey) return;
                if (allowed.indexOf(e.key) === -1) return;
                var t = e.target;
                var tag = (t && t.tagName || '').toLowerCase();
                var typing = tag === 'input' || tag === 'textarea' || tag === 'select' || (t && t.isContentEditable);
                if (typing && e.key !== 'Escape') return;
                if (e.key === '/' || e.key === '?') e.preventDefault();
                dotNetRef.invokeMethodAsync(methodName, e.key);
            };
            window._vzKeys = handler;
            document.addEventListener('keydown', handler);
        },
        unregisterShortcuts: function () {
            if (window._vzKeys) {
                document.removeEventListener('keydown', window._vzKeys);
                window._vzKeys = null;
            }
        }
    };
})();

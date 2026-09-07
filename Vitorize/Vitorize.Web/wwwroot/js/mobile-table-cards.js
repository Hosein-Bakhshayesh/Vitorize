// Give every ordinary admin/storefront table a readable card layout on phones.
// Razor pages keep their semantic <thead>; this lightweight enhancer mirrors each header into
// a data attribute so CSS can show the label beside its value without duplicating markup per page.
(function () {
    var phone = window.matchMedia('(max-width: 760px)');
    var tableSelector = '.vz-table-wrap table, .st-table-wrap table';

    function apply(scope) {
        var root = scope && scope.querySelectorAll ? scope : document;
        var tables = [];
        if (root.matches && root.matches(tableSelector)) tables.push(root);
        if (root.closest) {
            var containingTable = root.closest(tableSelector);
            if (containingTable) tables.push(containingTable);
        }
        root.querySelectorAll(tableSelector).forEach(function (table) { tables.push(table); });

        Array.from(new Set(tables)).forEach(function (table) {
            var wrap = table.closest('.vz-table-wrap, .st-table-wrap');
            if (!wrap || wrap.classList.contains('orders-detail-table') || wrap.dataset.mobileLayout === 'preserve') return;

            var headers = Array.prototype.map.call(table.querySelectorAll('thead th'), function (header) {
                return (header.textContent || '').replace(/\s+/g, ' ').trim() || 'اطلاعات';
            });
            table.querySelectorAll('tbody tr').forEach(function (row) {
                Array.prototype.forEach.call(row.children, function (cell, index) {
                    if (cell.tagName === 'TD') cell.dataset.mobileLabel = headers[index] || 'اطلاعات';
                });
            });
            wrap.classList.toggle('vz-mobile-cards', phone.matches && !!headers.length);
            wrap.classList.toggle('st-mobile-cards', phone.matches && !!headers.length);
        });
    }

    function refresh() { apply(document); }
    function start() {
        refresh();
        new MutationObserver(function (changes) {
            changes.forEach(function (change) {
                change.addedNodes.forEach(function (node) {
                    if (node.nodeType === 1) apply(node.parentElement || document);
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
        phone.addEventListener ? phone.addEventListener('change', refresh) : phone.addListener(refresh);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
    else start();
})();

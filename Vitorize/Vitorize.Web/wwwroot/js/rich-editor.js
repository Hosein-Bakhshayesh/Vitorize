(function () {
    const editors = new WeakMap();
    if (window.Quill) {
        const BlockEmbed = Quill.import('blots/block/embed');
        class DividerBlot extends BlockEmbed { static blotName = 'divider'; static tagName = 'hr'; }
        Quill.register(DividerBlot, true);
    }
    window.vzRichEditor = {
        create: function (host, dotNetRef, html, placeholder) {
            if (!host || !window.Quill || editors.has(host)) return;
            const quill = new Quill(host, {
                theme: 'snow', placeholder: placeholder || '',
                modules: { history: { delay: 800, maxStack: 100, userOnly: true }, toolbar: [
                    [{ header: [2, 3, 4, false] }], ['bold', 'italic', 'underline', 'strike'],
                    [{ list: 'ordered' }, { list: 'bullet' }], ['blockquote', 'code-block'],
                    [{ align: [] }, { direction: 'rtl' }], ['link'], ['blockquote'], ['table', 'divider'], ['clean'], ['undo', 'redo', 'fullscreen']
                ] }
            });
            const toolbar = quill.getModule('toolbar');
            toolbar.addHandler('undo', function () { quill.history.undo(); });
            toolbar.addHandler('redo', function () { quill.history.redo(); });
            toolbar.addHandler('divider', function () { const range = quill.getSelection(true); quill.insertEmbed(range.index, 'divider', true, 'user'); quill.setSelection(range.index + 1, 0); });
            toolbar.addHandler('table', function () { const table = quill.getModule('table'); if (table) table.insertTable(2, 2); });
            toolbar.addHandler('fullscreen', function () { host.closest('.vz-field')?.classList.toggle('vz-editor-fullscreen'); });
            quill.clipboard.dangerouslyPasteHTML(html || '');
            let timer;
            const changed = function (delta, old, source) {
                if (source !== 'user') return;
                clearTimeout(timer);
                timer = setTimeout(function () { dotNetRef.invokeMethodAsync('OnHtmlChanged', quill.root.innerHTML); }, 200);
            };
            quill.on('text-change', changed);
            editors.set(host, { quill: quill, changed: changed, timer: timer });
        },
        setHtml: function (host, html) {
            const state = editors.get(host);
            if (state && state.quill.root.innerHTML !== (html || '')) state.quill.clipboard.dangerouslyPasteHTML(html || '');
        },
        dispose: function (host) {
            const state = editors.get(host);
            if (!state) return;
            clearTimeout(state.timer); state.quill.off('text-change', state.changed); editors.delete(host);
        }
    };
})();

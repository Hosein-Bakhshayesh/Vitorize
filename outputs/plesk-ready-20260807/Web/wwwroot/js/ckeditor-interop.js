// CKEditor 5 interop for the Vitorize admin.
// Self-hosted UMD build (window.CKEDITOR). One reusable module drives every
// RichTextEditor instance; the Blazor component owns lifecycle via the id.
(function () {
    "use strict";

    // id -> { editor, dotNetRef, lastData, timer, opts }
    const instances = new Map();

    function ck() { return window.CKEDITOR; }

    // Restrict headings to h2–h4 so the editor never emits tags the server
    // sanitizer would strip (keeps round-tripping lossless).
    function headingConfig() {
        return {
            options: [
                { model: "paragraph", title: "متن معمولی", class: "ck-heading_paragraph" },
                { model: "heading2", view: "h2", title: "عنوان بزرگ (H2)", class: "ck-heading_heading2" },
                { model: "heading3", view: "h3", title: "عنوان متوسط (H3)", class: "ck-heading_heading3" },
                { model: "heading4", view: "h4", title: "عنوان کوچک (H4)", class: "ck-heading_heading4" }
            ]
        };
    }

    function buildConfig(CK, opts) {
        const rtl = opts.direction !== "ltr";
        const plugins = [
            CK.Essentials, CK.Paragraph, CK.Heading, CK.Bold, CK.Italic, CK.Underline,
            CK.Strikethrough, CK.Subscript, CK.Superscript, CK.RemoveFormat,
            CK.Link, CK.AutoLink, CK.List, CK.Indent, CK.IndentBlock, CK.BlockQuote,
            CK.Alignment, CK.Autoformat, CK.TextTransformation, CK.PasteFromOffice,
            CK.Table, CK.TableToolbar, CK.TableCaption, CK.TableProperties,
            CK.TableCellProperties, CK.TableColumnResize,
            CK.Image, CK.ImageToolbar, CK.ImageCaption, CK.ImageStyle, CK.ImageResize,
            CK.ImageInsert, CK.ImageUpload, CK.AutoImage, CK.LinkImage, CK.PictureEditing,
            CK.SimpleUploadAdapter, CK.CodeBlock, CK.HorizontalLine, CK.FindAndReplace,
            CK.SourceEditing, CK.GeneralHtmlSupport
        ].filter(Boolean);

        const toolbar = {
            items: [
                "sourceEditing", "|",
                "undo", "redo", "|",
                "heading", "|",
                "bold", "italic", "underline", "strikethrough", "removeFormat", "|",
                "link", "insertImage", "insertTable", "blockQuote", "codeBlock", "horizontalLine", "|",
                "bulletedList", "numberedList", "outdent", "indent", "|",
                "alignment", "|",
                "findAndReplace"
            ],
            shouldNotGroupWhenFull: false
        };

        return {
            // Supplied by the server after environment validation — never defaulted
            // here so a Production node can't silently fall back to GPL.
            licenseKey: opts.licenseKey,
            language: { ui: "fa", content: rtl ? "fa" : "en" },
            initialData: opts.value || "",
            placeholder: opts.placeholder || "",
            plugins: plugins,
            toolbar: toolbar,
            heading: headingConfig(),
            alignment: { options: ["left", "center", "right", "justify"] },
            link: { addTargetToExternalLinks: true, defaultProtocol: "https://" },
            image: {
                resizeUnit: "%",
                toolbar: [
                    "imageTextAlternative", "toggleImageCaption", "|",
                    "imageStyle:inline", "imageStyle:alignLeft", "imageStyle:alignCenter", "imageStyle:alignRight", "|",
                    "resizeImage"
                ]
            },
            table: {
                contentToolbar: ["tableColumn", "tableRow", "mergeTableCells", "tableProperties", "tableCellProperties"]
            },
            simpleUpload: {
                uploadUrl: opts.imageUploadUrl,
                withCredentials: true,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            },
            // The server sanitizer is the security gate; GHS only lets safe
            // direction/alignment round-trip through source editing and paste.
            htmlSupport: {
                allow: [
                    {
                        name: /^(p|h2|h3|h4|blockquote|ul|ol|li|td|th|figure|figcaption|span|div|pre|code)$/,
                        attributes: ["dir"],
                        styles: ["text-align"]
                    }
                ]
            }
        };
    }

    async function destroyInstance(id) {
        const rec = instances.get(id);
        if (!rec) return;
        instances.delete(id);
        clearTimeout(rec.timer);
        try { await rec.editor.destroy(); } catch (e) { /* already torn down */ }
    }

    const api = {
        // Returns true on success so the component can flip its ready flag.
        create: async function (host, dotNetRef, id, opts) {
            const CK = ck();
            if (!host || !CK || !CK.ClassicEditor) {
                console.error("[vzCkEditor] CKEditor build not loaded.");
                return false;
            }
            // Guard against duplicate init after enhanced navigation / rerender.
            if (instances.has(id)) { await destroyInstance(id); }
            if (host.dataset.ckeInitialized === "1") { return false; }
            host.dataset.ckeInitialized = "1";

            let editor;
            try {
                editor = await CK.ClassicEditor.create(host, buildConfig(CK, opts));
            } catch (e) {
                host.dataset.ckeInitialized = "";
                console.error("[vzCkEditor] init failed", e);
                return false;
            }

            const rec = { editor: editor, dotNetRef: dotNetRef, lastData: opts.value || "", timer: null, opts: opts, host: host };
            instances.set(id, rec);

            if (opts.readOnly || opts.disabled) { editor.enableReadOnlyMode("vz-blazor"); }

            editor.model.document.on("change:data", function () {
                clearTimeout(rec.timer);
                rec.timer = setTimeout(function () {
                    const data = editor.getData();
                    if (data === rec.lastData) return;
                    rec.lastData = data;
                    rec.dotNetRef.invokeMethodAsync("OnEditorChanged", data);
                }, 220);
            });

            return true;
        },

        // Push a new value from Blazor without echoing it back as a user edit.
        setData: function (id, html) {
            const rec = instances.get(id);
            if (!rec) return;
            const value = html || "";
            if (rec.editor.getData() === value) return;
            rec.lastData = value;
            rec.editor.setData(value);
        },

        setReadOnly: function (id, readOnly) {
            const rec = instances.get(id);
            if (!rec) return;
            if (readOnly) { rec.editor.enableReadOnlyMode("vz-blazor"); }
            else { rec.editor.disableReadOnlyMode("vz-blazor"); }
        },

        // CSS-only fullscreen wrapper (Fullscreen plugin is not in this build).
        toggleFullscreen: function (id) {
            const rec = instances.get(id);
            if (!rec) return false;
            const shell = rec.host.closest(".vz-ck");
            if (!shell) return false;
            const active = shell.classList.toggle("vz-ck--fullscreen");
            document.body.classList.toggle("vz-ck-fullscreen-lock", active);
            return active;
        },

        // Upload a picked file to the attachment endpoint and insert a link.
        pickAndAttach: function (id, uploadUrl) {
            const rec = instances.get(id);
            if (!rec) return;
            const input = document.createElement("input");
            input.type = "file";
            input.style.display = "none";
            document.body.appendChild(input);
            input.addEventListener("change", function () {
                const file = input.files && input.files[0];
                document.body.removeChild(input);
                if (!file) return;
                uploadAttachment(rec, uploadUrl, file);
            });
            input.click();
        },

        dispose: function (id) {
            const rec = instances.get(id);
            if (rec && rec.host) { rec.host.dataset.ckeInitialized = ""; }
            const shell = rec && rec.host ? rec.host.closest(".vz-ck") : null;
            if (shell && shell.classList.contains("vz-ck--fullscreen")) {
                document.body.classList.remove("vz-ck-fullscreen-lock");
            }
            return destroyInstance(id);
        }
    };

    function uploadAttachment(rec, uploadUrl, file) {
        rec.dotNetRef.invokeMethodAsync("OnAttachmentStatus", "در حال آپلود فایل…", false);
        const data = new FormData();
        data.append("file", file);
        const xhr = new XMLHttpRequest();
        xhr.open("POST", uploadUrl, true);
        xhr.responseType = "json";
        xhr.withCredentials = true;
        xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
        xhr.addEventListener("load", function () {
            const res = xhr.response;
            if (xhr.status < 200 || xhr.status >= 300 || !res || res.error) {
                const msg = (res && res.error && res.error.message) || "آپلود فایل ناموفق بود.";
                rec.dotNetRef.invokeMethodAsync("OnAttachmentStatus", msg, true);
                return;
            }
            insertLink(rec.editor, res.url, file.name);
            rec.dotNetRef.invokeMethodAsync("OnAttachmentStatus", "", false);
        });
        xhr.addEventListener("error", function () {
            rec.dotNetRef.invokeMethodAsync("OnAttachmentStatus", "خطا در ارتباط با سرور هنگام آپلود فایل.", true);
        });
        xhr.send(data);
    }

    function insertLink(editor, url, text) {
        editor.model.change(function (writer) {
            const node = writer.createText(text || url, { linkHref: url });
            editor.model.insertContent(node, editor.model.document.selection);
        });
        editor.editing.view.focus();
    }

    window.vzCkEditor = api;
})();

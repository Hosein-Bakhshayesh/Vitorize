(function () {
    const mimeTypes = new Set(["image/jpeg", "image/png", "image/webp"]);
    let current = null;

    const styles = document.createElement("style");
    styles.textContent = `.vz-redaction-modal{position:fixed;inset:0;z-index:2000;display:grid;place-items:center;padding:20px;background:rgba(15,23,42,.7);overflow:auto}.vz-redaction-modal__panel{width:min(100%,1040px);max-height:calc(100vh - 40px);overflow:auto;background:var(--st-surface,var(--surface-solid,#fff));color:var(--st-text,var(--text,#111827));border-radius:16px;padding:18px;box-shadow:0 24px 70px rgba(0,0,0,.35)}.vz-redaction-modal__head{display:flex;align-items:center;justify-content:space-between;gap:16px}.vz-redaction-modal__head h2{font-size:18px;margin:0}.vz-redaction-modal__close{border:0;background:transparent;font-size:28px;cursor:pointer;color:inherit}.vz-redaction-modal__notice{font-weight:700;line-height:1.8;margin:14px 0 4px}.vz-redaction-modal__instructions{color:var(--st-text-2,var(--text-2,#475569));line-height:1.8;margin:0 0 12px}.vz-redaction-modal__canvas-wrap{display:flex;justify-content:center;align-items:center;min-height:220px;overflow:auto;border:1px solid var(--st-border,var(--border,#cbd5e1));border-radius:12px;background:#e2e8f0}.vz-redaction-modal__canvas{display:block;touch-action:none;cursor:crosshair;max-width:none}.vz-redaction-modal__tools,.vz-redaction-modal__actions,.vz-redaction-modal__masks{display:flex;flex-wrap:wrap;gap:8px;margin-top:12px}.vz-redaction-modal__actions{justify-content:flex-end}.vz-redaction-modal__masks{font-size:13px;color:var(--st-text-2,var(--text-2,#475569))}@media(max-width:600px){.vz-redaction-modal{padding:8px}.vz-redaction-modal__panel{max-height:calc(100vh - 16px);padding:14px}.vz-redaction-modal__tools .st-btn{flex:1 1 42%}.vz-redaction-modal__actions .st-btn{flex:1 1 100%}}`;
    document.head.appendChild(styles);

    function removeEditor() {
        if (!current) return;
        current.modal.remove();
        if (current.objectUrl) URL.revokeObjectURL(current.objectUrl);
        current = null;
    }

    function button(text, label) {
        const node = document.createElement("button");
        node.type = "button";
        node.className = "st-btn st-btn--outline";
        node.textContent = text;
        node.setAttribute("aria-label", label || text);
        return node;
    }

    function createEditor(file, uploadInput, instructions) {
        removeEditor();
        const objectUrl = URL.createObjectURL(file);
        const image = new Image();
        image.onload = () => {
            const modal = document.createElement("div");
            modal.className = "vz-redaction-modal";
            modal.setAttribute("role", "dialog");
            modal.setAttribute("aria-modal", "true");
            modal.setAttribute("aria-label", "ویرایش و پوشاندن اطلاعات حساس مدرک");
            modal.innerHTML = '<div class="vz-redaction-modal__panel"><div class="vz-redaction-modal__head"><h2>پوشاندن اطلاعات حساس</h2><button type="button" class="vz-redaction-modal__close" aria-label="بستن">×</button></div><p class="vz-redaction-modal__notice">نسخه نهایی همین تصویر برای احراز هویت ارسال خواهد شد و فایل اصلی ارسال نمی‌شود.</p><p class="vz-redaction-modal__instructions"></p><div class="vz-redaction-modal__canvas-wrap"><canvas class="vz-redaction-modal__canvas" tabindex="0" aria-label="تصویر مدرک؛ برای پوشاندن اطلاعات روی تصویر بکشید"></canvas></div><div class="vz-redaction-modal__tools"></div><div class="vz-redaction-modal__masks" aria-live="polite"></div><div class="vz-redaction-modal__actions"></div></div>';
            document.body.appendChild(modal);
            const canvas = modal.querySelector("canvas");
            const ctx = canvas.getContext("2d");
            const tools = modal.querySelector(".vz-redaction-modal__tools");
            const masksPanel = modal.querySelector(".vz-redaction-modal__masks");
            const actions = modal.querySelector(".vz-redaction-modal__actions");
            const instructionNode = modal.querySelector(".vz-redaction-modal__instructions");
            instructionNode.textContent = instructions || "برای پوشاندن اطلاعات غیرضروری، روی تصویر مستطیل بکشید.";
            const state = { modal, objectUrl, image, canvas, ctx, masks: [], scale: 1, zoom: 1, drawing: null };
            current = state;

            const fit = () => {
                const maxW = Math.max(280, Math.min(window.innerWidth - 64, 960));
                const maxH = Math.max(260, Math.min(window.innerHeight - 390, 620));
                state.scale = Math.min(maxW / image.naturalWidth, maxH / image.naturalHeight) * state.zoom;
                canvas.width = Math.max(1, Math.round(image.naturalWidth * state.scale));
                canvas.height = Math.max(1, Math.round(image.naturalHeight * state.scale));
                draw();
            };
            const drawMask = (mask, preview) => {
                ctx.fillStyle = "#111827";
                ctx.fillRect(mask.x * state.scale, mask.y * state.scale, mask.w * state.scale, mask.h * state.scale);
                if (preview) { ctx.strokeStyle = "#38bdf8"; ctx.lineWidth = 2; ctx.strokeRect(mask.x * state.scale, mask.y * state.scale, mask.w * state.scale, mask.h * state.scale); }
            };
            const draw = () => {
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
                state.masks.forEach(mask => drawMask(mask, false));
                if (state.drawing) drawMask(state.drawing, true);
            };
            const refreshMasks = () => {
                masksPanel.replaceChildren();
                if (!state.masks.length) { masksPanel.textContent = "هنوز ناحیه‌ای پوشانده نشده است."; return; }
                state.masks.forEach((mask, index) => {
                    const item = button(`حذف ناحیه ${index + 1}`, `حذف ناحیه پوشانده‌شده ${index + 1}`);
                    item.onclick = () => { state.masks.splice(index, 1); refreshMasks(); draw(); };
                    masksPanel.appendChild(item);
                });
            };
            const point = event => {
                const rect = canvas.getBoundingClientRect();
                return { x: Math.max(0, Math.min(image.naturalWidth, (event.clientX - rect.left) / state.scale)), y: Math.max(0, Math.min(image.naturalHeight, (event.clientY - rect.top) / state.scale)) };
            };
            canvas.onpointerdown = event => {
                event.preventDefault(); canvas.setPointerCapture(event.pointerId);
                const p = point(event); state.drawing = { x: p.x, y: p.y, w: 0, h: 0 }; draw();
            };
            canvas.onpointermove = event => {
                if (!state.drawing) return;
                const p = point(event); state.drawing.w = p.x - state.drawing.x; state.drawing.h = p.y - state.drawing.y; draw();
            };
            const finish = event => {
                if (!state.drawing) return;
                const m = state.drawing; state.drawing = null;
                if (m.w < 0) { m.x += m.w; m.w = -m.w; } if (m.h < 0) { m.y += m.h; m.h = -m.h; }
                if (m.w >= 2 && m.h >= 2) state.masks.push(m);
                refreshMasks(); draw();
            };
            canvas.onpointerup = finish; canvas.onpointercancel = finish;
            const zoomOut = button("−", "کوچک‌نمایی"); zoomOut.onclick = () => { state.zoom = Math.max(.35, state.zoom - .2); fit(); };
            const zoomIn = button("+", "بزرگ‌نمایی"); zoomIn.onclick = () => { state.zoom = Math.min(3, state.zoom + .2); fit(); };
            const fitButton = button("اندازه مناسب", "نمایش کامل تصویر"); fitButton.onclick = () => { state.zoom = 1; fit(); };
            const undo = button("بازگشت", "حذف آخرین ناحیه"); undo.onclick = () => { state.masks.pop(); refreshMasks(); draw(); };
            const clear = button("پاک کردن همه", "حذف همه ناحیه‌ها"); clear.onclick = () => { state.masks = []; refreshMasks(); draw(); };
            const reset = button("بازنشانی تصویر", "پاک کردن تغییرات و بازگردانی تصویر"); reset.onclick = () => { state.masks = []; state.zoom = 1; refreshMasks(); fit(); };
            tools.append(zoomOut, zoomIn, fitButton, undo, clear, reset);
            const cancel = button("انصراف", "انصراف بدون آپلود"); cancel.onclick = removeEditor;
            const confirm = button("تأیید و آپلود نسخه نهایی", "تأیید و آپلود نسخه پوشانده‌شده"); confirm.className = "st-btn st-btn--primary";
            confirm.onclick = () => {
                confirm.disabled = true;
                const output = document.createElement("canvas"); output.width = image.naturalWidth; output.height = image.naturalHeight;
                const outputContext = output.getContext("2d"); outputContext.drawImage(image, 0, 0);
                outputContext.fillStyle = "#111827";
                state.masks.forEach(mask => outputContext.fillRect(mask.x, mask.y, mask.w, mask.h));
                output.toBlob(blob => {
                    if (!blob) { confirm.disabled = false; return; }
                    const finalFile = new File([blob], "redacted-document.png", { type: "image/png" });
                    const transfer = new DataTransfer(); transfer.items.add(finalFile); uploadInput.files = transfer.files;
                    uploadInput.dispatchEvent(new Event("change", { bubbles: true }));
                    removeEditor();
                }, "image/png");
            };
            actions.append(cancel, confirm);
            modal.querySelector(".vz-redaction-modal__close").onclick = removeEditor;
            modal.addEventListener("click", event => { if (event.target === modal) removeEditor(); });
            modal.addEventListener("keydown", event => { if (event.key === "Escape") removeEditor(); });
            refreshMasks(); fit(); canvas.focus();
        };
        image.onerror = removeEditor;
        image.src = objectUrl;
    }

    window.vzDocumentRedaction = {
        choose: function (sourceId, uploadId, instructions) {
            const source = document.getElementById(sourceId); const upload = document.getElementById(uploadId);
            if (!source || !upload) return;
            source.value = "";
            source.onchange = async () => {
                const original = source.files && source.files[0];
                source.value = "";
                if (!original) return;
                let file = original;
                try {
                    if (window.vzImageUpload && window.vzImageUpload.isHeic(original))
                        file = await window.vzImageUpload.normalizeHeicFile(original);
                } catch {
                    window.alert("تبدیل تصویر HEIC ممکن نیست. لطفاً تصویر را به JPEG تبدیل کنید.");
                    return;
                }
                if (!mimeTypes.has(file.type)) {
                    window.alert("فرمت تصویر مجاز نیست. فرمت‌های مجاز: JPG، JPEG، PNG، WEBP و HEIC.");
                    return;
                }
                createEditor(file, upload, instructions);
            };
            source.click();
        }
    };
})();

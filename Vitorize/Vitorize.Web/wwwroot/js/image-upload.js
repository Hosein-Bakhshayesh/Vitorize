(function () {
    const heicTypes = new Set(["image/heic", "image/heif"]);
    const heicExtensions = new Set(["heic", "heif"]);

    function extensionOf(name) {
        const value = String(name || "");
        const dot = value.lastIndexOf(".");
        return dot < 0 ? "" : value.slice(dot + 1).toLowerCase();
    }

    function isHeic(file) {
        return heicTypes.has(String(file && file.type || "").toLowerCase()) ||
            heicExtensions.has(extensionOf(file && file.name));
    }

    function bytesToArray(bytes) {
        if (bytes instanceof Uint8Array) return bytes;
        if (Array.isArray(bytes)) return new Uint8Array(bytes);
        if (bytes && typeof bytes === "object" && typeof bytes["__byte[]"] === "string") {
            return bytesToArray(bytes["__byte[]"]);
        }
        if (typeof bytes === "string") {
            const binary = atob(bytes);
            const output = new Uint8Array(binary.length);
            for (let index = 0; index < binary.length; index++) output[index] = binary.charCodeAt(index);
            return output;
        }
        return new Uint8Array(bytes || []);
    }

    function loadImage(blob) {
        return new Promise((resolve, reject) => {
            const url = URL.createObjectURL(blob);
            const image = new Image();
            image.onload = () => { URL.revokeObjectURL(url); resolve(image); };
            image.onerror = () => { URL.revokeObjectURL(url); reject(new Error("مرورگر نتوانست تصویر HEIC را بخواند.")); };
            image.src = url;
        });
    }

    function toJpegBlob(image) {
        return new Promise((resolve, reject) => {
            const canvas = document.createElement("canvas");
            canvas.width = image.naturalWidth;
            canvas.height = image.naturalHeight;
            const context = canvas.getContext("2d");
            if (!context || !canvas.width || !canvas.height) {
                reject(new Error("تبدیل تصویر HEIC ممکن نیست."));
                return;
            }
            context.fillStyle = "#ffffff";
            context.fillRect(0, 0, canvas.width, canvas.height);
            context.drawImage(image, 0, 0);
            canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("تبدیل تصویر HEIC ممکن نیست.")), "image/jpeg", 0.92);
        });
    }

    async function convertBlob(blob) {
        const image = await loadImage(blob);
        return await toJpegBlob(image);
    }

    function base64Of(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onerror = () => reject(new Error("خواندن تصویر تبدیل‌شده ممکن نیست."));
            reader.onload = () => resolve(String(reader.result).split(",", 2)[1] || "");
            reader.readAsDataURL(blob);
        });
    }

    window.vzImageUpload = {
        isHeic,

        // Used by the document-redaction editor, which already owns the browser File object.
        normalizeHeicFile: async function (file) {
            if (!isHeic(file)) return file;
            const jpeg = await convertBlob(file);
            const baseName = String(file.name || "document").replace(/\.[^.]+$/, "");
            return new File([jpeg], `${baseName}.jpeg`, { type: "image/jpeg", lastModified: Date.now() });
        },

        // Used by Blazor's InputFile component. The bytes are converted locally and only the JPEG
        // is returned to .NET for the normal upload request.
        convertHeicToJpeg: async function (bytes) {
            const jpeg = await convertBlob(new Blob([bytesToArray(bytes)], { type: "image/heic" }));
            return { base64: await base64Of(jpeg) };
        }
    };
})();

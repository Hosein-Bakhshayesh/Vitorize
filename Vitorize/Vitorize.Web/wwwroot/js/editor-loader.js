// Shared, route-independent loader for the self-hosted CKEditor assets.
// The promise is stored on window so multiple editor instances never race or
// download/initialize the UMD build more than once.
const stateKey = "__vitorizeEditorAssetLoad";

function addStyle(url) {
    if (Array.from(document.querySelectorAll("link[data-vz-editor-url]")).some(link => link.dataset.vzEditorUrl === url)) return;
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = url;
    link.dataset.vzEditorUrl = url;
    document.head.appendChild(link);
}

function addScript(url) {
    const current = Array.from(document.querySelectorAll("script[data-vz-editor-url]")).find(script => script.dataset.vzEditorUrl === url);
    if (current) {
        if (current.dataset.vzEditorLoaded === "true") return Promise.resolve();
        if (current.dataset.vzEditorFailed === "true") current.remove();
        else return new Promise((resolve, reject) => {
            current.addEventListener("load", resolve, { once: true });
            current.addEventListener("error", () => reject(new Error(`Unable to load ${url}`)), { once: true });
        });
    }

    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = url;
        script.async = true;
        script.dataset.vzEditorUrl = url;
        script.addEventListener("load", () => { script.dataset.vzEditorLoaded = "true"; resolve(); }, { once: true });
        script.addEventListener("error", () => { script.dataset.vzEditorFailed = "true"; reject(new Error(`Unable to load ${url}`)); }, { once: true });
        document.head.appendChild(script);
    });
}

export function ensure(assets) {
    if (!window[stateKey]) {
        window[stateKey] = (async () => {
            addStyle(assets.bundleCss);
            addStyle(assets.themeCss);
            await addScript(assets.bundle);
            await addScript(assets.translation);
            await addScript(assets.interop);
            if (!window.vzCkEditor || !window.CKEDITOR) {
                throw new Error("CKEditor assets loaded without their expected globals.");
            }
            return true;
        })().catch(error => {
            // A later navigation can retry after a transient network/deployment error.
            window[stateKey] = null;
            throw error;
        });
    }
    return window[stateKey];
}

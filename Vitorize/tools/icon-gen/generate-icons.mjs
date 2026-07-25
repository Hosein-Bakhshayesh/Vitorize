// Vitorize icon-set generator.
//
// Fetches a *curated* subset of extra Iconify collections and emits, per collection:
//   1. a self-hosted SVG sprite  -> Vitorize.Web/wwwroot/lib/icons/<prefix>-sprite.svg
//   2. a C# catalog data file    -> Vitorize.Shared/Icons/IconCatalog.<Prefix>.Generated.cs
//
// Output is committed; the running app never calls Iconify. Re-run only to
// refresh or extend the curated set. We deliberately do NOT bundle whole
// collections (tens of thousands of icons) — only the domain-relevant subset,
// keeping sprites small and the picker fast.
//
// Usage:  node tools/icon-gen/generate-icons.mjs
//
// Rendering: each <symbol> bakes its own presentation group so a single
// <use> renderer works for stroke sets (Tabler/Lucide-like) and fill sets
// (Phosphor/brand glyphs) alike.

import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "..", "..");
const spriteDir = resolve(repoRoot, "Vitorize.Web/wwwroot/lib/icons");
const catalogDir = resolve(repoRoot, "Vitorize.Shared/Icons");

// Curated Tabler set: the brand / payment / gaming / commerce glyphs Lucide
// lacks, plus a few high-use UI icons. Names are canonical Tabler ids.
const TABLER = [
    // Gaming platforms & brands
    "brand-steam", "brand-xbox", "brand-twitch",
    "brand-discord", "brand-telegram", "brand-whatsapp", "brand-instagram", "brand-facebook",
    "brand-x", "brand-twitter", "brand-youtube", "brand-tiktok", "brand-linkedin",
    "brand-google", "brand-google-play", "brand-apple", "brand-android",
    "brand-windows", "brand-ubuntu", "brand-chrome", "brand-github", "brand-gitlab",
    "brand-visa", "brand-mastercard", "brand-paypal", "brand-cashapp",
    "brand-stripe", "brand-amazon", "brand-figma", "brand-spotify",
    // Gaming & entertainment
    "device-gamepad", "device-gamepad-2", "device-gamepad-3", "dice",
    "dice-5", "sword", "shield-check", "trophy", "medal", "crown", "target-arrow",
    // Commerce / payments / wallet
    "coin", "coins", "wallet", "credit-card", "credit-card-pay", "cash", "cash-banknote",
    "receipt", "receipt-2", "discount", "gift", "gift-card", "shopping-cart",
    "shopping-bag", "shopping-cart-plus", "package", "packages", "building-store", "barcode",
    "qrcode", "tag", "tags", "ticket", "current-location",
    // Support / communication
    "headset", "headphones", "message", "messages", "mail", "send", "phone",
    "bell", "bell-ringing", "help", "help-circle", "info-circle",
    // Users / security
    "user", "users", "user-check", "user-plus", "shield", "shield-lock", "lock",
    "key", "fingerprint", "id", "id-badge-2",
    // System / dev / infra
    "server", "database", "cloud", "cloud-computing", "code", "terminal-2", "cpu",
    "settings", "adjustments", "dashboard", "chart-bar", "chart-line", "chart-pie",
    "activity", "world", "rocket", "flame", "sparkles", "bolt",
    // Files / media / general
    "file", "file-text", "file-invoice", "folder", "photo", "video", "music",
    "calendar", "clock", "map-pin", "star", "heart", "bookmark", "check", "x",
    "plus", "minus", "trash", "edit", "download", "upload", "search", "filter",
    "refresh", "eye", "eye-off", "home", "menu-2", "dots"
];

// Curated Phosphor (fill) set — decorative variety with a different visual weight.
const PHOSPHOR = [
    "game-controller-fill", "sword-fill", "shield-fill", "crown-fill", "trophy-fill",
    "coin-fill", "wallet-fill", "credit-card-fill", "storefront-fill", "package-fill",
    "gift-fill", "ticket-fill", "headset-fill", "chat-circle-fill", "bell-fill",
    "rocket-fill", "lightning-fill", "star-fill", "heart-fill", "fire-fill",
    "medal-fill", "cube-fill", "gauge-fill", "lock-key-fill", "user-circle-fill",
    "shopping-cart-fill", "tag-fill", "percent-fill", "diamond-fill", "crown-simple-fill"
];

const COLLECTIONS = [
    { prefix: "tabler", title: "Tabler", persian: "تبلر", names: TABLER },
    { prefix: "ph", title: "Phosphor", persian: "فسفر", names: PHOSPHOR }
];

function curlJson(url) {
    for (let attempt = 1; attempt <= 4; attempt++) {
        try {
            const out = execFileSync("curl", ["-sSL", "-m", "30", url],
                { maxBuffer: 64 * 1024 * 1024, encoding: "utf8" });
            return JSON.parse(out);
        } catch (e) {
            if (attempt === 4) throw e;
        }
    }
}

function chunk(arr, size) {
    const out = [];
    for (let i = 0; i < arr.length; i += size) out.push(arr.slice(i, i + size));
    return out;
}

function fetchCollection(prefix, names) {
    // node's global fetch is blocked here; curl works. Small batches keep each
    // request light on a flaky connection.
    const merged = { width: undefined, height: undefined, icons: {} };
    for (const batch of chunk(names, 12)) {
        const url = `https://api.iconify.design/${prefix}.json?icons=${batch.join(",")}`;
        const data = curlJson(url);
        merged.width = merged.width || data.width;
        merged.height = merged.height || data.height;
        Object.assign(merged.icons, data.icons || {});
    }
    return merged;
}

function bakeSymbol(id, body, width, height) {
    const vb = `0 0 ${width} ${height}`;
    // Tabler ships self-contained bodies (own stroke/fill group). Phosphor ships
    // bare paths that inherit fill from the host — wrap those in a fill group so
    // the shared <use> renderer never applies an unwanted outline.
    const selfContained = /stroke\s*=|fill\s*=/.test(body);
    const content = selfContained ? body : `<g fill="currentColor" stroke="none">${body}</g>`;
    return `<symbol id="${id}" viewBox="${vb}">${content}</symbol>`;
}

function csIdentifier(prefix) {
    return prefix.charAt(0).toUpperCase() + prefix.slice(1);
}

function esc(s) { return s.replace(/\\/g, "\\\\").replace(/"/g, "\\\""); }

async function run() {
    mkdirSync(spriteDir, { recursive: true });
    mkdirSync(catalogDir, { recursive: true });

    for (const col of COLLECTIONS) {
        const data = await fetchCollection(col.prefix, col.names);
        const defW = data.width || 24;
        const defH = data.height || 24;
        const icons = data.icons || {};
        const symbols = [];
        const items = [];

        for (const name of col.names) {
            const icon = icons[name];
            if (!icon || !icon.body) { console.warn(`  ! missing ${col.prefix}:${name}`); continue; }
            const w = icon.width || defW;
            const h = icon.height || defH;
            symbols.push(bakeSymbol(name, icon.body, w, h));
            const words = name.replace(/[-_]/g, " ");
            items.push(`        ("${esc(name)}", "${esc(words)}")`);
        }

        const sprite = `<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg">${symbols.join("")}</svg>\n`;
        writeFileSync(resolve(spriteDir, `${col.prefix}-sprite.svg`), sprite, "utf8");

        const cls = csIdentifier(col.prefix);
        const cs = `// <auto-generated />
namespace Vitorize.Shared.Icons;

internal static class IconCatalog${cls}Data
{
    internal const string Prefix = "${col.prefix}";
    internal const string Title = "${col.title}";
    internal const string PersianTitle = "${col.persian}";
    internal static readonly (string Name, string Keywords)[] Items =
    [
${items.join(",\n")}
    ];
}
`;
        writeFileSync(resolve(catalogDir, `IconCatalog.${cls}.Generated.cs`), cs, "utf8");
        console.log(`  ${col.prefix}: ${symbols.length}/${col.names.length} icons -> sprite + catalog`);
    }
}

run().catch(e => { console.error(e); process.exit(1); });

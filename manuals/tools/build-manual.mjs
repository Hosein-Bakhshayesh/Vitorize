// Vitorize manual builder — HTML -> A4 PDF using Chromium's native print engine.
//
// Why native print: Paged.js mis-paginates long RTL chapters (it stops emitting pages inside a
// chapter and clips the overflow), so pagination is left to Chromium. Chromium ignores @page
// margin boxes, so the running header and page number come from headerTemplate/footerTemplate.
//
// Table of contents: Chromium cannot resolve target-counter(), so page numbers are resolved in two
// passes. Each chapter heading carries an invisible ASCII marker (#c1#). Pass 1 renders a draft,
// pdf.js reports which page each marker landed on, those numbers are injected into the TOC, and
// pass 2 renders the final document.
//
// Everything is local: the repository stylesheet, bundled Vazirmatn woff2 and local screenshots.
// No CDN.
//
// Usage: node build-manual.mjs <input.html> <output.pdf> [--title "..."] [--raster <dir>]

import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
import { createServer } from 'node:http';
import { readFile, writeFile, mkdir, rm } from 'node:fs/promises';
import path from 'node:path';

const PDFJS = 'D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/pdfjs-dist';
const args = process.argv.slice(2);
const input = path.resolve(args[0]);
const output = path.resolve(args[1]);
const title = valueOf('--title') ?? 'ویتورایز';
const rasterDir = valueOf('--raster');
const root = path.dirname(input);

function valueOf(flag) {
  const i = args.indexOf(flag);
  return i >= 0 ? args[i + 1] : undefined;
}

const MIME = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp',
  '.gif': 'image/gif', '.woff2': 'font/woff2', '.woff': 'font/woff', '.svg': 'image/svg+xml',
  '.pdf': 'application/pdf'
};

/** Serves the manual folder, pdf.js and the freshly produced PDF over loopback. */
function serve(extra = {}) {
  const server = createServer(async (req, res) => {
    try {
      const rel = decodeURIComponent(new URL(req.url, 'http://x').pathname);
      if (rel === '/' || rel === '/viewer.html') {
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end('<!doctype html><meta charset="utf-8"><body style="margin:0"><canvas id="c"></canvas></body>');
        return;
      }
      let file;
      if (extra[rel]) file = extra[rel];
      else if (rel.startsWith('/pdfjs/')) file = path.join(PDFJS, rel.slice(7));
      else file = path.join(root, rel.replace(/^\/+/, ''));
      const body = await readFile(file);
      res.writeHead(200, { 'Content-Type': MIME[path.extname(file).toLowerCase()] ?? 'application/octet-stream' });
      res.end(body);
    } catch { res.writeHead(404).end('not found'); }
  });
  return server;
}

const headerTemplate = `
<div style="width:100%;padding:0 17mm;font-family:'Segoe UI',Tahoma,sans-serif;font-size:7.5pt;
            color:#7b8a91;display:flex;justify-content:space-between;direction:rtl;">
  <span>${title}</span><span style="letter-spacing:.5px;">VITORIZE</span>
</div>`;

const footerTemplate = `
<div style="width:100%;padding:0 17mm;font-family:'Segoe UI',Tahoma,sans-serif;font-size:7.5pt;
            color:#7b8a91;text-align:center;">
  <span class="pageNumber"></span> / <span class="totalPages"></span>
</div>`;

async function renderPdf(browser, htmlName, dest, { withHeader }) {
  const page = await browser.newPage({ viewport: { width: 1240, height: 1754 } });
  const issues = [];
  page.on('pageerror', e => issues.push(`pageerror: ${e.message.slice(0, 140)}`));
  page.on('requestfailed', r => issues.push(`asset failed: ${r.url().split('/').pop()}`));

  await page.goto(`http://127.0.0.1:${port}/${htmlName}`, { waitUntil: 'networkidle', timeout: 180_000 });
  await page.emulateMedia({ media: 'print' });
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(async () => {
    await Promise.all([...document.images]
      .filter(i => !i.complete)
      .map(i => new Promise(r => { i.onload = i.onerror = r; })));
  });
  await page.waitForTimeout(800);

  const broken = await page.evaluate(() =>
    [...document.images].filter(i => !i.complete || i.naturalWidth === 0).map(i => i.getAttribute('src')));

  await page.pdf({
    path: dest,
    format: 'A4',
    printBackground: true,
    preferCSSPageSize: true,
    displayHeaderFooter: withHeader,
    headerTemplate: withHeader ? headerTemplate : '<span></span>',
    footerTemplate: withHeader ? footerTemplate : '<span></span>'
  });
  await page.close();
  return { issues: [...new Set(issues)], broken };
}

/** Extracts per-page text (and optionally rasterises) with pdf.js. */
async function inspect(browser, pdfFile, { raster } = {}) {
  const srv = serve({ '/doc.pdf': pdfFile });
  await new Promise(r => srv.listen(0, '127.0.0.1', r));
  const p = srv.address().port;
  const page = await browser.newPage({ viewport: { width: 1000, height: 1400 } });
  await page.goto(`http://127.0.0.1:${p}/viewer.html`, { waitUntil: 'domcontentloaded' });

  const count = await page.evaluate(async (pp) => {
    const pdfjs = await import(`http://127.0.0.1:${pp}/pdfjs/build/pdf.mjs`);
    pdfjs.GlobalWorkerOptions.workerSrc = `http://127.0.0.1:${pp}/pdfjs/build/pdf.worker.mjs`;
    window.__pdf = await pdfjs.getDocument({ url: `http://127.0.0.1:${pp}/doc.pdf` }).promise;
    return window.__pdf.numPages;
  }, p);

  const pages = [];
  if (raster) await mkdir(raster, { recursive: true });

  for (let i = 1; i <= count; i++) {
    const info = await page.evaluate(async ({ i, raster }) => {
      const pg = await window.__pdf.getPage(i);
      const viewport = pg.getViewport({ scale: raster ? 1.1 : 0.4 });
      const canvas = document.getElementById('c');
      canvas.width = Math.floor(viewport.width);
      canvas.height = Math.floor(viewport.height);
      const ctx = canvas.getContext('2d');
      ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, canvas.width, canvas.height);
      await pg.render({ canvasContext: ctx, viewport }).promise;
      const tc = await pg.getTextContent();
      const text = tc.items.map(x => x.str).join(' ');
      const size = pg.getViewport({ scale: 1 });
      return { text, chars: text.replace(/\s+/g, '').length, w: Math.round(size.width), h: Math.round(size.height) };
    }, { i, raster: !!raster });

    if (raster) {
      const buf = await page.locator('#c').screenshot();
      await writeFile(path.join(raster, `page-${String(i).padStart(3, '0')}.png`), buf);
    }
    pages.push({ i, ...info });
  }

  await page.close(); srv.close();
  return pages;
}

// ---------------------------------------------------------------- build
const server = serve();
await new Promise(r => server.listen(0, '127.0.0.1', r));
const port = server.address().port;

const browser = await chromium.launch({ channel: 'chrome' });
const htmlName = path.basename(input);
const draft = path.join(path.dirname(output), `.draft-${path.basename(output)}`);

// Pass 1 — draft, purely to learn which page each chapter marker lands on.
const pass1 = await renderPdf(browser, htmlName, draft, { withHeader: true });
const draftPages = await inspect(browser, draft);

// pdf.js splits an ASCII run embedded in RTL text into separate items, so the extracted marker can
// come back as "# c1 #". Collapse whitespace before matching.
const markerPage = new Map();
for (const pg of draftPages) {
  const flat = pg.text.replace(/\s+/g, '');
  for (const m of flat.matchAll(/#([a-z0-9][a-z0-9-]*)#/gi)) {
    if (!markerPage.has(m[1])) markerPage.set(m[1], pg.i);
  }
  if (process.env.MARKER_DEBUG) {
    const hits = [...flat.matchAll(/#[^#]{0,20}#/g)].map(x => x[0]);
    if (hits.length) console.log(`  debug p${pg.i}: ${hits.join(' ')}`);
  }
}

// Inject the resolved numbers into the TOC (Persian digits) and rebuild.
const original = await readFile(input, 'utf8');
const FA = '۰۱۲۳۴۵۶۷۸۹';
const fa = n => String(n).replace(/\d/g, d => FA[+d]);
let injected = original.replace(
  /(<a class="pg" href="#([a-z0-9-]+)">)\s*(<\/a>)/gi,
  (all, open, id, close) => markerPage.has(id) ? `${open}${fa(markerPage.get(id))}${close}` : all);

const tmpHtml = path.join(root, `.final-${htmlName}`);
await writeFile(tmpHtml, injected, 'utf8');

// Pass 2 — final document with a populated TOC.
const pass2 = await renderPdf(browser, path.basename(tmpHtml), output, { withHeader: true });
const finalPages = rasterDir
  ? await inspect(browser, output, { raster: rasterDir })
  : await inspect(browser, output);

await browser.close();
server.close();
await rm(tmpHtml, { force: true });
await rm(draft, { force: true });

// ---------------------------------------------------------------- report
const resolved = [...markerPage.entries()];
const tocEntries = [...original.matchAll(/<a class="pg" href="#([a-z0-9-]+)">/gi)].map(m => m[1]);
const unresolved = tocEntries.filter(id => !markerPage.has(id));
const sizes = new Set(finalPages.map(p => `${p.w}x${p.h}`));
const blank = finalPages.filter(p => p.chars < 12).map(p => p.i);

console.log(`pages=${finalPages.length}`);
console.log(`pageSizes=${[...sizes].join(',')}`);
console.log(`tocEntries=${tocEntries.length} resolved=${resolved.length} unresolved=${unresolved.length}${unresolved.length ? ' -> ' + unresolved.join(',') : ''}`);
console.log(`blankish=${blank.length ? blank.join(',') : 'none'}`);
console.log(`brokenImages=${pass2.broken.length ? pass2.broken.join(',') : '0'}`);
const allIssues = [...new Set([...pass1.issues, ...pass2.issues])].filter(x => !x.includes('favicon'));
console.log(`issues=${allIssues.length ? allIssues.join(' | ') : 'none'}`);
if (rasterDir) console.log(`rasters=${rasterDir}`);

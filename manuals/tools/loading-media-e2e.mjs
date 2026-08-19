// Loading-media end-to-end against a running Vitorize instance.
//   default -> upload animated GIF -> real boot overlay uses it -> persistence -> failure case
//   -> remove -> default restored.  Desktop + mobile.  Captures overlay evidence screenshots.
//
// The overlay releases itself the moment the app shell renders, so for capture the release
// script (js/initial-loader.js) is blocked at the network layer. That is TEST-ONLY interception:
// no production timing is altered.
//
// usage: node loading-media-e2e.mjs <origin> <evidenceDir> <gifPath>
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const ORIGIN = process.argv[2] || 'http://localhost:5077';
const OUT = process.argv[3] || 'D:/Vitorize/outputs/ui-ux-evidence';
const GIF_PATH = process.argv[4] || 'D:/Vitorize/manuals/tmp/qa-loading.gif';
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';
const GIF = fs.readFileSync(GIF_PATH);

fs.mkdirSync(OUT, { recursive: true });
let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };

const browser = await chromium.launch({ channel: 'chrome' });

/** Loads a non-prerendered route with the loader-release script blocked and reads the overlay. */
async function readLoader(ctx, { shot = null, full = false } = {}) {
  const p = await ctx.newPage();
  const errs = [], bad = [];
  // The harness itself aborts js/initial-loader.js to hold the overlay open; the resulting
  // ERR_FAILED is a test artefact, not an application error.
  p.on('console', m => {
    const t = m.text();
    if (m.type() === 'error' && !/initial-loader\.js|net::ERR_FAILED/.test(t)) errs.push(t.slice(0, 140));
  });
  p.on('response', r => { if (r.status() >= 400) bad.push(`${r.status()} ${r.url().split('/').pop()}`); });
  await p.route('**/js/initial-loader.js*', r => r.abort());
  await p.goto(`${ORIGIN}/login`, { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(1800);
  const info = await p.evaluate(() => {
    const el = document.getElementById('vz-initial-loader');
    if (!el) return null;
    const cs = getComputedStyle(el);
    const media = el.querySelector('.vz-splash__media');
    const mark = el.querySelector('.vz-splash-mark img');
    const spin = el.querySelector('.vz-spinner');
    const r = el.getBoundingClientRect();
    const mr = media ? media.getBoundingClientRect() : null;
    return {
      usesCustomMedia: !!media,
      customSrc: media ? media.getAttribute('src') : null,
      customLoaded: media ? (media.complete && media.naturalWidth > 0) : null,
      customNatural: media ? `${media.naturalWidth}x${media.naturalHeight}` : null,
      customBox: mr ? { w: Math.round(mr.width), h: Math.round(mr.height) } : null,
      defaultMark: !!mark,
      markLoaded: mark ? (mark.complete && mark.naturalWidth > 0) : null,
      // Visible fallback = the built-in mark is actually on screen (not the hidden sibling).
      fallbackShown: (() => { const fb = document.getElementById('vz-splash-fallback');
        return !!fb && getComputedStyle(fb).display !== 'none'; })(),
      spinnerAnimated: spin ? getComputedStyle(spin).animationName !== 'none' : false,
      position: cs.position, zIndex: cs.zIndex, role: el.getAttribute('role'),
      opaque: cs.backgroundColor !== 'transparent' && cs.backgroundColor !== 'rgba(0, 0, 0, 0)',
      covers: Math.round(r.width) >= document.documentElement.clientWidth &&
              Math.round(r.height) >= document.documentElement.clientHeight,
      centred: Math.abs(((r.left + r.right) / 2) - (document.documentElement.clientWidth / 2)) < 3,
      hOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      srText: (el.textContent || '').trim()
    };
  });
  if (shot) await p.screenshot({ path: `${OUT}/${shot}`, fullPage: full });
  await p.close();
  return { info, errs, bad };
}

/** Verifies the overlay releases and the app becomes interactive (script NOT blocked). */
async function verifyRelease(ctx) {
  const p = await ctx.newPage();
  await p.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  const gone = await p.locator('#vz-initial-loader').count() === 0;
  let interactive = false;
  try { await p.locator('#pw-mobile').fill('09120000013'); interactive = (await p.locator('#pw-mobile').inputValue()) === '09120000013'; } catch {}
  await p.close();
  return { gone, interactive };
}

async function adminLogin(page) {
  await page.goto(`${ORIGIN}/admin/login`, { waitUntil: 'networkidle' });
  await page.locator('input[name="mobile"]').fill('09120000011');
  await page.locator('input[name="password"]').fill(PW);
  await Promise.all([
    page.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login'), { timeout: 45000 }),
    page.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
  ]);
}
async function openLoadingField(page) {
  await page.goto(`${ORIGIN}/admin/settings`, { waitUntil: 'networkidle' });
  await page.locator('.vz-settab', { hasText: 'لوگو و تصاویر' }).first().click();
  const f = page.getByTestId('setting-LoadingMediaPath');
  await f.scrollIntoViewIfNeeded();
  await f.waitFor({ state: 'visible', timeout: 20000 });
  return f;
}
async function clearMedia(page) {
  const f = await openLoadingField(page);
  const rm = f.locator('.vz-upload__preview button');
  if (await rm.count()) { await rm.click(); await f.locator('input[type=file]').waitFor({ state: 'attached', timeout: 30000 }); }
  return f;
}

const admin = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: 'light', locale: 'fa-IR' });
const adminPage = await admin.newPage();
await adminLogin(adminPage);

// ============ 0. record + clear starting state ============
console.log('\n=== 0. record and clear existing loading media ===');
let f = await openLoadingField(adminPage);
const hadMedia = await f.locator('.vz-upload__preview img').count() > 0;
console.log(`  starting state: ${hadMedia ? 'custom media configured' : 'empty (default loader)'}`);
await clearMedia(adminPage);
ok('cleared to empty for a clean baseline');

// ============ 1. DEFAULT loader, desktop + mobile ============
for (const vp of [{ n: 'desktop', width: 1440, height: 900 }, { n: 'mobile', width: 390, height: 844 }]) {
  console.log(`\n=== 1. default loader — ${vp.n} ===`);
  const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, colorScheme: 'light', locale: 'fa-IR' });
  const { info, errs, bad } = await readLoader(ctx, { shot: `default-loading-media-${vp.n}.png` });
  if (!info) { fail(`${vp.n}: no overlay rendered`); await ctx.close(); continue; }
  !info.usesCustomMedia ? ok(`${vp.n}: no custom media`) : fail(`${vp.n}: unexpected custom media`);
  info.defaultMark && info.markLoaded ? ok(`${vp.n}: default Vitorize mark visible`) : fail(`${vp.n}: default mark missing/broken`);
  info.spinnerAnimated ? ok(`${vp.n}: ring animating`) : fail(`${vp.n}: ring not animating`);
  info.covers && info.opaque ? ok(`${vp.n}: covers viewport, opaque`) : fail(`${vp.n}: overlay geometry wrong`);
  info.role === 'status' && info.srText.includes('در حال بارگذاری') ? ok(`${vp.n}: role=status + status text`) : fail(`${vp.n}: accessibility text missing`);
  info.hOverflow <= 1 ? ok(`${vp.n}: no horizontal overflow`) : fail(`${vp.n}: hOverflow=${info.hOverflow}`);
  errs.length === 0 ? ok(`${vp.n}: 0 console errors`) : fail(`${vp.n}: console ${errs.slice(0,2).join(' | ')}`);
  bad.length === 0 ? ok(`${vp.n}: 0 failed responses`) : fail(`${vp.n}: ${bad.slice(0,2).join(' | ')}`);
  await ctx.close();
}

// ============ 2. upload the animated GIF via the real Admin UI ============
console.log('\n=== 2. upload animated GIF through Admin Settings ===');
f = await openLoadingField(adminPage);
await f.locator('input[type=file]').setInputFiles({ name: 'qa-loading.gif', mimeType: 'image/gif', buffer: GIF });
const preview = f.locator('.vz-upload__preview img');
await preview.waitFor({ state: 'visible', timeout: 60000 });
const savedSrc = await preview.getAttribute('src');
/\/uploads\/settings\/[a-f0-9]{32}\.gif/.test(savedSrc || '') ? ok(`upload accepted as .gif: ${savedSrc.replace(/[a-f0-9]{32}/, '<hash>')}`) : fail(`unexpected src ${savedSrc}`);
const previewOk = await preview.evaluate(i => i.complete && i.naturalWidth > 0);
previewOk ? ok('admin preview renders (not broken)') : fail('admin preview broken');
await adminPage.screenshot({ path: `${OUT}/admin-loading-media-configured.png` });

// persistence after a full refresh
const f2 = await openLoadingField(adminPage);
const persisted = await f2.locator('.vz-upload__preview img').getAttribute('src');
persisted === savedSrc ? ok('setting persisted after refresh') : fail(`not persisted (${persisted})`);

// the media URL itself must resolve
const head = await adminPage.request.get(savedSrc);
head.ok() ? ok(`media URL resolves (${head.status()})`) : fail(`media URL ${head.status()}`);

// ============ 3. CUSTOM loader in the real overlay, desktop + mobile ============
for (const vp of [{ n: 'desktop', width: 1440, height: 900 }, { n: 'mobile', width: 390, height: 844 }]) {
  console.log(`\n=== 3. custom loading media — ${vp.n} ===`);
  const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, colorScheme: 'light', locale: 'fa-IR' });
  const { info, errs, bad } = await readLoader(ctx, { shot: `custom-loading-media-${vp.n}.png` });
  if (!info) { fail(`${vp.n}: no overlay`); await ctx.close(); continue; }
  info.usesCustomMedia ? ok(`${vp.n}: usesCustomMedia = true`) : fail(`${vp.n}: custom media NOT used`);
  info.customLoaded ? ok(`${vp.n}: GIF decoded, natural ${info.customNatural}`) : fail(`${vp.n}: GIF broken`);
  // The fallback mark is present but hidden while the custom medium loads successfully.
  !info.fallbackShown ? ok(`${vp.n}: default mark hidden, custom medium is what shows`) : fail(`${vp.n}: default mark visible alongside custom media`);
  info.covers && info.opaque ? ok(`${vp.n}: overlay covers viewport`) : fail(`${vp.n}: overlay geometry wrong`);
  info.centred ? ok(`${vp.n}: overlay centred`) : fail(`${vp.n}: overlay not centred`);
  // Product 1:1 rule must NOT have squashed the loader, and aspect must be preserved.
  const nat = (info.customNatural || '0x0').split('x').map(Number);
  const box = info.customBox || { w: 0, h: 0 };
  const natRatio = nat[0] / nat[1], boxRatio = box.w / box.h;
  Math.abs(natRatio - boxRatio) < 0.05 ? ok(`${vp.n}: aspect preserved (natural ${info.customNatural} -> ${box.w}x${box.h})`) : fail(`${vp.n}: distorted ${box.w}x${box.h} from ${info.customNatural}`);
  box.w <= 240 && box.h <= 240 ? ok(`${vp.n}: within max-size rules (${box.w}x${box.h})`) : fail(`${vp.n}: exceeds max size ${box.w}x${box.h}`);
  info.hOverflow <= 1 ? ok(`${vp.n}: no horizontal overflow`) : fail(`${vp.n}: hOverflow=${info.hOverflow}`);
  info.role === 'status' ? ok(`${vp.n}: status role retained`) : fail(`${vp.n}: status role lost`);
  errs.length === 0 ? ok(`${vp.n}: 0 console errors`) : fail(`${vp.n}: ${errs.slice(0,2).join(' | ')}`);
  bad.length === 0 ? ok(`${vp.n}: 0 failed responses`) : fail(`${vp.n}: ${bad.slice(0,2).join(' | ')}`);
  const rel = await verifyRelease(ctx);
  rel.gone ? ok(`${vp.n}: overlay releases normally`) : fail(`${vp.n}: overlay stuck`);
  rel.interactive ? ok(`${vp.n}: app interactive after release`) : fail(`${vp.n}: app not interactive`);
  await ctx.close();
}

// ============ 4. failure case: configured file removed from disk ============
console.log('\n=== 4. configured media unavailable ===');
const fileName = (savedSrc.match(/([a-f0-9]{32}\.gif)/) || [])[1];
const physical = `D:/Vitorize/Vitorize/Vitorize.Api/App_Data/PublicMedia/settings/${fileName}`;
let moved = null;
if (fs.existsSync(physical)) {
  moved = `${physical}.bak`;
  fs.renameSync(physical, moved);
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: 'light' });
  const { info } = await readLoader(ctx, { shot: 'loading-media-missing-file.png' });
  if (info) {
    console.log(`  observed: usesCustomMedia=${info.usesCustomMedia} loaded=${info.customLoaded} defaultMark=${info.defaultMark} fallbackShown=${info.fallbackShown}`);
    if (info.usesCustomMedia && info.customLoaded === false) {
      fail('missing media leaves a broken <img> with no fallback (user sees broken icon)');
    } else if (info.defaultMark && info.markLoaded && info.fallbackShown) {
      ok('missing media falls back to the built-in Vitorize mark');
    } else fail(`unexpected failure-case state: ${JSON.stringify(info)}`);
    info.covers ? ok('overlay still renders (not blank/stuck)') : fail('overlay collapsed');
  } else fail('no overlay in failure case');
  await ctx.close();
  fs.renameSync(moved, physical);
  ok('physical file restored');
} else console.log(`  skip - physical file not found at expected path`);

// ============ 5. remove -> default restored ============
console.log('\n=== 5. remove custom media -> default fallback ===');
await clearMedia(adminPage);
const f3 = await openLoadingField(adminPage);
(await f3.locator('.vz-upload__preview img').count()) === 0 ? ok('setting empty after refresh') : fail('setting still populated after refresh');
for (const vp of [{ n: 'desktop', width: 1440, height: 900 }, { n: 'mobile', width: 390, height: 844 }]) {
  const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, colorScheme: 'light' });
  const { info } = await readLoader(ctx, { shot: `restored-loading-media-${vp.n}.png` });
  if (!info) { fail(`${vp.n}: no overlay after removal`); await ctx.close(); continue; }
  !info.usesCustomMedia ? ok(`${vp.n}: custom media cleared (no stale cache)`) : fail(`${vp.n}: stale custom media still shown`);
  info.defaultMark && info.markLoaded ? ok(`${vp.n}: default Vitorize loader restored`) : fail(`${vp.n}: default not restored`);
  info.spinnerAnimated ? ok(`${vp.n}: ring animating`) : fail(`${vp.n}: ring not animating`);
  await ctx.close();
}

await admin.close();
await browser.close();
console.log(`\nTOTAL FAILURES = ${fails}`);
console.log(`evidence: ${OUT}`);
process.exit(fails ? 1 : 0);

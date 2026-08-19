// Published-package check on a FRESH bootstrap DB (no users): default loader + product 1:1.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
import fs from 'node:fs';
const ORIGIN = process.argv[2], OUT = process.argv[3];
fs.mkdirSync(OUT, { recursive: true });
let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };
const browser = await chromium.launch({ channel: 'chrome' });

for (const vp of [{ n: 'desktop', width: 1440, height: 900 }, { n: 'mobile', width: 390, height: 844 }]) {
  console.log(`\n=== published ${vp.n} ===`);
  const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, colorScheme: 'light', locale: 'fa-IR' });

  // default boot loader (fresh install => LoadingMediaPath empty)
  const lp = await ctx.newPage();
  await lp.route('**/js/initial-loader.js*', r => r.abort());
  await lp.goto(`${ORIGIN}/login`, { waitUntil: 'domcontentloaded' });
  await lp.waitForTimeout(1500);
  const L = await lp.evaluate(() => {
    const el = document.getElementById('vz-initial-loader'); if (!el) return null;
    const cs = getComputedStyle(el), mark = el.querySelector('.vz-splash-mark img'), sp = el.querySelector('.vz-spinner');
    return { custom: !!el.querySelector('.vz-splash__media'), mark: !!mark,
             markLoaded: mark ? (mark.complete && mark.naturalWidth > 0) : false,
             spin: sp ? getComputedStyle(sp).animationName !== 'none' : false,
             fixed: cs.position === 'fixed', role: el.getAttribute('role') };
  });
  await lp.screenshot({ path: `${OUT}/published-default-loader-${vp.n}.png` });
  await lp.close();
  if (!L) fail(`${vp.n}: no boot loader`);
  else {
    !L.custom && L.mark && L.markLoaded ? ok(`${vp.n}: fresh install shows default Vitorize loader`) : fail(`${vp.n}: default loader wrong (${JSON.stringify(L)})`);
    L.spin ? ok(`${vp.n}: ring animating`) : fail(`${vp.n}: ring static`);
    L.fixed && L.role === 'status' ? ok(`${vp.n}: overlay fixed + role=status`) : fail(`${vp.n}: overlay/role wrong`);
  }

  // public routes: no broken images, no console errors, no overflow
  for (const route of ['/', '/shop', '/login', '/faq', '/cart']) {
    const p = await ctx.newPage();
    const errs = [], bad = [];
    p.on('console', m => { if (m.type() === 'error') errs.push(m.text().slice(0, 100)); });
    p.on('response', r => { if (r.status() >= 400) bad.push(`${r.status()} ${r.url().split('/').pop()}`); });
    await p.goto(`${ORIGIN}${route}`, { waitUntil: 'networkidle' });
    await p.waitForTimeout(400);
    const d = await p.evaluate(() => {
      const broken = [...document.images].filter(i => !(i.complete && i.naturalWidth > 0)).map(i => i.getAttribute('src'));
      const media = [...document.querySelectorAll('.st-pcard__media, .st-gal__main, .st-cart-item__image')]
        .map(e => { const r = e.getBoundingClientRect(); return { w: Math.round(r.width), h: Math.round(r.height) }; })
        .filter(m => m.w > 0);
      return { broken, media, ov: document.documentElement.scrollWidth - document.documentElement.clientWidth };
    });
    const nonSquare = d.media.filter(m => Math.abs(m.w - m.h) > 2);
    const issues = [];
    if (d.broken.length) issues.push(`broken:${d.broken.length}`);
    if (d.ov > 1) issues.push(`overflow:${d.ov}`);
    if (nonSquare.length) issues.push(`nonSquareProductMedia:${JSON.stringify(nonSquare[0])}`);
    if (errs.length) issues.push(`console:${errs[0]}`);
    if (bad.length) issues.push(`http:${bad[0]}`);
    issues.length === 0
      ? ok(`${vp.n} ${route.padEnd(7)} clean (${d.media.length} product media, all square)`)
      : fail(`${vp.n} ${route} -> ${issues.join(' | ')}`);
    await p.close();
  }
  await ctx.close();
}
await browser.close();
console.log(`\nTOTAL FAILURES = ${fails}`);
process.exit(fails ? 1 : 0);

// Mobile smoke + FIX-17 default-loader verification against a running published build.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const origin = process.argv[2];
const browser = await chromium.launch({ channel: 'chrome' });
let broken = 0, failed = 0, errors = 0;

// ---- mobile 390x844 ----
for (const route of ['/', '/shop', '/login', '/faq']) {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, colorScheme: 'light', locale: 'fa-IR' });
  const page = await ctx.newPage();
  const f = [], e = [];
  page.on('response', r => { if (r.status() >= 400) f.push(`${r.status()} ${r.url()}`); });
  page.on('console', m => { if (m.type() === 'error') e.push(m.text().slice(0, 120)); });
  await page.goto(`${origin}${route}`, { waitUntil: 'networkidle', timeout: 90_000 });
  await page.waitForTimeout(900);
  const imgs = await page.evaluate(() => [...document.images].map(i => ({ ok: i.complete && i.naturalWidth > 0, src: i.getAttribute('src') })));
  const b = imgs.filter(i => !i.ok);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  console.log(`[mobile] ${route.padEnd(7)} images=${imgs.length} broken=${b.length} failed=${f.length} console=${e.length} hOverflow=${overflow}`);
  b.forEach(x => console.log(`    BROKEN ${x.src}`));
  broken += b.length; failed += f.length; errors += e.length;
  await ctx.close();
}

// ---- FIX-17 default loader (block the release script so the overlay stays visible) ----
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: 'light' });
const page = await ctx.newPage();
await page.route('**/js/initial-loader.js*', r => r.abort());
await page.goto(`${origin}/login`, { waitUntil: 'domcontentloaded', timeout: 90_000 });
await page.waitForTimeout(1200);
const loader = await page.evaluate(() => {
  const el = document.getElementById('vz-initial-loader');
  if (!el) return null;
  const cs = getComputedStyle(el);
  const media = el.querySelector('.vz-splash__media');
  const markImg = el.querySelector('.vz-splash-mark img');
  const spinner = el.querySelector('.vz-spinner');
  return {
    position: cs.position, zIndex: cs.zIndex,
    opaque: cs.backgroundColor !== 'transparent' && cs.backgroundColor !== 'rgba(0, 0, 0, 0)',
    usesCustomMedia: !!media,
    defaultMark: !!markImg,
    markLoaded: markImg ? (markImg.complete && markImg.naturalWidth > 0) : false,
    markNatural: markImg ? `${markImg.naturalWidth}x${markImg.naturalHeight}` : 'n/a',
    spinnerAnimated: spinner ? getComputedStyle(spinner).animationName !== 'none' : false,
    role: el.getAttribute('role')
  };
});
console.log('[loader]', JSON.stringify(loader));
await ctx.close();

await browser.close();
console.log(`TOTAL mobileBroken=${broken} mobileFailed=${failed} mobileConsole=${errors}`);

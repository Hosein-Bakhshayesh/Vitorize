// Deployment asset check: loads representative pages from a running site and reports every
// <img> that failed to decode, plus failed network responses and console errors.
//
// Usage: node asset-check.mjs <origin> [routes...]

import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const origin = process.argv[2] ?? 'http://127.0.0.1:5390';
const routes = process.argv.slice(3).length ? process.argv.slice(3) : ['/', '/shop', '/login', '/faq'];

const browser = await chromium.launch({ channel: 'chrome' });
let totalBroken = 0, totalFailed = 0, totalConsole = 0;

for (const route of routes) {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 }, colorScheme: 'light', locale: 'fa-IR'
  });
  const page = await context.newPage();
  const failed = [];
  const consoleErrors = [];
  page.on('response', r => { if (r.status() >= 400) failed.push(`${r.status()} ${r.url()}`); });
  page.on('requestfailed', r => failed.push(`FAILED ${r.url()}`));
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 160)); });

  await page.goto(`${origin}${route}`, { waitUntil: 'networkidle', timeout: 90_000 }).catch(e => {
    console.log(`  ${route}: NAVIGATION FAILED - ${e.message.slice(0, 120)}`);
  });
  await page.waitForTimeout(1200);

  const imgs = await page.evaluate(() => [...document.images].map(i => ({
    src: i.currentSrc || i.getAttribute('src') || '(none)',
    ok: i.complete && i.naturalWidth > 0,
    w: i.naturalWidth, h: i.naturalHeight,
    cls: (i.className || '').slice(0, 40),
    visible: !!(i.offsetWidth || i.offsetHeight || i.getClientRects().length)
  })));

  const broken = imgs.filter(i => !i.ok);
  const logo = imgs.find(i => i.cls.includes('st-logo__img'));

  console.log(`\n=== ${route} ===`);
  console.log(`  images=${imgs.length}  broken=${broken.length}  failedResponses=${failed.length}  consoleErrors=${consoleErrors.length}`);
  if (logo) console.log(`  header logo: src=${logo.src.replace(origin, '')} ok=${logo.ok} natural=${logo.w}x${logo.h} visible=${logo.visible}`);
  else console.log('  header logo: NOT FOUND in DOM');
  broken.forEach(b => console.log(`    BROKEN img: ${b.src.replace(origin, '')}  (class=${b.cls})`));
  [...new Set(failed)].slice(0, 12).forEach(f => console.log(`    ${f.replace(origin, '')}`));
  [...new Set(consoleErrors)].slice(0, 6).forEach(c => console.log(`    console: ${c}`));

  totalBroken += broken.length; totalFailed += failed.length; totalConsole += consoleErrors.length;
  await context.close();
}

await browser.close();
console.log(`\nTOTAL brokenImages=${totalBroken} failedResponses=${totalFailed} consoleErrors=${totalConsole}`);

// Reports the DOM context of every failed <img> so the owning component can be identified.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const origin = process.argv[2];
const route = process.argv[3] ?? '/';
const browser = await chromium.launch({ channel: 'chrome' });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, colorScheme: 'light' });
await page.goto(`${origin}${route}`, { waitUntil: 'networkidle', timeout: 90_000 });
await page.waitForTimeout(1500);

const info = await page.evaluate(() => [...document.images].map(i => ({
  src: (i.getAttribute('src') || '').slice(-70),
  ok: i.complete && i.naturalWidth > 0,
  hasOnError: i.hasAttribute('onerror'),
  cls: i.className,
  parentCls: i.parentElement?.className ?? '',
  grandCls: i.parentElement?.parentElement?.className ?? '',
  nextSibling: i.nextElementSibling ? `${i.nextElementSibling.tagName}.${i.nextElementSibling.className}` : '(none)',
  displayed: getComputedStyle(i).display,
  box: `${i.offsetWidth}x${i.offsetHeight}`
})));

info.forEach((x, n) => {
  console.log(`[${n}] ok=${x.ok} onerror=${x.hasOnError} display=${x.displayed} box=${x.box}`);
  console.log(`     src        ...${x.src}`);
  console.log(`     class      ${x.cls}`);
  console.log(`     parent     ${x.parentCls}`);
  console.log(`     grandparent${x.grandCls}`);
  console.log(`     next       ${x.nextSibling}`);
});
await browser.close();

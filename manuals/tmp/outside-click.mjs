import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O = 'http://localhost:5077', PW = 'E2E-Admin-Only-aA1!';
const b = await chromium.launch({ channel: 'chrome' });
const c = await b.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
const p = await c.newPage();
await p.goto(`${O}/login`, { waitUntil: 'networkidle' });
await p.locator('#pw-mobile').fill('09120000013'); await p.locator('#pw-pass').fill(PW);
await Promise.all([p.waitForURL(u => !u.pathname.startsWith('/login')), p.locator('form[action="/auth/customer/login"] button[type=submit]').click()]);
await p.goto(`${O}/product/e2e-seo-product`, { waitUntil: 'networkidle' });

await p.locator('.st-avatar').click();
await p.waitForTimeout(500);
console.log('opened  :', await p.locator('.st-dropdown').count());

// what is actually under the point we click?
const at = await p.evaluate(() => {
  const el = document.elementFromPoint(5, 500);
  return el ? `${el.tagName.toLowerCase()} class="${(el.className||'').toString().slice(0,60)}" z=${getComputedStyle(el).zIndex} pos=${getComputedStyle(el).position}` : 'null';
});
console.log('at(5,500):', at);

await p.mouse.click(5, 500);
for (const w of [300, 800, 2000, 4000]) {
  await p.waitForTimeout(w === 300 ? 300 : w - 300);
  console.log(`after ~${w}ms:`, await p.locator('.st-dropdown').count());
}
await b.close();

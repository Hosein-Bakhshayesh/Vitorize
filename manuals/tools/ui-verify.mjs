// UI hardening verification: floating-panel bounds + product-image 1:1 + non-product regression.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = 'http://localhost:5077';
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';
let fails = 0;
const fail = m => { console.log(`  FAIL ${m}`); fails++; };
const ok = m => console.log(`  ok   ${m}`);

const browser = await chromium.launch({ channel: 'chrome' });

async function login(page) {
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill(PW);
  await Promise.all([
    page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 }),
    page.locator('form[action="/auth/customer/login"] button[type=submit]').click()
  ]);
}

const squareOf = async (page, sel) => page.evaluate(s => {
  const el = document.querySelector(s);
  if (!el) return null;
  const r = el.getBoundingClientRect();
  return { w: Math.round(r.width), h: Math.round(r.height), d: Math.abs(r.width - r.height) };
}, sel);

for (const vp of [{ n: 'desktop', width: 1440, height: 900 }, { n: 'mobile', width: 390, height: 844 }]) {
  console.log(`\n===== ${vp.n} ${vp.width}x${vp.height} =====`);
  const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, colorScheme: 'light', locale: 'fa-IR' });
  const page = await ctx.newPage();
  await login(page);

  // ---------- Defect 1: account dropdown must stay fully on screen ----------
  await page.goto(`${ORIGIN}/product/e2e-seo-product`, { waitUntil: 'networkidle' });
  const avatar = page.locator('.st-avatar');
  if (await avatar.count()) {
    await avatar.click();
    await page.waitForTimeout(400);
    const m = await page.evaluate(() => {
      const el = document.querySelector('.st-dropdown');
      if (!el) return null;
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      return { x: Math.round(r.x), y: Math.round(r.y), right: Math.round(r.right), bottom: Math.round(r.bottom),
               w: Math.round(r.width), h: Math.round(r.height), overflowY: cs.overflowY,
               vw: document.documentElement.clientWidth, vh: document.documentElement.clientHeight };
    });
    if (!m) fail('account dropdown did not open');
    else {
      m.x >= 0 ? ok(`dropdown left edge ${m.x} >= 0`) : fail(`dropdown escapes left (x=${m.x})`);
      m.right <= m.vw ? ok(`dropdown right ${m.right} <= ${m.vw}`) : fail(`dropdown escapes right (${m.right} > ${m.vw})`);
      m.y >= 0 ? ok(`dropdown top ${m.y} >= 0`) : fail(`dropdown above viewport (y=${m.y})`);
      (m.bottom <= m.vh || m.overflowY === 'auto') ? ok(`dropdown bottom ${m.bottom} within ${m.vh} or scrolls`) : fail(`dropdown bottom ${m.bottom} > ${m.vh} and does not scroll`);
      // last item must be reachable
      const last = await page.evaluate(() => {
        const b = document.querySelector('.st-dropdown .danger');
        if (!b) return null; const r = b.getBoundingClientRect();
        return { x: Math.round(r.x), right: Math.round(r.right), vw: document.documentElement.clientWidth };
      });
      if (last) (last.x >= 0 && last.right <= last.vw) ? ok('logout control fully on screen') : fail(`logout control off screen (x=${last.x}, right=${last.right})`);
    }
    await page.keyboard.press('Escape').catch(() => {});
    await page.mouse.click(5, 400);
  } else fail('no .st-avatar (not authenticated)');

  // ---------- Defect 2: product media 1:1 ----------
  const productChecks = [
    ['/', '.st-pcard__media', 'home product card'],
    ['/shop', '.st-pcard__media', 'shop product card'],
    ['/product/e2e-seo-product', '.st-gal__main', 'product detail main'],
    ['/product/e2e-seo-product', '.st-pcard__media', 'related product card'],
    ['/customer/wishlist', '.st-pcard__media', 'wishlist product card'],
    ['/cart', '.st-cart-item__image', 'cart product thumb']
  ];
  for (const [route, sel, label] of productChecks) {
    await page.goto(`${ORIGIN}${route}`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(300);
    const s = await squareOf(page, sel);
    if (!s) { console.log(`  skip ${label} (not present on ${route})`); continue; }
    s.d <= 2 ? ok(`${label} ${s.w}x${s.h} square`) : fail(`${label} ${s.w}x${s.h} NOT square (Δ${s.d}) on ${route}`);
  }

  // ---------- non-product regression: these must NOT be square ----------
  await page.goto(`${ORIGIN}/`, { waitUntil: 'networkidle' });
  const logo = await page.evaluate(() => {
    const i = document.querySelector('.st-logo__img, header img');
    if (!i) return null; const r = i.getBoundingClientRect();
    return { w: Math.round(r.width), h: Math.round(r.height) };
  });
  if (logo) (logo.w !== logo.h || logo.w === 0) ? ok(`logo ${logo.w}x${logo.h} unchanged (not forced square)`) : console.log(`  note logo is ${logo.w}x${logo.h} (naturally square asset)`);

  await ctx.close();
}

await browser.close();
console.log(`\nTOTAL FAILURES = ${fails}`);
process.exit(fails ? 1 : 0);

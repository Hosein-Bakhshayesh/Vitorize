// Published-binaries smoke: drives the ACTUAL published Api/Web (not the dev stack) through the
// defects fixed this pass, plus inventory and blog, on desktop and mobile in light and dark.
// usage: node published-smoke.mjs <origin>
//
// The API throttles sign-in after five attempts (429), which is a deliberate brute-force control.
// This script therefore signs in twice in total and switches viewport/theme on the same session
// rather than opening a fresh logged-in context per combination.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = process.argv[2] || 'http://127.0.0.1:5399';
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';

let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };

const browser = await chromium.launch({ channel: 'chrome' });

async function loginCustomer(page, mobile) {
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(PW);
  await Promise.all([
    page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 45000 }),
    page.locator('form[action="/auth/customer/login"] button[type=submit]').click()
  ]);
}

async function clearCart(page) {
  await page.goto(`${ORIGIN}/cart`, { waitUntil: 'networkidle' });
  const clear = page.locator('button', { hasText: 'خالی کردن سبد خرید' });
  if (await clear.count()) { await clear.first().click(); await page.waitForTimeout(1200); }
}

async function addToCart(page, slug) {
  await page.goto(`${ORIGIN}/product/${slug}`, { waitUntil: 'networkidle' });
  const buy = page.locator('.st-buy__card button.st-btn--accent');
  const wasEnabled = !(await buy.isDisabled());
  if (wasEnabled) {
    await buy.click();
    const dialog = page.locator('.vz-dialog');
    if (await dialog.count() && await dialog.first().isVisible()) {
      for (const input of await page.locator('.vz-dialog input.st-input, .vz-dialog input[type=text]').all()) {
        await input.fill('published-smoke');
      }
      await page.locator('.vz-dialog button.st-btn--accent').click();
    }
    await page.waitForTimeout(1500);
  }
  return wasEnabled;
}

// ---- One customer session: inventory + KYC CTA across viewport and theme ----------------------
{
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
  const page = await context.newPage();
  try {
    await loginCustomer(page, '09120000013');

    await clearCart(page);
    const purchasable = await addToCart(page, 'e2e-staged-cart-product');
    purchasable ? ok('managed-stock product with stock is purchasable')
                : fail('managed-stock product with stock was not purchasable');
    await page.goto(`${ORIGIN}/cart`, { waitUntil: 'networkidle' });
    (await page.locator('body').innerText()).includes('پیش‌فرض')
      ? fail('implicit default SKU leaked into the cart')
      : ok('implicit default SKU is hidden from the cart');
    await clearCart(page);

    await addToCart(page, 'e2e-fix09-always');

    for (const [label, viewport, scheme] of [
      ['desktop light', { width: 1440, height: 900 }, 'light'],
      ['desktop dark', { width: 1440, height: 900 }, 'dark'],
      ['mobile light', { width: 390, height: 844 }, 'light'],
      ['mobile dark', { width: 390, height: 844 }, 'dark']
    ]) {
      await page.setViewportSize(viewport);
      await page.emulateMedia({ colorScheme: scheme });
      await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });

      const panel = page.locator('[data-testid=checkout-kyc-information]');
      if (!await panel.count()) { fail(`KYC ${label}: panel missing`); continue; }
      if (!await panel.locator('[data-testid=checkout-kyc-state]').count()) {
        fail(`KYC ${label}: no verification state shown`); continue;
      }
      const link = panel.locator('a[href="/customer/verification"]').first();
      if (!await link.count()) { fail(`KYC ${label}: no action link`); continue; }
      if (!await link.isVisible()) { fail(`KYC ${label}: action link hidden`); continue; }

      // Truly hittable: its own centre point is what the browser would click, so a CSS rule or the
      // mobile bottom navigation covering it would be caught here.
      await link.scrollIntoViewIfNeeded();
      const hittable = await link.evaluate(el => {
        const r = el.getBoundingClientRect();
        const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
        return !!hit && (el.contains(hit) || hit.contains(el));
      });
      if (!hittable) { fail(`KYC ${label}: action link is covered`); continue; }

      await link.click();
      await page.waitForURL(/\/customer\/verification/, { timeout: 30000 });
      ok(`KYC ${label}: explanation + state + reachable CTA -> verification`);
    }
    await clearCart(page);
  } catch (e) {
    fail(`customer session: ${String(e).split('\n')[0].slice(0, 140)}`);
  } finally { await context.close(); }
}

// ---- Verified customer must not be nagged -----------------------------------------------------
{
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
  const page = await context.newPage();
  try {
    await loginCustomer(page, '09120000014');
    await clearCart(page);
    await addToCart(page, 'e2e-fix09-always');
    await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });
    (await page.locator('[data-testid=checkout-kyc-information]').count()) === 0
      ? ok('verified customer sees no verification prompt')
      : fail('verified customer is still told to verify');
    await clearCart(page);
  } catch (e) { fail(`verified customer: ${String(e).split('\n')[0].slice(0, 140)}`); }
  finally { await context.close(); }
}

// ---- Anonymous public routes, mobile dark -----------------------------------------------------
{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, colorScheme: 'dark', locale: 'fa-IR' });
  const page = await context.newPage();
  try {
    for (const route of ['/', '/shop', '/blog', '/faq']) {
      const r = await page.goto(`${ORIGIN}${route}`, { waitUntil: 'networkidle' });
      r && r.status() === 200 ? ok(`${route} renders (mobile dark)`) : fail(`${route} -> ${r && r.status()}`);
    }
  } catch (e) { fail(`public routes: ${String(e).split('\n')[0].slice(0, 140)}`); }
  finally { await context.close(); }
}

await browser.close();
console.log(`\npublished smoke: ${fails === 0 ? 'PASS' : `FAIL (${fails})`}`);
process.exit(fails ? 1 : 0);

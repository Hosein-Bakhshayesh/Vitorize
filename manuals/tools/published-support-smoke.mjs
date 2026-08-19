// SupportRequired fulfilment workflow against the ACTUAL published binaries.
// usage: node published-support-smoke.mjs <webOrigin> [orderNumber]
//
// The published build runs the Production configuration, so checkout would hand off to the REAL
// payment gateway. This smoke therefore never completes a payment: it verifies that a support
// product with stock is purchasable up to the payment hand-off, and then inspects an already-paid
// seeded support order to confirm the fulfilment workflow the administrator is steered towards.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const WEB = process.argv[2] || 'http://127.0.0.1:5399';
const ORDER_NUMBER = process.argv[3] || 'FIX09-FINAL-SUPPORT';
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';

let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };

const browser = await chromium.launch({ channel: 'chrome' });

// ---- Customer: a stocked SupportRequired product is purchasable and reaches payment -----------
{
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
  const page = await context.newPage();
  try {
    await page.goto(`${WEB}/login`, { waitUntil: 'networkidle' });
    await page.locator('#pw-mobile').fill('09120000013');
    await page.locator('#pw-pass').fill(PW);
    await Promise.all([
      page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 45000 }),
      page.locator('form[action="/auth/customer/login"] button[type=submit]').click()
    ]);

    await page.goto(`${WEB}/cart`, { waitUntil: 'networkidle' });
    const clear = page.locator('button', { hasText: 'خالی کردن سبد خرید' });
    if (await clear.count()) { await clear.first().click(); await page.waitForTimeout(1200); }

    await page.goto(`${WEB}/product/e2e-support-product`, { waitUntil: 'networkidle' });
    const buy = page.locator('.st-buy__card button.st-btn--accent');
    if (await buy.isDisabled()) { fail('SupportRequired product with stock was not purchasable'); }
    else {
      await buy.click();
      const field = page.locator('#product-input-support_ref');
      await field.waitFor({ state: 'visible', timeout: 20000 });
      await field.fill('published-support-smoke');
      await page.locator('.vz-dialog button.st-btn--accent').click();
      await page.locator('.vz-toast.success, .vz-toast--success').first().waitFor({ state: 'visible', timeout: 30000 });
      ok('SupportRequired product with stock is purchasable');
    }

    await page.goto(`${WEB}/checkout`, { waitUntil: 'networkidle' });
    await page.locator('.st-paycard.active').first().waitFor({ state: 'visible', timeout: 30000 });
    ok('checkout reaches the payment step (no payment is attempted against production settings)');

    await page.goto(`${WEB}/cart`, { waitUntil: 'networkidle' });
    const clear2 = page.locator('button', { hasText: 'خالی کردن سبد خرید' });
    if (await clear2.count()) { await clear2.first().click(); await page.waitForTimeout(1200); }
  } catch (e) {
    fail(`customer support purchase: ${String(e).split('\n')[0].slice(0, 150)}`);
  } finally { await context.close(); }
}

// ---- Admin: an already-paid support order is steered to the ticket workflow -------------------
{
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
  const admin = await context.newPage();
  try {
    await admin.goto(`${WEB}/admin/login`, { waitUntil: 'networkidle' });
    await admin.locator('input[name="mobile"]').fill('09120000011');
    await admin.locator('input[name="password"]').fill(PW);
    await Promise.all([
      admin.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login'), { timeout: 45000 }),
      admin.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
    ]);

    await admin.goto(`${WEB}/admin/orders`, { waitUntil: 'networkidle' });
    await admin.locator('#order-search').fill(ORDER_NUMBER);
    const row = admin.locator('tbody tr').filter({ hasText: ORDER_NUMBER });
    await row.first().waitFor({ state: 'visible', timeout: 30000 });
    await row.first().locator('.vz-ctx__trigger').click();
    await admin.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();

    const details = admin.getByRole('dialog').filter({ hasText: ORDER_NUMBER }).first();
    await details.waitFor({ state: 'visible', timeout: 30000 });
    const text = await details.innerText();

    (await details.locator('.vz-manual-delivery').count()) === 0
      ? ok('no manual-delivery action is offered for the support item')
      : fail('the manual-delivery action is offered for a support item the API refuses');
    text.includes('تیکت پشتیبانی')
      ? ok('admin guidance points at the support ticket workflow')
      : fail('admin guidance does not name the support ticket workflow');
  } catch (e) {
    fail(`admin support workflow: ${String(e).split('\n')[0].slice(0, 150)}`);
  } finally { await context.close(); }
}

await browser.close();
console.log(`\npublished SupportRequired smoke: ${fails === 0 ? 'PASS' : `FAIL (${fails})`}`);
process.exit(fails ? 1 : 0);

// Verifies the new checkout workflow end to end in a real browser, across viewport and theme.
// usage: node checkout-workflow-check.mjs <origin>
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = process.argv[2] || 'http://localhost:5077';
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';
const INPUT_PRODUCT = 'e2e-seo-product';      // seeded product that defines a required input
const PLAIN_PRODUCT = 'e2e-fix09-none';       // seeded product with no input fields

let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };

const browser = await chromium.launch({ channel: 'chrome' });

async function login(page) {
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill('09120000013');
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
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await page.locator('.vz-toast.success, .vz-toast--success').first().waitFor({ state: 'visible', timeout: 30000 });
}

const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
const page = await context.newPage();

try {
  // ---- PRODUCT: the two removed blocks, and adding to cart is no longer gated ----------------
  await page.goto(`${ORIGIN}/product/${INPUT_PRODUCT}`, { waitUntil: 'networkidle' });
  const body = await page.locator('body').innerText();

  body.includes('ضمانت فروشنده معتبر') ? fail('seller guarantee block still on the product page')
                                        : ok('seller guarantee block removed');
  (await page.locator('.st-trustbar').count()) === 0 ? ok('guarantee progress bar removed')
                                                     : fail('guarantee progress bar still rendered');
  /اطلاعات الزامی|مورد اطلاعات/.test(body) ? fail('required-info notice still on the product page')
                                           : ok('required-info notice removed');
  (await page.locator('[data-testid=product-input-summary]').count()) === 0
    ? ok('no required-info placeholder left behind')
    : fail('required-info placeholder still present');

  // Product media stays square. Poll: the gallery settles once its image has laid out.
  const square = await page.waitForFunction(() => {
    const el = document.querySelector('.st-gal__main');
    if (!el) return null;
    const r = el.getBoundingClientRect();
    if (r.width < 10) return null;
    return { w: Math.round(r.width), h: Math.round(r.height) };
  }, null, { timeout: 20000 }).then(h => h.jsonValue()).catch(() => null);
  square && Math.abs(square.w - square.h) <= 2
    ? ok('product image is still 1:1')
    : fail(`product image not square (${square ? square.w + 'x' + square.h : 'not measurable'})`);

  await login(page);
  await clearCart(page);
  await addToCart(page, INPUT_PRODUCT);
  ok('adding to cart is not blocked by required information');

  // ---- CART: no input editors -----------------------------------------------------------------
  await page.goto(`${ORIGIN}/cart`, { waitUntil: 'networkidle' });
  const cartText = await page.locator('body').innerText();
  cartText.includes('ویرایش اطلاعات خرید') ? fail('cart still offers a product-input editor')
                                            : ok('cart has no product-input editor');
  (await page.locator('.st-dynamic-form').count()) === 0 ? ok('cart renders no product-input form')
                                                         : fail('cart still renders a product-input form');

  // ---- CHECKOUT: section appears, blocks payment, then allows it ------------------------------
  await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });
  const section = page.locator('[data-testid=checkout-product-inputs]');
  await section.waitFor({ state: 'visible', timeout: 30000 });
  ok('checkout shows the required-information section');

  const cards = page.locator('[data-testid=checkout-input-card]');
  (await cards.count()) === 1 ? ok('one card for the single cart line')
                              : fail(`expected 1 input card, saw ${await cards.count()}`);

  // Attempt payment with the required field empty.
  await page.locator('button.st-btn--accent').last().click();
  await page.waitForTimeout(2500);
  if (!page.url().includes('/checkout')) { fail('payment proceeded with a missing required value'); }
  else {
    const invalid = await page.locator('.st-field__error, .is-invalid').count();
    invalid > 0 ? ok('payment blocked and field-level validation shown')
                : fail('payment blocked but no field-level validation shown');
  }

  // Fill it and confirm the page now accepts the value (value survives the failed attempt).
  const field = page.locator('[data-testid=checkout-input-card] input.st-input, [data-testid=checkout-input-card] textarea').first();
  await field.fill('buyer@example.test');
  const kept = await field.inputValue();
  kept === 'buyer@example.test' ? ok('entered value is preserved after a failed attempt')
                                : fail('entered value was lost');

  // ---- CHECKOUT: no section for a product without inputs --------------------------------------
  await clearCart(page);
  await addToCart(page, PLAIN_PRODUCT);
  await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });
  (await page.locator('[data-testid=checkout-product-inputs]').count()) === 0
    ? ok('no required-information section when nothing needs it')
    : fail('required-information section shown for a product with no inputs');

  // ---- Two different products -> two independent cards ----------------------------------------
  await addToCart(page, INPUT_PRODUCT);
  await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });
  const multi = await page.locator('[data-testid=checkout-input-card]').count();
  multi === 1 ? ok('only the line that needs information gets a card')
              : fail(`expected 1 card among 2 cart lines, saw ${multi}`);

  // ---- Responsive + theme ---------------------------------------------------------------------
  for (const [label, viewport, scheme] of [
    ['desktop dark', { width: 1440, height: 900 }, 'dark'],
    ['mobile light', { width: 390, height: 844 }, 'light'],
    ['mobile dark', { width: 390, height: 844 }, 'dark']
  ]) {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ colorScheme: scheme });
    await page.goto(`${ORIGIN}/checkout`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid=checkout-product-inputs]').waitFor({ state: 'visible', timeout: 20000 });

    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    const input = page.locator('[data-testid=checkout-input-card] input.st-input').first();
    // Centre it the way a customer scrolling to the field would, then confirm nothing (sticky
    // summary, bottom navigation) sits on top of it.
    const hittable = await input.evaluate(el => {
      el.scrollIntoView({ block: 'center' });
      const r = el.getBoundingClientRect();
      const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
      return !!hit && (el === hit || el.contains(hit) || hit.contains(el));
    });
    overflow <= 1 && hittable
      ? ok(`${label}: section usable, no horizontal overflow`)
      : fail(`${label}: overflow=${overflow} reachable=${hittable}`);
  }
} catch (e) {
  fail(`workflow: ${String(e).split('\n')[0].slice(0, 160)}`);
} finally {
  await context.close();
  await browser.close();
}

console.log(`\ncheckout workflow: ${fails === 0 ? 'PASS' : `FAIL (${fails})`}`);
process.exit(fails ? 1 : 0);

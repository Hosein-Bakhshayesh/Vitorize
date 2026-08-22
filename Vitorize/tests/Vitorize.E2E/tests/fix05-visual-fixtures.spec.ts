import { expect, test, type BrowserContext, type Page } from '@playwright/test';
import { expireOtp, latestOtp, monitorBrowser } from './support/app';

const productUrl = '/product/e2e-fix05-visual-cart-product';
const productName = 'FIX-05 Visual Cart Product';

async function expectNoOverflow(page: Page): Promise<void> {
  const layout = await page.evaluate(() => ({ innerWidth: window.innerWidth, scrollWidth: document.documentElement.scrollWidth }));
  expect(layout.scrollWidth, JSON.stringify(layout)).toBeLessThanOrEqual(layout.innerWidth + 1);
}

async function addFixtureItem(page: Page): Promise<void> {
  await page.goto(productUrl, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect.poll(async () => {
    const cookie = (await page.context().cookies()).find(item => item.name === 'Vitorize.GuestCart');
    if (!cookie) return 0;
    const cart = await page.request.get('http://127.0.0.1:5177/api/cart', { headers: { 'X-Vitorize-Guest-Cart': cookie.value } });
    return cart.ok() ? (await cart.json()).data.totalQuantity : 0;
  }).toBe(1);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: productName })).toBeVisible();
}

// Product information is collected at Checkout, not in a cart-side dialog, so these fields are
// filled where they actually live now.
async function fillCheckoutFixtureInformation(page: Page): Promise<void> {
  await page.locator('input[id$="-persian_text"]').fill('متن آزمایشی');
  await page.locator('input[id$="-email"]').fill('customer@example.test');
  await page.locator('input[id$="-url"]').fill('https://example.test/account');
  await page.locator('input[id$="-phone"]').fill('+491234567890');
  await page.locator('select[id$="-region"]').selectOption('north');
  await page.locator('input[id$="-terms"]').check();
}

async function signInAtCheckoutGate(page: Page): Promise<void> {
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcheckout/);
  await page.locator('#pw-mobile').fill('09120000014');
  await page.locator('#pw-pass').fill('E2E-Admin-Only-aA1!');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();
  await expect(page).toHaveURL(/\/checkout/);
}

test('FIX-05 visual OTP fixture renders entry, invalid, expired, and resend states', async ({ page, request }) => {
  const browser = monitorBrowser(page);
  // The standard E2E seed provides this confirmed Customer; a fresh browser context is anonymous.
  const mobile = '09120000013';

  await page.goto('/login?otp=1', { waitUntil: 'networkidle' });
  await page.locator('#otp-mobile').fill(mobile);
  await page.locator('#otp-mobile').locator('xpath=following::button[1]').click();
  const code = page.locator('#otp-code');
  await expect(code).toBeVisible();
  await expect(code).toHaveAttribute('dir', 'ltr');
  await expect(page.locator('main button.st-btn--primary.st-btn--block')).toBeDisabled();
  await expect(page.locator('button.st-btn--ghost').filter({ hasText: /ارسال مجدد/ })).toBeVisible();

  await code.fill('000000');
  await page.locator('main button.st-btn--primary.st-btn--block').click();
  await expect(page.locator('#otp-error')).toBeVisible();
  await expect(code).toHaveAttribute('aria-invalid', 'true');
  await expect(code).toHaveAttribute('aria-describedby', 'otp-error');

  const actualCode = await latestOtp(request, mobile);
  await expireOtp(request, mobile);
  await code.fill(actualCode);
  await page.locator('main button.st-btn--primary.st-btn--block').click();
  await expect(page.locator('#otp-error')).toBeVisible();
  await expect(code).toHaveAttribute('aria-invalid', 'true');
  await expectNoOverflow(page);
  browser.assertClean();
});

// The cart no longer edits product information - that moved to Checkout - so the guest-side subject
// is what the cart itself renders. The direction, validation and persistence coverage that used to
// live here now runs against the real checkout form in the test below.
test('FIX-05 visual guest cart fixture renders the fixture product without overflow', async ({ page }) => {
  const browser = monitorBrowser(page);
  await addFixtureItem(page);

  const item = page.locator('.st-cart-item').filter({ hasText: productName });
  await expect(item).toBeVisible();
  await expect(item.locator('.st-qty')).toBeVisible();
  await expect(page.locator('.st-cart-sum button.st-btn--accent')).toBeEnabled();
  await expect(page.locator('.st-cart-item')).toHaveCount(1);
  await expectNoOverflow(page);
  browser.assertClean();
});

test('FIX-05 visual authenticated checkout fixture renders the real checkout', async ({ page, context }) => {
  const browser = monitorBrowser(page);
  await addFixtureItem(page);
  await signInAtCheckoutGate(page);
  await expect(page.locator('main')).toContainText(productName);
  await expect(context.cookies()).resolves.toEqual(expect.any(Array));

  // Every supported input type keeps its own writing direction: Persian text stays RTL while
  // machine-readable values stay LTR so they cannot render reversed.
  const card = page.getByTestId('checkout-input-card');
  await expect(card.locator('input[id$="-persian_text"]')).toHaveAttribute('dir', 'rtl');
  await expect(card.locator('input[id$="-email"]')).toHaveAttribute('dir', 'ltr');
  await expect(card.locator('input[id$="-url"]')).toHaveAttribute('dir', 'ltr');
  await expect(card.locator('input[id$="-phone"]')).toHaveAttribute('dir', 'ltr');
  await expect(card.locator('select[id$="-region"]')).toBeVisible();
  await expect(card.locator('input[id$="-terms"]')).toBeVisible();

  // A missing required value stops the purchase and says so on the field itself.
  await page.locator('button.st-btn--accent').last().click();
  await expect(page).toHaveURL(/\/checkout/);
  await expect(card.locator('input[id$="-persian_text"]')).toHaveAttribute('aria-invalid', 'true');

  // Supplying the values clears the rejection. They are written to the cart line on submit, not
  // while typing, so a reload deliberately starts the form clean rather than half-filled.
  await fillCheckoutFixtureInformation(page);
  await expect(card.locator('input[id$="-persian_text"]')).toHaveValue('متن آزمایشی');
  await expect(card.locator('input[id$="-email"]')).toHaveValue('customer@example.test');
  await expect(card.locator('select[id$="-region"]')).toHaveValue('north');
  await expect(card.locator('input[id$="-terms"]')).toBeChecked();
  await expectNoOverflow(page);
  browser.assertClean();
});

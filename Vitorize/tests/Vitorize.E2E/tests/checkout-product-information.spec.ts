import { expect, test, clearCustomerCart } from '../framework/fixtures';

/**
 * Product-required information is collected at Checkout — not on the product page and not in the
 * cart. These tests pin the customer-visible half of that contract: the product page no longer
 * announces or blocks on it, the cart carries no editors, and Checkout shows one card per cart line
 * that needs information and refuses to start a payment until the required values are valid.
 */

const INPUT_PRODUCT = 'e2e-seo-product';   // defines a required input
const PLAIN_PRODUCT = 'e2e-fix09-none';    // defines none
const CUSTOMER = '09120000013';

test.describe('checkout product information @checkoutinputs', () => {
  test.describe.configure({ timeout: 180_000 });

  test('the product page neither announces required information nor blocks the cart on it', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await page.goto(`/product/${INPUT_PRODUCT}`, { waitUntil: 'networkidle' });

    await expect(page.locator('.st-trustbar')).toHaveCount(0);
    await expect(page.getByTestId('product-input-summary')).toHaveCount(0);
    await expect(page.locator('body')).not.toContainText('ضمانت فروشنده معتبر');
    await expect(page.locator('body')).not.toContainText('اطلاعات الزامی');

    // Adding to the cart happens immediately; no information dialog stands in the way.
    await login(page);
    await clearCustomerCart(page);
    await page.goto(`/product/${INPUT_PRODUCT}`, { waitUntil: 'networkidle' });
    await page.locator('.st-buy__card button.st-btn--accent').click();
    await expect(page.locator('.vz-toast.success, .vz-toast--success').first()).toBeVisible();
    await expect(page.locator('.vz-dialog')).toHaveCount(0);
  });

  test('the cart carries no product-input editor', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await login(page);
    await clearCustomerCart(page);
    await addToCart(page, INPUT_PRODUCT);

    await page.goto('/cart', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-cart-item').first()).toBeVisible();
    await expect(page.locator('.st-dynamic-form')).toHaveCount(0);
    await expect(page.locator('body')).not.toContainText('ویرایش اطلاعات خرید');
  });

  test('checkout collects the information and refuses to pay until it is valid', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await login(page);
    await clearCustomerCart(page);
    await addToCart(page, INPUT_PRODUCT);

    await page.goto('/checkout', { waitUntil: 'networkidle' });
    const section = page.getByTestId('checkout-product-inputs');
    await expect(section).toBeVisible();
    await expect(page.getByTestId('checkout-input-card')).toHaveCount(1);

    // Attempting payment with the required value empty must not leave the checkout page.
    await page.locator('button.st-btn--accent').last().click();
    await page.waitForTimeout(2000);
    await expect(page).toHaveURL(/\/checkout/);
    await expect(page.locator('.st-field__error, .is-invalid').first()).toBeVisible();

    // The value survives that refusal rather than forcing the customer to retype.
    const field = page.locator('[data-testid=checkout-input-card] input.st-input').first();
    await field.fill('buyer@example.test');
    await expect(field).toHaveValue('buyer@example.test');
  });

  test('no information section appears when nothing in the cart needs one', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await login(page);
    await clearCustomerCart(page);
    await addToCart(page, PLAIN_PRODUCT);

    await page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('checkout-product-inputs')).toHaveCount(0);
    await expect(page.locator('button.st-btn--accent').last()).toBeVisible();
  });

  test('only the lines that need information get a card', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await login(page);
    await clearCustomerCart(page);
    await addToCart(page, PLAIN_PRODUCT);
    await addToCart(page, INPUT_PRODUCT);

    await page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('checkout-input-card')).toHaveCount(1);
    // The card names the item it belongs to, so the association is never ambiguous.
    await expect(page.getByTestId('checkout-input-card').first()).toContainText('E2E Dynamic Product');
  });

  test('the information section stays usable on the current viewport and theme', async ({ page }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await login(page);
    await clearCustomerCart(page);
    await addToCart(page, INPUT_PRODUCT);

    await page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('checkout-product-inputs')).toBeVisible();

    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, 'the section must not push the page sideways').toBeLessThanOrEqual(1);

    // Nothing (sticky summary, bottom navigation) may sit on top of the field.
    const reachable = await page.locator('[data-testid=checkout-input-card] input.st-input').first().evaluate(el => {
      el.scrollIntoView({ block: 'center' });
      const r = el.getBoundingClientRect();
      const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
      return !!hit && (el === hit || el.contains(hit) || hit.contains(el));
    });
    expect(reachable).toBe(true);
  });
});

async function sizeFor(page: import('@playwright/test').Page, project: string) {
  await page.setViewportSize(project.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
}

async function login(page: import('@playwright/test').Page) {
  if (await page.locator('form[action="/auth/customer/logout"]').count()) return;
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(CUSTOMER);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

async function addToCart(page: import('@playwright/test').Page, slug: string) {
  await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success, .vz-toast--success').first()).toBeVisible();
}

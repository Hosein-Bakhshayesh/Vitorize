import { expect, test, clearCustomerCart } from '../framework/fixtures';

/**
 * The checkout KYC panel used to hide its own call to action behind an inline stylesheet, so a
 * customer who needed verification saw one sentence and had no way to reach the flow. These tests
 * cover the panel across both viewports and both themes, and follow the action through to the
 * existing verification page — no second KYC workflow is introduced.
 */

const KYC_REQUIRED_PRODUCT = 'e2e-fix09-always';   // KycRequirementMode.Always
const NO_KYC_PRODUCT = 'e2e-fix09-none';           // KycRequirementMode.None

const UNVERIFIED_CUSTOMER = '09120000013';
const VERIFIED_CUSTOMER = '09120000014';           // CustomerVIP, already verified

test.describe('checkout KYC call to action @kyccta', () => {
  test.describe.configure({ timeout: 180_000 });

  test('a customer needing verification sees the explanation, the state and a working CTA', async ({ page, consoleGuard }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await loginStoreCustomer(page, UNVERIFIED_CUSTOMER);
    await clearCustomerCart(page);
    await addProduct(page, KYC_REQUIRED_PRODUCT);
    await goToCheckout(page);

    const panel = page.getByTestId('checkout-kyc-information');
    await expect(panel).toBeVisible();

    // Explanation and current state are both readable, not just present in the DOM.
    await expect(panel.getByTestId('checkout-kyc-post-payment-copy')).toBeVisible();
    await expect(panel.getByTestId('checkout-kyc-post-payment-copy')).toContainText('پس از پرداخت');
    await expect(panel.getByTestId('checkout-kyc-state')).toBeVisible();

    const action = panel.locator('a[href="/customer/verification"]');
    await expect(action).toBeVisible();
    await expect(action).toBeEnabled();
    await expect(action).toContainText(/احراز هویت/);

    // Genuinely reachable: not clipped, not covered by the mobile bottom navigation.
    await expect(await isActuallyClickable(page, action)).toBe(true);

    await action.click();
    await expect(page).toHaveURL(/\/customer\/verification/);
    await expect(page.locator('main, .st-stack').first()).toBeVisible();

    consoleGuard.assertClean();
  });

  test('a checkout without KYC items shows no verification prompt', async ({ page, consoleGuard }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await loginStoreCustomer(page, UNVERIFIED_CUSTOMER);
    await clearCustomerCart(page);
    await addProduct(page, NO_KYC_PRODUCT);
    await goToCheckout(page);

    await expect(page.getByTestId('checkout-kyc-information')).toHaveCount(0);
    consoleGuard.assertClean();
  });

  test('an already verified customer is never told to complete verification', async ({ page, consoleGuard }, testInfo) => {
    await sizeFor(page, testInfo.project.name);
    await loginStoreCustomer(page, VERIFIED_CUSTOMER);
    await clearCustomerCart(page);
    await addProduct(page, KYC_REQUIRED_PRODUCT);
    await goToCheckout(page);

    await expect(page.getByTestId('checkout-kyc-information')).toHaveCount(0);
    await expect(page.locator('button.st-btn--accent').last()).toBeVisible();
    consoleGuard.assertClean();
  });
});

async function loginStoreCustomer(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

async function sizeFor(page: import('@playwright/test').Page, project: string) {
  await page.setViewportSize(project.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
}

async function addProduct(page: import('@playwright/test').Page, slug: string) {
  await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
}

async function goToCheckout(page: import('@playwright/test').Page) {
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/checkout/);
}

/** True when the element's own centre point is what the browser would actually hit. */
async function isActuallyClickable(page: import('@playwright/test').Page, locator: import('@playwright/test').Locator) {
  await locator.scrollIntoViewIfNeeded();
  return locator.evaluate((element: HTMLElement) => {
    const rect = element.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return false;
    const hit = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    return element.contains(hit) || (hit?.contains(element) ?? false);
  });
}

import { expect, test, clearCustomerCart } from '../framework/fixtures';
import { apiBaseUrl } from './support/app';

const products = {
  none: 'e2e-fix09-none',
  always: 'e2e-fix09-always',
  threshold: 'e2e-fix09-quantity',
  couponThreshold: 'e2e-fix09-above'
} as const;

type Order = {
  id: string;
  items: Array<{ productId: string; quantity: number; requiresVerification: boolean }>;
};

test.describe('FIX-09 Phase 1 current Checkout KYC @fix09p1checkoutkyc', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop light applies the current pre-payment KYC gate without coupon bypass', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'The complete Checkout/KYC lifecycle is exercised once in desktop light.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await assertFixtureActors(request);

    await loginAs('Customer');
    await clearCustomerCart(page);

    await addProduct(page, products.none);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);

    await clearCustomerCart(page);
    await addProduct(page, products.threshold);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);

    await page.goto(`/product/${products.threshold}`);
    await page.locator('.st-buy__qty .st-qty button').last().click();
    await expect(page.locator('.st-alert--info')).toContainText('احراز هویت');
    await page.locator('.st-buy__qty .st-qty button').first().click();
    await expect(page.locator('.st-alert--info')).toHaveCount(0);
    await addProduct(page, products.threshold);
    await page.goto('/cart');
    const thresholdItem = page.locator('.st-cart-item').filter({ hasText: 'E2E FIX09 Quantity' });
    await thresholdItem.locator('.st-qty button').last().click();
    await expect(thresholdItem.locator('.st-qty')).toContainText('۲');
    await expect(page.getByTestId('cart-kyc-requirement')).toContainText('احراز هویت');
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toContainText('احراز هویت');
    await expect(checkoutKycAlert(page).locator('a[href="/customer/verification"]')).toBeVisible();
    const beforeBlockedThreshold = await ordersFor(request, 'Customer');
    await placeOrder(page);
    await expect(page.locator('.vz-toast.error, .vz-toast--error').last()).toContainText('احراز هویت');
    await expect(page).toHaveURL(/\/checkout/);
    expect((await ordersFor(request, 'Customer')).length).toBe(beforeBlockedThreshold.length);
    await expect(page.locator('.st-card').filter({ hasText: 'E2E FIX09 Quantity' })).toContainText('۲');

    await clearCustomerCart(page);
    await addProduct(page, products.always);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toContainText('احراز هویت');
    const beforeBlockedAlways = await ordersFor(request, 'Customer');
    await placeOrder(page);
    await expect(page.locator('.vz-toast.error, .vz-toast--error').last()).toContainText('احراز هویت');
    expect((await ordersFor(request, 'Customer')).length).toBe(beforeBlockedAlways.length);

    await clearCustomerCart(page);
    await addProduct(page, products.couponThreshold);
    await page.goto('/cart');
    await page.locator('.st-promo input').fill('E2E10');
    await page.locator('.st-promo button').click();
    await expect(page.locator('.st-promo__msg.ok')).toBeVisible();
    await expect(page.locator('.st-cart-sum')).toContainText('E2E10');
    await goToCheckoutFromCart(page, false);
    await expect(page.locator('.st-sumrow[style*="success"]')).toBeVisible();
    await expect(checkoutKycAlert(page)).toContainText('احراز هویت');
    const beforeBlockedCoupon = await ordersFor(request, 'Customer');
    await placeOrder(page);
    await expect(page.locator('.vz-toast.error, .vz-toast--error').last()).toContainText('احراز هویت');
    expect((await ordersFor(request, 'Customer')).length).toBe(beforeBlockedCoupon.length);

    await loginAs('CustomerVIP');
    await clearCustomerCart(page);
    const beforeVerified = await ordersFor(request, 'CustomerVIP');
    await addProduct(page, products.threshold);
    await page.goto('/cart');
    await page.locator('.st-cart-item .st-qty button').last().click();
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);
    await placeOrder(page);
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
    const verifiedOrders = await ordersFor(request, 'CustomerVIP');
    expect(verifiedOrders.length).toBe(beforeVerified.length + 1);
    const verifiedOrder = verifiedOrders[0];
    expect(verifiedOrder.items).toContainEqual(expect.objectContaining({ quantity: 2, requiresVerification: true }));

    await clearCustomerCart(page);
    await addProduct(page, products.always);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);
    consoleGuard.assertClean();
  });

  test('desktop dark keeps None and triggered checkout states readable', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-dark', 'Dark representative check only.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('Customer');
    await clearCustomerCart(page);
    await addProduct(page, products.none);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);
    await clearCustomerCart(page);
    await addProduct(page, products.always);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toContainText('احراز هویت');
    consoleGuard.assertClean();
  });

  test('mobile keeps the triggered KYC block readable and reachable', async ({ page, consoleGuard }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'Mobile representative check only.');
    await page.setViewportSize({ width: 390, height: 844 });
    await loginStoreCustomer(page, '09120000013');
    await clearCustomerCart(page);
    await addProduct(page, products.always);
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toBeVisible();
    await expect(checkoutKycAlert(page).locator('a[href="/customer/verification"]')).toBeVisible();
    await expectNoOverflow(page);
    consoleGuard.assertClean();
  });

  test('mobile allows verified triggered checkout', async ({ page, consoleGuard }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'Mobile representative check only.');
    await page.setViewportSize({ width: 390, height: 844 });
    await loginStoreCustomer(page, '09120000014');
    await clearCustomerCart(page);
    await addProduct(page, products.threshold);
    await page.goto('/cart');
    await page.locator('.st-cart-item .st-qty button').last().click();
    await goToCheckoutFromCart(page);
    await expect(checkoutKycAlert(page)).toHaveCount(0);
    await expect(page.locator('button.st-btn--accent').last()).toBeVisible();
    await expectNoOverflow(page);
    consoleGuard.assertClean();
  });
});

async function addProduct(page: import('@playwright/test').Page, slug: string) {
  await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
}

async function goToCheckoutFromCart(page: import('@playwright/test').Page, loadCart = true) {
  if (loadCart) await page.goto('/cart', { waitUntil: 'networkidle' });
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/checkout/);
}

async function placeOrder(page: import('@playwright/test').Page) {
  await page.locator('button.st-btn--accent').last().click();
}

function checkoutKycAlert(page: import('@playwright/test').Page) {
  return page.getByTestId('checkout-kyc-gate');
}

async function assertFixtureActors(request: import('@playwright/test').APIRequestContext) {
  expect((await customerProfile(request, 'Customer')).verificationStatus).not.toBe(1);
  expect((await customerProfile(request, 'CustomerVIP')).verificationStatus).toBe(1);
}

async function ordersFor(request: import('@playwright/test').APIRequestContext, role: 'Customer' | 'CustomerVIP') {
  return api<Order[]>(request, role, '/orders');
}

async function customerProfile(request: import('@playwright/test').APIRequestContext, role: 'Customer' | 'CustomerVIP') {
  return api<{ verificationStatus: number }>(request, role, '/auth/me');
}

async function api<T>(request: import('@playwright/test').APIRequestContext, role: 'Customer' | 'CustomerVIP', route: string): Promise<T> {
  const users = {
    Customer: { mobile: process.env.E2E_CUSTOMER_MOBILE ?? '09120000013' },
    CustomerVIP: { mobile: process.env.E2E_VIP_MOBILE ?? '09120000014' }
  };
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  const login = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: users[role].mobile, password } });
  expect(login.ok()).toBeTruthy();
  const accessToken = ((await login.json()) as { data: { accessToken: string } }).data.accessToken;
  const response = await request.get(`${apiBaseUrl}${route}`, { headers: { Authorization: `Bearer ${accessToken}` } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: T }).data;
}

async function expectNoOverflow(page: import('@playwright/test').Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
}

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

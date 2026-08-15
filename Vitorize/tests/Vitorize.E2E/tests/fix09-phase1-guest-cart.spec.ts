import { expect, test } from '../framework/fixtures';
import { apiBaseUrl } from './support/app';

const guestCookie = 'Vitorize.GuestCart';
const product = { slug: 'e2e-fix09-quantity', title: 'E2E FIX09 Quantity' };

type Cart = { id: string; totalQuantity: number; items: Array<{ id: string; quantity: number; requiresKyc: boolean; kycRequirementMode: number; kycThresholdAmount: number | null; kycEvaluatedAmount: number }> };

test.describe('FIX-09 Phase 1 guest cart KYC regression @fix09p1guest', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop light preserves guest quantity through merge and blocks KYC before payment', async ({ page, context, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'The full guest-to-checkout journey runs once in desktop light.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await runGuestJourney(page, context, request, true);
    consoleGuard.assertClean();
  });

  test('desktop dark keeps the guest merge and KYC block readable', async ({ page, context, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-dark', 'Dark representative only.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await runGuestJourney(page, context, request, false);
    consoleGuard.assertClean();
  });

  test('mobile light keeps guest quantity controls and the KYC CTA reachable', async ({ page, context, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile light representative is required.');
    await page.setViewportSize({ width: 390, height: 844 });
    await runGuestJourney(page, context, request, false);
    await expectNoOverflow(page);
    consoleGuard.assertClean();
  });
});

async function runGuestJourney(page: import('@playwright/test').Page, context: import('@playwright/test').BrowserContext, request: import('@playwright/test').APIRequestContext, secondTab: boolean) {
  expect((await context.cookies()).some(cookie => cookie.name === guestCookie)).toBe(false);
  await page.goto(`/product/${product.slug}`, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect.poll(async () => {
    const capability = (await context.cookies()).find(cookie => cookie.name === guestCookie)?.value;
    if (!capability) return 0;
    const response = await page.request.get(`${apiBaseUrl}/cart`, { headers: { 'X-Vitorize-Guest-Cart': capability } });
    return response.ok() ? ((await response.json() as { data: Cart }).data.totalQuantity) : 0;
  }).toBe(1);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  const item = page.locator('.st-cart-item').filter({ hasText: product.title });
  await expect(item).toHaveCount(1);
  await item.locator('.st-qty button').last().click();
  await expect(item.locator('.st-qty span')).toHaveText('۲');

  const capability = await guestCapability(context);
  await expectGuestCookie(context);
  await expectGuestCart(page, capability, 2, true);
  const storage = await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) }));
  expect([...storage.local, ...storage.session]).not.toContain(guestCookie);

  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: product.title })).toHaveCount(1);
  await expectGuestCart(page, capability, 2, true);
  if (secondTab) {
    const tab = await context.newPage();
    try {
      await tab.goto('/cart', { waitUntil: 'networkidle' });
      await expect(tab.locator('.st-cart-item').filter({ hasText: product.title })).toHaveCount(1);
      await expectGuestCart(tab, capability, 2, true);
    } finally { await tab.close(); }
  }

  const customerToken = await customerTokenFor(request);
  await expectCustomerCart(request, customerToken, 0);
  await page.goto('/login?returnUrl=%2Fcart', { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill(password());
  await Promise.all([page.waitForURL(/\/cart/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
  expect((await context.cookies()).some(cookie => cookie.name === guestCookie)).toBe(false);
  await expect(page.locator('.st-cart-item').filter({ hasText: product.title })).toHaveCount(1);
  await expectCustomerCart(request, customerToken, 2, true);
  await expectConsumedGuestCapability(page, capability);

  await page.reload({ waitUntil: 'networkidle' });
  await expectCustomerCart(request, customerToken, 2, true);
  await page.goto('/shop', { waitUntil: 'networkidle' });
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: product.title })).toHaveCount(1);
  await expect(page.getByTestId('cart-kyc-requirement')).toBeVisible();
  await expectCustomerCart(request, customerToken, 2, true);

  const ordersBefore = await ordersFor(request, customerToken);
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/checkout/);
  const information = page.getByTestId('checkout-kyc-information');
  await expect(information).toBeVisible();
  await expect(information.getByTestId('checkout-kyc-post-payment-copy')).toContainText('پس از پرداخت');
  await page.locator('button.st-btn--accent').last().click();
  await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
  expect((await ordersFor(request, customerToken)).length).toBe(ordersBefore.length + 1);

  const paymentResultOrderId = new URL(page.url()).searchParams.get('orderId');
  expect(paymentResultOrderId).toBeTruthy();
  const kycCta = page.getByRole('link', { name: 'تکمیل احراز هویت', exact: true });
  await expect(kycCta).toBeVisible();
  await expect(kycCta).toHaveAttribute('href', `/customer/orders/${paymentResultOrderId}`);
}

async function guestCapability(context: import('@playwright/test').BrowserContext): Promise<string> {
  const cookie = (await context.cookies()).find(value => value.name === guestCookie);
  expect(cookie).toBeTruthy();
  return cookie!.value;
}

async function expectGuestCookie(context: import('@playwright/test').BrowserContext) {
  const cookie = (await context.cookies()).find(value => value.name === guestCookie);
  expect(cookie).toBeTruthy();
  expect(cookie!.httpOnly).toBe(true);
  expect(cookie!.sameSite).toBe('Lax');
  expect(cookie!.path).toBe('/');
}

async function expectGuestCart(page: import('@playwright/test').Page, capability: string, quantity: number, expectsKyc: boolean) {
  const response = await page.request.get(`${apiBaseUrl}/cart`, { headers: { 'X-Vitorize-Guest-Cart': capability } });
  expect(response.ok()).toBeTruthy();
  const cart = (await response.json() as { data: Cart }).data;
  expect(cart.items).toHaveLength(1);
  expect(cart.totalQuantity).toBe(quantity);
  expect(cart.items[0]).toMatchObject({ quantity, requiresKyc: expectsKyc, kycRequirementMode: 2, kycThresholdAmount: 4000, kycEvaluatedAmount: 5000 });
}

async function expectConsumedGuestCapability(page: import('@playwright/test').Page, capability: string) {
  const response = await page.request.get(`${apiBaseUrl}/cart`, { headers: { 'X-Vitorize-Guest-Cart': capability } });
  expect(response.ok()).toBeTruthy();
  const cart = (await response.json() as { data: Cart }).data;
  expect(cart.totalQuantity).toBe(0);
  expect(cart.items).toEqual([]);
}

async function customerTokenFor(request: import('@playwright/test').APIRequestContext) {
  const response = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: '09120000013', password: password() } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: { accessToken: string } }).data.accessToken;
}

async function expectCustomerCart(request: import('@playwright/test').APIRequestContext, token: string, quantity: number, expectsKyc = false) {
  const response = await request.get(`${apiBaseUrl}/cart`, { headers: { Authorization: `Bearer ${token}` } });
  expect(response.ok()).toBeTruthy();
  const cart = (await response.json() as { data: Cart }).data;
  if (quantity === 0) { expect(cart.items).toEqual([]); return; }
  expect(cart.items).toHaveLength(1);
  expect(cart.totalQuantity).toBe(quantity);
  expect(cart.items[0]).toMatchObject({ quantity, requiresKyc: expectsKyc, kycRequirementMode: 2, kycThresholdAmount: 4000, kycEvaluatedAmount: 5000 });
}

async function ordersFor(request: import('@playwright/test').APIRequestContext, token: string) {
  const response = await request.get(`${apiBaseUrl}/orders`, { headers: { Authorization: `Bearer ${token}` } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: Array<{ id: string }> }).data;
}

function password() { return process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!'; }

async function expectNoOverflow(page: import('@playwright/test').Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
}

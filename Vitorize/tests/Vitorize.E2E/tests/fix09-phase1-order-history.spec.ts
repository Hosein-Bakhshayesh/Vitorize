import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, monitorBrowser } from './support/app';

const productIds = {
  above: '31000000-0000-0000-0000-000000000053',
  quantity: '31000000-0000-0000-0000-000000000052',
  policyV2: '31000000-0000-0000-0000-000000000043'
} as const;

type OrderFixture = { id: string; number: string; title: string; delivery?: string };
type FixtureSet = { v1: OrderFixture; v2: OrderFixture; retry: OrderFixture; legacy: OrderFixture };
type ApiResult<T> = { data: T };
type Product = Record<string, unknown> & { id: string; kycPolicyVersionId: string | null; kycThresholdAmount: number | null };
type Order = { id: string; orderNumber: string; items: Array<{ id: string; productTitle: string }> };
type PaymentStart = { paymentId: string; authority: string | null };

test.describe('FIX-09 Phase 1 order history regression @fix09p1history', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop light renders V1, V2 and retry orders for Admin and their owner', async ({ page, browser, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin is exercised once in desktop light.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const orders = await prepareOrders(request);
    await loginAs('SuperAdmin');

    for (const order of [orders.v1, orders.v2, orders.retry, orders.legacy]) await openAdminOrder(page, order);

    const customerContext = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR', timezoneId: 'Asia/Tehran', colorScheme: 'light' });
    const customerPage = await customerContext.newPage();
    const customerGuard = monitorBrowser(customerPage);
    try {
      await loginCustomer(customerPage, '09120000014');
      for (const order of [orders.v1, orders.v2, orders.retry, orders.legacy]) await openCustomerOrder(customerPage, order);
      await expect(customerPage.locator('body')).not.toContainText('KYC');
      customerGuard.assertClean();
    } finally {
      await customerContext.close();
    }
    consoleGuard.assertClean();
  });

  test('desktop dark keeps customer V1, V2 and retry details compatible', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-dark', 'Customer dark representative only.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const orders = await prepareOrders(request);
    await loginCustomer(page, '09120000014');
    for (const order of [orders.v1, orders.v2, orders.retry]) await openCustomerOrder(page, order);
    await expect(page.locator('body')).not.toContainText('KYC');
    consoleGuard.assertClean();
  });

  test('mobile customer retry details remain readable', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile light customer smoke is required.');
    await page.setViewportSize({ width: 390, height: 844 });
    const orders = await prepareOrders(request);
    await loginCustomer(page, '09120000014');
    await openCustomerOrder(page, orders.retry);
    await expectNoOverflow(page);
    await expect(page.locator('body')).not.toContainText('KYC');
    consoleGuard.assertClean();
  });
});

async function prepareOrders(request: import('@playwright/test').APIRequestContext): Promise<FixtureSet> {
  const owner = await tokenFor(request, '09120000014');
  const otherCustomer = await tokenFor(request, '09120000013');
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const v1 = await checkoutAndPay(request, owner, productIds.above, 1);
  const v1Detail = await orderDetails(request, admin, `/admin/orders/${v1.id}`);
  const delivery = `FIX09 delivered V1 ${Date.now()}`;
  await expectOk(await request.post(`${apiBaseUrl}/admin/orders/${v1.id}/deliver-manual`, {
    headers: bearer(admin), data: { orderItemId: v1Detail.items[0].id, content: delivery, isVisibleToCustomer: true }
  }));

  await updateKycProduct(request, admin, productIds.above, productIds.policyV2, 4_000);
  const v2 = await checkoutAndPay(request, owner, productIds.above, 1);

  const retry = await checkout(request, owner, productIds.quantity, 2);
  const first = await startPayment(request, owner, retry.id, false);
  await expectOk(await request.get(`${apiBaseUrl}/payments/zarinpal/callback?Authority=${encodeURIComponent(first.authority!)}&Status=NOK`, { headers: bearer(owner) }));
  await updateKycProduct(request, admin, productIds.quantity, productIds.policyV2, 10_000);
  await pay(request, owner, retry.id, true);

  const legacyProductId = await createLegacyCompatibleProduct(request, admin);
  const legacy = await checkoutAndPay(request, owner, legacyProductId, 1);

  for (const order of [v1, v2, retry, legacy]) {
    const customerDetail = await orderDetails(request, owner, `/orders/${order.id}`);
    expect(customerDetail.items).toHaveLength(1);
    const adminDetail = await orderDetails(request, admin, `/admin/orders/${order.id}`);
    expect(adminDetail.items).toHaveLength(1);
  }
  const idor = await request.get(`${apiBaseUrl}/orders/${v1.id}`, { headers: bearer(otherCustomer) });
  expect(idor.status()).toBe(404);
  return { v1: { ...v1, delivery }, v2, retry, legacy };
}

async function checkoutAndPay(request: import('@playwright/test').APIRequestContext, token: string, productId: string, quantity: number) {
  const order = await checkout(request, token, productId, quantity);
  await pay(request, token, order.id, false);
  return order;
}

async function checkout(request: import('@playwright/test').APIRequestContext, token: string, productId: string, quantity: number): Promise<OrderFixture> {
  await expectOk(await request.delete(`${apiBaseUrl}/cart/clear`, { headers: bearer(token) }));
  await expectOk(await request.post(`${apiBaseUrl}/cart/items`, { headers: bearer(token), data: { productId, quantity } }));
  const checkout = await request.post(`${apiBaseUrl}/checkout`, {
    headers: { ...bearer(token), 'Idempotency-Key': `fix09-history-${Date.now()}-${Math.random()}` }, data: {}
  });
  await expectOk(checkout);
  const data = (await checkout.json() as ApiResult<{ orderId: string; orderNumber: string }>).data;
  const detail = await orderDetails(request, token, `/orders/${data.orderId}`);
  return { id: data.orderId, number: data.orderNumber, title: detail.items[0].productTitle };
}

async function pay(request: import('@playwright/test').APIRequestContext, token: string, orderId: string, retry: boolean) {
  const started = await startPayment(request, token, orderId, retry);
  await expectOk(await request.post(`${apiBaseUrl}/payments/mock/verify/${started.paymentId}`, { headers: bearer(token) }));
}

async function startPayment(request: import('@playwright/test').APIRequestContext, token: string, orderId: string, retry: boolean): Promise<PaymentStart> {
  const response = await request.post(`${apiBaseUrl}/payments/${retry ? 'retry' : 'start'}/${orderId}`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<PaymentStart>).data;
}

async function updateKycProduct(request: import('@playwright/test').APIRequestContext, token: string, productId: string, versionId: string, threshold: number) {
  const existing = await request.get(`${apiBaseUrl}/admin/products/${productId}`, { headers: bearer(token) });
  await expectOk(existing);
  const product = (await existing.json() as ApiResult<Product>).data;
  const updated = await request.put(`${apiBaseUrl}/admin/products/${productId}`, {
    headers: bearer(token), data: { ...product, requiresVerification: true, kycRequirementMode: 2, kycThresholdAmount: threshold, kycPolicyVersionId: versionId }
  });
  await expectOk(updated);
}

async function createLegacyCompatibleProduct(request: import('@playwright/test').APIRequestContext, token: string): Promise<string> {
  const versions = await request.get(`${apiBaseUrl}/admin/kyc/policy-versions`, { headers: bearer(token) });
  await expectOk(versions);
  const legacyVersion = (await versions.json() as ApiResult<Array<{ id: string; policyCode: string }>>).data
    .find(version => version.policyCode === 'legacy-profile-verification');
  expect(legacyVersion).toBeTruthy();
  const suffix = Date.now().toString(36);
  const created = await request.post(`${apiBaseUrl}/admin/products`, {
    headers: bearer(token),
    data: {
      categoryId: '31000000-0000-0000-0000-000000000001', title: `FIX09 migrated legacy ${suffix}`, slug: `fix09-migrated-legacy-${suffix}`,
      productType: 1, deliveryType: 2, basePrice: 5_000, currencyType: 2, requiresVerification: true,
      kycRequirementMode: 1, kycPolicyVersionId: legacyVersion!.id, minOrderQuantity: 1, isActive: true
    }
  });
  await expectOk(created);
  return (await created.json() as ApiResult<{ id: string }>).data.id;
}

async function orderDetails(request: import('@playwright/test').APIRequestContext, token: string, route: string): Promise<Order> {
  const response = await request.get(`${apiBaseUrl}${route}`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<Order>).data;
}

async function tokenFor(request: import('@playwright/test').APIRequestContext, mobile: string): Promise<string> {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  const response = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile, password } });
  await expectOk(response);
  return (await response.json() as ApiResult<{ accessToken: string }>).data.accessToken;
}

function bearer(token: string) { return { Authorization: `Bearer ${token}` }; }

async function expectOk(response: import('@playwright/test').APIResponse) {
  expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy();
}

async function openAdminOrder(page: import('@playwright/test').Page, order: OrderFixture) {
  await page.goto('/admin/orders', { waitUntil: 'networkidle' });
  await page.locator('#order-search').fill(order.number);
  const row = page.locator('tbody tr').filter({ hasText: order.number });
  await expect(row).toHaveCount(1);
  await row.locator('.vz-ctx__trigger').click();
  await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
  const detail = page.getByRole('dialog').filter({ hasText: order.number });
  await expect(detail).toBeVisible();
  await expect(detail.locator('tbody tr')).toHaveCount(1);
  await expect(detail.locator('#completion-reason')).toBeVisible();
  await expect(detail.locator('.vz-deflist')).toHaveCount(2);
  await detail.locator('button.vz-btn--outline').last().click();
  await expect(detail).toBeHidden();
}

async function openCustomerOrder(page: import('@playwright/test').Page, order: OrderFixture) {
  await page.goto(`/customer/orders/${order.id}`, { waitUntil: 'networkidle' });
  await expect(page.locator('h1 .st-mono')).toHaveText(order.number);
  await expect(page.locator('.st-card').filter({ hasText: order.title }).first()).toBeVisible();
  await expect(page.locator('.st-sumrow')).toHaveCount(5);
  if (order.delivery) await expect(page.locator('main')).toContainText(order.delivery);
  await expect(page).not.toHaveURL(/error|exception/i);
}

async function loginCustomer(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function expectNoOverflow(page: import('@playwright/test').Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
}

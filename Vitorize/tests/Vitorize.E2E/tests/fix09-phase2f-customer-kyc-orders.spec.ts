import { expect, test, clearCustomerCart, expectRtlAndNoOverflow } from '../framework/fixtures';
import { apiBaseUrl, monitorBrowser } from './support/app';

const order = {
  main: '32000000-0000-0000-0000-000000000001',
  v2: '32000000-0000-0000-0000-000000000002',
  mixed: '32000000-0000-0000-0000-000000000003',
  payment: '32000000-0000-0000-0000-000000000004'
} as const;

const item = {
  awaitingSubmission: '32000000-0000-0000-0000-000000000101',
  awaitingReview: '32000000-0000-0000-0000-000000000102',
  rejected: '32000000-0000-0000-0000-000000000103',
  finalRejected: '32000000-0000-0000-0000-000000000104',
  releasePending: '32000000-0000-0000-0000-000000000201',
  v2Submission: '32000000-0000-0000-0000-000000000202'
} as const;

const heldCanary = 'FIX09-P2F-HELD-CANARY-DO-NOT-RENDER';

test.describe('FIX-09 Phase 2F Customer KYC orders @fix09p2fcustomer', () => {
  test.describe.configure({ timeout: 180_000 });

  test('customer order details map KYC states, CTAs, delivery truth, mixed items, and legacy items', async ({ page, consoleGuard }, testInfo) => {
    await setViewport(page, testInfo.project.name);
    await login(page, '09120000013');
    const contextCalls: string[] = [];
    page.on('request', request => {
      if (request.url().includes('/api/orders/items/') && request.url().includes('/kyc-context')) contextCalls.push(request.url());
    });

    await openOrder(page, order.main);
    await expect(page.locator('main')).toContainText('P2F Awaiting Submission');
    await expect(page.locator('main')).toContainText('در انتظار ارسال مدارک');
    await expect(page.locator('main')).toContainText('2F V1 Purchase Policy');
    await expect(page.locator('main')).toContainText('2F V1 purchase-time instructions.');
    await expect(page.locator('main')).toContainText('E2E Identity A');
    await expect(page.locator('main')).toContainText('E2E Identity B');
    await expect(page.locator('main')).toContainText('در انتظار بررسی');
    await expect(page.locator('main')).toContainText('نیازمند اصلاح و ارسال مجدد');
    await expect(page.locator('main')).toContainText('رد نهایی');
    await expect(page.locator('main')).toContainText('P2F-DELIVERED-CODE');
    await expect(page.locator('main')).not.toContainText(heldCanary);
    expect(contextCalls).toEqual([]);

    const submissionCard = page.locator('.st-card').filter({ hasText: 'P2F Awaiting Submission' });
    const submissionCta = submissionCard.locator(`a[href*="${item.awaitingSubmission}"]`);
    await expect(submissionCta).toBeVisible();
    await expect(submissionCta).toBeEnabled();
    await submissionCta.focus();
    await expect(submissionCta).toBeFocused();
    await submissionCta.click();
    await expect(page).toHaveURL(new RegExp(`orderItem=${item.awaitingSubmission}`));
    await expect(page.locator('main')).toContainText('2F V1 Purchase Policy');
    await expect(page.locator('main')).toContainText('2F V1 purchase-time instructions.');
    await expect(page.locator('main')).not.toContainText('2F V2 Purchase Policy');

    await openOrder(page, order.main);
    const rejectedCard = page.locator('.st-card').filter({ hasText: 'P2F Rejected' });
    const rejectedCta = rejectedCard.locator(`a[href*="${item.rejected}"]`);
    await expect(rejectedCta).toBeVisible();
    await rejectedCta.click();
    await expect(page.locator('main')).toContainText('2F V1 Purchase Policy');

    await openOrder(page, order.main);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Awaiting Review' }).locator('a[href*="verification"]')).toHaveCount(0);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Final Rejected' }).locator('a[href*="verification"]')).toHaveCount(0);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Manual Pending' })).toContainText('تحویل دستی');
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Support Pending' })).toContainText('پشتیبانی');

    await openOrder(page, order.v2);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Instant Release Pending' })).toContainText('تأیید شده');
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Instant Release Pending' })).toContainText('تحویل این آیتم در حال انجام است');
    await expect(page.locator('main')).not.toContainText(heldCanary);
    const v2Cta = page.locator('.st-card').filter({ hasText: 'P2F V2 Awaiting Submission' }).locator(`a[href*="${item.v2Submission}"]`);
    await v2Cta.click();
    await expect(page.locator('main')).toContainText('2F V2 Purchase Policy');
    await expect(page.locator('main')).toContainText('2F V2 purchase-time instructions.');

    await openOrder(page, order.mixed);
    await expect(page.locator('.st-card')).toHaveCount(6); // five items plus the order summary card
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Awaiting Submission' }).locator('a[href*="verification"]')).toBeVisible();
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Awaiting Review' }).locator('a[href*="verification"]')).toHaveCount(0);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Manual Pending' })).toContainText('تحویل دستی');
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Support Satisfied' })).toContainText('پشتیبانی');
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Delivered Legacy' }).locator('.st-alert')).toHaveCount(0);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Delivered Legacy' })).not.toContainText('احراز هویت:');

    await openOrder(page, order.main);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Legacy' })).not.toContainText('احراز هویت:');
    await expectRtlAndNoOverflow(page);
    consoleGuard.assertClean();
  });

  test('payment result distinguishes paid from completion and presents the KYC action', async ({ page, consoleGuard }, testInfo) => {
    await setViewport(page, testInfo.project.name);
    await login(page, '09120000013');
    await page.goto(`/payment/result?orderId=${order.payment}&paid=1`, { waitUntil: 'networkidle' });
    await expect(page.locator('h1')).toContainText('پرداخت با موفقیت انجام شد');
    await expect(page.locator('main')).toContainText('تکمیل احراز هویت');
    await expect(page.locator('main')).toContainText('پرداخت موفق بوده');
    await expect(page.locator('main')).not.toContainText(heldCanary);
    await expect(page.locator('main')).not.toContainText('سفارش شما تکمیل شد');
    const action = page.locator(`a[href="/customer/orders/${order.payment}"]`).filter({ hasText: 'تکمیل' });
    await expect(action).toBeVisible();
    await expectRtlAndNoOverflow(page);
    consoleGuard.assertClean();
  });

  test('other customer cannot view an order or obtain its purchase-time KYC context', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Ownership is exercised once against the real customer UI.');
    await setViewport(page, testInfo.project.name);
    await login(page, '09120000014');
    await page.goto(`/customer/orders/${order.main}`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).toContainText('سفارش یافت نشد');
    await expect(page.locator('main')).not.toContainText('2F V1 Purchase Policy');
    await page.goto(`/customer/verification?orderItem=${item.awaitingSubmission}`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).not.toContainText('2F V1 Purchase Policy');
    await expect(page.locator('body')).not.toContainText('یک خطای پیش‌بینی نشده رخ داد');
    consoleGuard.assertClean();
  });

  test('unverified customer receives post-payment KYC information while checkout remains available', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'The checkout transition is exercised once through the real UI.');
    await setViewport(page, testInfo.project.name);
    await login(page, '09120000013');
    await clearCustomerCart(page);
    const before = await myOrderCount(request);
    await addCheckoutGateProduct(request);
    await page.goto('/cart', { waitUntil: 'networkidle' });
    await page.locator('.st-cart-sum button.st-btn--accent').click();
    await expect(page).toHaveURL(/\/checkout/);
    await expect(page.getByTestId('checkout-kyc-information')).toBeVisible();
    await expect(page.getByTestId('checkout-kyc-post-payment-copy')).toContainText('پس از پرداخت');
    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
    expect(await myOrderCount(request)).toBe(before + 1);
    await clearCustomerCart(page);
    consoleGuard.assertClean();
  });
});

async function openOrder(page: import('@playwright/test').Page, id: string) {
  await page.goto(`/customer/orders/${id}`, { waitUntil: 'networkidle' });
  await expect(page.locator('h1 .st-mono')).toBeVisible();
}

async function login(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function myOrderCount(request: import('@playwright/test').APIRequestContext): Promise<number> {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  const loginResponse = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: '09120000013', password } });
  expect(loginResponse.ok()).toBeTruthy();
  const token = ((await loginResponse.json()) as { data: { accessToken: string } }).data.accessToken;
  const response = await request.get(`${apiBaseUrl}/orders`, { headers: { Authorization: `Bearer ${token}` } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: unknown[] }).data.length;
}

async function addCheckoutGateProduct(request: import('@playwright/test').APIRequestContext): Promise<void> {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  const loginResponse = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: '09120000013', password } });
  expect(loginResponse.ok()).toBeTruthy();
  const token = ((await loginResponse.json()) as { data: { accessToken: string } }).data.accessToken;
  const addResponse = await request.post(`${apiBaseUrl}/cart/items`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { productId: '31000000-0000-0000-0000-000000000053', quantity: 1, inputValues: {} }
  });
  expect(addResponse.ok()).toBeTruthy();
}

async function setViewport(page: import('@playwright/test').Page, project: string) {
  await page.setViewportSize(project.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
}

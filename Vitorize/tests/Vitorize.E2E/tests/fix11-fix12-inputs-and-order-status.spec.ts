import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, stockManagedProduct } from './support/app';

type ApiResult<T> = { data: T };
type OrderFixture = { id: string; number: string };
type PaymentStart = { paymentId: string; authority: string | null };

// The deterministic KYC-threshold product seeded by the FIX-09 Phase-2F fixture.
const kycProductId = '31000000-0000-0000-0000-000000000053';
const categoryId = '31000000-0000-0000-0000-000000000001';
const customerMobile = '09120000013';

const PENDING_PAYMENT = 'در انتظار پرداخت';
const PREPARING = 'در حال آماده‌سازی';
const OLD_PROCESSING = 'در حال پردازش';

test.describe('FIX-11 required/optional inputs and FIX-12 order status display @fix11 @fix12', () => {
  test.describe.configure({ timeout: 180_000 });

  test('FIX-11 checkout distinguishes required from optional and enforces the required field', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Storefront coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const product = await createRequiredOptionalProduct(request);
    await loginCustomer(page, customerMobile);
    await clearCart(request, await tokenFor(request, customerMobile));

    // The product page no longer announces the fields, and adding to the cart is not gated on them.
    await page.goto(`/product/${product.slug}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('product-input-summary')).toHaveCount(0);
    await page.getByRole('button', { name: 'افزودن به سبد خرید' }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('سبد خرید');

    // Checkout asks for them, marking required and optional distinctly.
    await page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('checkout-input-required-player_id')).toBeVisible();
    await expect(page.getByTestId('checkout-input-optional-note')).toHaveText('اختیاری');
    await expect(page.getByTestId('checkout-input-required-note')).toHaveCount(0);
    await expect(page.getByTestId('checkout-input-optional-player_id')).toHaveCount(0);

    // Required missing -> payment is refused and the field is marked.
    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/checkout/);
    const required = page.locator('[data-testid=checkout-input-card] [id$="-player_id"]').first();
    await expect(required).toHaveAttribute('aria-invalid', 'true');

    // Required supplied, optional left blank -> the purchase proceeds.
    await required.fill('FIX11-CHECKOUT');
    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);

    consoleGuard.assertClean();
  });

  test('FIX-11 cart keeps the required value, tolerates the blank optional one, and checks out', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Cart/checkout coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const product = await createRequiredOptionalProduct(request);
    const token = await tokenFor(request, customerMobile);
    await clearCart(request, token);
    await expectOk(await request.post(`${apiBaseUrl}/cart/items`, {
      headers: bearer(token),
      data: { productId: product.id, quantity: 1, inputValues: { player_id: 'FIX11-CART' } }
    }));
    await loginCustomer(page, customerMobile);

    await page.goto('/cart', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-cart-item').filter({ hasText: product.title })).toBeVisible();
    // The cart carries no product-input editors; it only shows the line itself.
    await expect(page.locator('.st-dynamic-form')).toHaveCount(0);

    await page.locator('.st-cart-sum button.st-btn--accent').click();
    await expect(page).toHaveURL(/\/checkout/);
    // Checkout seeds the field from the value already stored on the line.
    await expect(page.locator('[data-testid=checkout-input-card] [id$="-player_id"]').first())
      .toHaveValue('FIX11-CART');
    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);

    const orderId = new URL(page.url()).searchParams.get('orderId')!;
    const detail = await orderDetails(request, token, `/orders/${orderId}`);
    expect(detail.status).toBe(2);
    expect(detail.items[0].inputValues).toContainEqual(expect.objectContaining({ fieldKey: 'player_id', value: 'FIX11-CART' }));
    expect(detail.items[0].inputValues).toContainEqual(expect.objectContaining({ fieldKey: 'note', value: null }));

    consoleGuard.assertClean();
  });

  test('FIX-12 an unpaid order reads "در انتظار پرداخت" for the customer and the admin', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Status terminology is asserted once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const token = await tokenFor(request, customerMobile);
    const order = await checkout(request, token, kycProductId, 1);
    expect((await orderDetails(request, token, `/orders/${order.id}`)).status).toBe(1);

    await loginCustomer(page, customerMobile);
    await page.goto(`/customer/orders/${order.id}`, { waitUntil: 'networkidle' });
    await expect(orderStatusBadge(page)).toHaveText(PENDING_PAYMENT);
    await page.goto('/customer/orders', { waitUntil: 'networkidle' });
    await expect(customerOrderStatusCell(page, order.number)).toHaveText(PENDING_PAYMENT);

    await loginAs('SuperAdmin');
    await openAdminOrderRow(page, order.number);
    await expect(adminOrderStatusCell(page, order.number)).toContainText(PENDING_PAYMENT);

    consoleGuard.assertClean();
  });

  test('FIX-12 a paid order reads "در حال آماده‌سازی" everywhere while KYC stays visible', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Status terminology is asserted once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const token = await tokenFor(request, customerMobile);
    const order = await checkoutAndPay(request, token, kycProductId, 1);

    // The persisted enum must remain the existing numeric Processing value.
    const detail = await orderDetails(request, token, `/orders/${order.id}`);
    expect(detail.status).toBe(2);

    await loginCustomer(page, customerMobile);

    // PaymentResult must not claim the order is still awaiting payment.
    await page.goto(`/payment/result?orderId=${order.id}&paid=1`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).not.toContainText(PENDING_PAYMENT);

    await page.goto(`/customer/orders/${order.id}`, { waitUntil: 'networkidle' });
    await expect(orderStatusBadge(page)).toHaveText(PREPARING);
    await expect(page.locator('main')).not.toContainText(OLD_PROCESSING);
    // Item-level truth is preserved next to the order-level "being prepared" badge.
    await expect(page.locator('main')).toContainText('احراز هویت');
    await expect(page.getByRole('link', { name: 'تکمیل احراز هویت', exact: true })).toBeVisible();

    await page.goto('/customer/orders', { waitUntil: 'networkidle' });
    await expect(customerOrderStatusCell(page, order.number)).toHaveText(PREPARING);
    await page.goto('/customer/dashboard', { waitUntil: 'networkidle' });
    await expect(page.locator('main')).not.toContainText(OLD_PROCESSING);

    await loginAs('SuperAdmin');
    await page.goto('/admin/orders', { waitUntil: 'networkidle' });
    await expect(page.locator('.vz-stats')).toContainText(PREPARING);
    await expect(page.locator('.vz-stats')).not.toContainText(OLD_PROCESSING);
    await expect(page.locator('.vz-pills')).toContainText(PREPARING);
    await expect(page.locator('.vz-pills')).not.toContainText(OLD_PROCESSING);
    await openAdminOrderRow(page, order.number);
    await expect(adminOrderStatusCell(page, order.number)).toContainText(PREPARING);

    // Admin order details: badge and stepper use the same terminology.
    const row = page.locator('tbody tr').filter({ hasText: order.number });
    await row.locator('.vz-ctx__trigger').click();
    await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
    const dialog = page.getByRole('dialog').filter({ hasText: order.number });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(PREPARING);
    await expect(dialog).toContainText('آماده‌سازی');
    await expect(dialog).not.toContainText(OLD_PROCESSING);

    consoleGuard.assertClean();
  });

  test('mobile 390x844 keeps the indicators and the status badge readable without overflow', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile smoke is sufficient.');
    await page.setViewportSize({ width: 390, height: 844 });
    const product = await createRequiredOptionalProduct(request);
    const token = await tokenFor(request, customerMobile);
    await clearCart(request, token);
    const order = await checkoutAndPay(request, token, kycProductId, 1);
    await loginCustomer(page, customerMobile);

    await page.goto(`/product/${product.slug}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('product-input-summary')).toContainText('۱ مورد اطلاعات الزامی');
    await page.getByRole('button', { name: 'افزودن به سبد خرید' }).click();
    await expect(page.getByTestId('product-input-required-player_id')).toBeVisible();
    await expect(page.getByTestId('product-input-optional-note')).toBeVisible();
    await expectNoOverflow(page);

    await page.goto(`/customer/orders/${order.id}`, { waitUntil: 'networkidle' });
    await expect(orderStatusBadge(page)).toHaveText(PREPARING);
    await expectNoOverflow(page);

    consoleGuard.assertClean();
  });
});

async function createRequiredOptionalProduct(request: import('@playwright/test').APIRequestContext) {
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const suffix = `${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`;
  const slug = `fix11-required-optional-${suffix}`;
  const title = `FIX11 Required Optional ${suffix}`;
  const created = await request.post(`${apiBaseUrl}/admin/products`, {
    headers: bearer(admin),
    data: {
      categoryId, title, slug, productType: 1, deliveryType: 2, basePrice: 5_000, currencyType: 2,
      minOrderQuantity: 1, isActive: true,
      inputFields: [
        {
          key: 'player_id', label: 'شناسه بازیکن', fieldType: 1, isRequired: true,
          minLength: 3, maxLength: 50, displayStage: 1, sortOrder: 10, isActive: true
        },
        {
          key: 'note', label: 'یادداشت', fieldType: 1, isRequired: false,
          maxLength: 200, displayStage: 1, sortOrder: 20, isActive: true
        }
      ]
    }
  });
  await expectOk(created);
  const id = (await created.json() as ApiResult<{ id: string }>).data.id;
  await stockManagedProduct(request, admin, id);
  return { id, slug, title };
}

async function checkoutAndPay(request: import('@playwright/test').APIRequestContext, token: string, productId: string, quantity: number) {
  const order = await checkout(request, token, productId, quantity);
  const started = await request.post(`${apiBaseUrl}/payments/start/${order.id}`, { headers: bearer(token) });
  await expectOk(started);
  const payment = (await started.json() as ApiResult<PaymentStart>).data;
  await expectOk(await request.post(`${apiBaseUrl}/payments/mock/verify/${payment.paymentId}`, { headers: bearer(token) }));
  return order;
}

async function checkout(request: import('@playwright/test').APIRequestContext, token: string, productId: string, quantity: number): Promise<OrderFixture> {
  await clearCart(request, token);
  await expectOk(await request.post(`${apiBaseUrl}/cart/items`, { headers: bearer(token), data: { productId, quantity } }));
  const response = await request.post(`${apiBaseUrl}/checkout`, {
    headers: { ...bearer(token), 'Idempotency-Key': `fix11-fix12-${Date.now()}-${Math.random()}` }, data: {}
  });
  await expectOk(response);
  const data = (await response.json() as ApiResult<{ orderId: string; orderNumber: string }>).data;
  return { id: data.orderId, number: data.orderNumber };
}

async function clearCart(request: import('@playwright/test').APIRequestContext, token: string) {
  await expectOk(await request.delete(`${apiBaseUrl}/cart/clear`, { headers: bearer(token) }));
}

type OrderDetail = { status: number; items: Array<{ inputValues: Array<{ fieldKey: string; value: string | null }> }> };

async function orderDetails(request: import('@playwright/test').APIRequestContext, token: string, route: string): Promise<OrderDetail> {
  const response = await request.get(`${apiBaseUrl}${route}`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<OrderDetail>).data;
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

// The order-level status badge sits next to the payment badge in the header; index 1 is the order one.
function orderStatusBadge(page: import('@playwright/test').Page) {
  return page.locator('.st-spread').first().locator('.st-badge').nth(1);
}

// Customer orders table: number | items | amount | payment | status | date | actions
function customerOrderStatusCell(page: import('@playwright/test').Page, orderNumber: string) {
  return page.locator('tbody tr').filter({ hasText: orderNumber }).locator('td').nth(4);
}

// Admin orders table: select | number | customer | amount | payment | status | date | actions
function adminOrderStatusCell(page: import('@playwright/test').Page, orderNumber: string) {
  return page.locator('tbody tr').filter({ hasText: orderNumber }).locator('td').nth(5);
}

async function openAdminOrderRow(page: import('@playwright/test').Page, orderNumber: string) {
  await page.goto('/admin/orders', { waitUntil: 'networkidle' });
  await page.locator('#order-search').fill(orderNumber);
  await expect(page.locator('tbody tr').filter({ hasText: orderNumber })).toHaveCount(1);
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

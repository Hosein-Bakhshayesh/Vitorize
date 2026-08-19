import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, stockManagedProduct } from './support/app';

type ApiResult<T> = { data: T };
type Cart = { subtotalAmount: number; discountAmount: number; vatEnabled: boolean; vatRatePercent: number; vatAmount: number; finalAmount: number };
type CouponPreview = { discountAmount: number; vatEnabled: boolean; vatAmount: number; vatTaxableAmount: number; finalAmount: number };
type Checkout = { orderId: string; orderNumber: string; finalAmount: number; vatAmount: number; vatRatePercent: number };

const customerMobile = '09120000013';
const categoryId = '31000000-0000-0000-0000-000000000001';
const couponCode = 'E2E10';           // deterministic 10% coupon from seed-e2e.sql
const price = 1_000_000;              // clean base so VAT arithmetic is exact in the UI

const VAT_LABEL = 'مالیات بر ارزش افزوده';

test.describe('FIX-13 VAT @fix13', () => {
  test.describe.configure({ timeout: 240_000 });

  // VAT is a global financial setting; leave it disabled so later suites see pre-FIX-13 behaviour.
  test.afterEach(async ({ request }) => { await setVat(request, { enabled: false, rate: 0, mode: 'BeforeDiscount' }); });

  test('Admin Settings exposes VAT, persists it, and rejects an invalid rate', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: false, rate: 0, mode: 'BeforeDiscount' });
    await loginAs('SuperAdmin');

    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    await page.getByRole('button', { name: 'پرداخت' }).click();
    await expect(page.locator('.vz-setgroup__title').filter({ hasText: VAT_LABEL })).toBeVisible();

    // The bool control is the styled .vz-switch; its raw checkbox is visually hidden by design.
    const enabledSwitch = settingField(page, 'VatEnabled').locator('label.vz-switch');
    const enabled = enabledSwitch.locator('input[type="checkbox"]');
    const rate = settingField(page, 'VatRatePercent').locator('input[type="number"]');
    const mode = page.getByTestId('setting-vat-calculation-mode');
    await expect(enabledSwitch).toBeVisible();
    await expect(rate).toBeVisible();
    await expect(mode).toBeVisible();
    // Only Persian labels are surfaced; the invariant enum string stays in the value attribute.
    await expect(mode).toContainText('محاسبه مالیات قبل از اعمال تخفیف');
    await expect(mode).toContainText('محاسبه مالیات بعد از اعمال تخفیف');

    // Toggle through the visible label; the underlying input is visually hidden by .vz-switch.
    if (!(await enabled.isChecked())) await enabledSwitch.click();
    await expect(enabled).toBeChecked();
    await rate.fill('10');
    await rate.blur();
    await mode.selectOption('BeforeDiscount');
    await page.getByRole('button', { name: 'ذخیره همه تغییرات' }).click();
    await expect(page.locator('.vz-toast').last()).toBeVisible();

    await page.reload({ waitUntil: 'networkidle' });
    await page.getByRole('button', { name: 'پرداخت' }).click();
    await expect(settingField(page, 'VatEnabled').locator('label.vz-switch')).toContainText('فعال');
    await expect(settingField(page, 'VatEnabled').locator('input[type="checkbox"]')).toBeChecked();
    await expect(settingField(page, 'VatRatePercent').locator('input[type="number"]')).toHaveValue('10');
    await expect(page.getByTestId('setting-vat-calculation-mode')).toHaveValue('BeforeDiscount');

    await page.getByTestId('setting-vat-calculation-mode').selectOption('AfterDiscount');
    await page.getByRole('button', { name: 'ذخیره همه تغییرات' }).click();
    await expect(page.locator('.vz-toast').last()).toBeVisible();
    await page.reload({ waitUntil: 'networkidle' });
    await page.getByRole('button', { name: 'پرداخت' }).click();
    await expect(page.getByTestId('setting-vat-calculation-mode')).toHaveValue('AfterDiscount');

    // The server refuses an out-of-range rate through the same validated endpoint the UI uses.
    const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
    for (const invalid of ['-1', '101', 'abc']) {
      const rejected = await request.post(`${apiBaseUrl}/admin/settings`, {
        headers: bearer(admin),
        data: { key: 'VatRatePercent', value: invalid, groupName: 'Tax', valueType: 'decimal' }
      });
      expect(rejected.status(), `rate ${invalid} must be rejected`).toBe(400);
    }
    consoleGuard.assertClean();
  });

  test('Cart shows no VAT row while VAT is disabled', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Storefront coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: false, rate: 0, mode: 'BeforeDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    await stockCart(request, token, product.id);
    await loginCustomer(page, customerMobile);

    await page.goto('/cart', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cart-vat-row')).toHaveCount(0);
    await expect(page.getByTestId('cart-payable')).toContainText(toman(price));
    consoleGuard.assertClean();
  });

  test('Cart and Checkout render the BeforeDiscount decomposition from the server', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Storefront coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: true, rate: 10, mode: 'BeforeDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    await stockCart(request, token, product.id);

    // No-coupon preview: 1,000,000 + 10% = 1,100,000.
    const cart = await getCart(request, token);
    expect(cart.vatEnabled).toBe(true);
    expect(cart.vatAmount).toBe(100_000);
    expect(cart.finalAmount).toBe(1_100_000);

    await loginCustomer(page, customerMobile);
    await page.goto('/cart', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cart-vat-row')).toContainText(VAT_LABEL);
    await expect(page.getByTestId('cart-vat-row')).toContainText('۱۰٪');
    await expect(page.getByTestId('cart-vat-row')).toContainText(toman(100_000));
    await expect(page.getByTestId('cart-payable')).toContainText(toman(1_100_000));

    // With the 10% coupon: VAT still taxes the full subtotal, so payable returns to 1,000,000.
    const preview = await validateCoupon(request, token, cart.subtotalAmount);
    expect(preview.vatTaxableAmount).toBe(1_000_000);
    expect(preview.vatAmount).toBe(100_000);
    expect(preview.finalAmount).toBe(1_000_000);

    await page.locator('.st-promo input').fill(couponCode);
    await page.locator('.st-promo button').click();
    await expect(page.locator('.st-promo__msg.ok')).toBeVisible();
    await expect(page.getByTestId('cart-vat-row')).toContainText(toman(100_000));
    await expect(page.getByTestId('cart-payable')).toContainText(toman(1_000_000));

    // Checkout must show the same decomposition before any payment is initiated.
    await page.locator('.st-cart-sum button.st-btn--accent').click();
    await expect(page).toHaveURL(/\/checkout/);
    await expect(page.getByTestId('checkout-vat-row')).toContainText(toman(100_000));
    await expect(page.getByTestId('checkout-payable')).toContainText(toman(1_000_000));
    consoleGuard.assertClean();
  });

  test('AfterDiscount taxes only the discounted amount', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Storefront coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: true, rate: 10, mode: 'AfterDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    await stockCart(request, token, product.id);

    const cart = await getCart(request, token);
    const preview = await validateCoupon(request, token, cart.subtotalAmount);
    expect(preview.vatTaxableAmount).toBe(900_000, 'taxable base drops to the discounted amount');
    expect(preview.vatAmount).toBe(90_000);
    expect(preview.finalAmount).toBe(990_000);

    await loginCustomer(page, customerMobile);
    await page.goto('/cart', { waitUntil: 'networkidle' });
    await page.locator('.st-promo input').fill(couponCode);
    await page.locator('.st-promo button').click();
    await expect(page.locator('.st-promo__msg.ok')).toBeVisible();
    await expect(page.getByTestId('cart-vat-row')).toContainText(toman(90_000));
    await expect(page.getByTestId('cart-payable')).toContainText(toman(990_000));
    consoleGuard.assertClean();
  });

  test('Order history and Admin order details show the immutable purchase-time snapshot', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Order coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: true, rate: 10, mode: 'BeforeDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    const order = await checkout(request, token, product.id);
    expect(order.vatRatePercent).toBe(10);
    expect(order.vatAmount).toBe(100_000);
    expect(order.finalAmount).toBe(1_100_000);

    await loginCustomer(page, customerMobile);
    await page.goto(`/customer/orders/${order.orderId}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('order-vat-row')).toContainText('۱۰٪');
    await expect(page.getByTestId('order-vat-row')).toContainText(toman(100_000));

    // Change the configuration; the historical order must not move.
    await setVat(request, { enabled: true, rate: 25, mode: 'AfterDiscount' });
    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.getByTestId('order-vat-row')).toContainText('۱۰٪');
    await expect(page.getByTestId('order-vat-row')).toContainText(toman(100_000));
    await expect(page.locator('.st-sumrow.total')).toContainText(toman(1_100_000));

    // Disabling VAT entirely also leaves it alone.
    await setVat(request, { enabled: false, rate: 0, mode: 'BeforeDiscount' });
    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.getByTestId('order-vat-row')).toContainText(toman(100_000));

    await loginAs('SuperAdmin');
    await page.goto('/admin/orders', { waitUntil: 'networkidle' });
    await page.locator('#order-search').fill(order.orderNumber);
    const row = page.locator('tbody tr').filter({ hasText: order.orderNumber });
    await expect(row).toHaveCount(1);
    await row.locator('.vz-ctx__trigger').click();
    await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
    const dialog = page.getByRole('dialog').filter({ hasText: order.orderNumber });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByTestId('admin-order-vat')).toContainText(toman(100_000));
    await expect(dialog.getByTestId('admin-order-vat-basis')).toHaveText('قبل از اعمال تخفیف');
    // The internal enum name is never shown to an operator.
    await expect(dialog).not.toContainText('BeforeDiscount');
    consoleGuard.assertClean();
  });

  test('a pre-VAT order shows no VAT row and keeps its original total', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Historical coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await setVat(request, { enabled: false, rate: 0, mode: 'BeforeDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    const legacy = await checkout(request, token, product.id);
    expect(legacy.finalAmount).toBe(price);

    // Turning VAT on afterwards must not retro-tax it.
    await setVat(request, { enabled: true, rate: 25, mode: 'AfterDiscount' });
    await loginCustomer(page, customerMobile);
    await page.goto(`/customer/orders/${legacy.orderId}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('order-vat-row')).toHaveCount(0);
    await expect(page.locator('.st-sumrow.total')).toContainText(toman(price));
    consoleGuard.assertClean();
  });

  test('mobile 390x844 keeps the VAT line and payable readable without overflow', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile smoke is sufficient.');
    await page.setViewportSize({ width: 390, height: 844 });
    await setVat(request, { enabled: true, rate: 10, mode: 'BeforeDiscount' });
    const token = await tokenFor(request, customerMobile);
    const product = await createProduct(request);
    await stockCart(request, token, product.id);
    await loginCustomer(page, customerMobile);

    await page.goto('/cart', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cart-vat-row')).toBeVisible();
    await expect(page.getByTestId('cart-vat-row')).toContainText(toman(100_000));
    await expect(page.getByTestId('cart-payable')).toBeVisible();
    await expect(page.getByTestId('cart-payable')).toContainText(toman(1_100_000));
    await expectNoOverflow(page);
    consoleGuard.assertClean();
  });
});

/** Mirrors AdminUiHelper.MoneyFa: invariant thousands grouping, Persian digits, Toman unit. */
function toman(value: number): string {
  const grouped = value.toLocaleString('en-US');
  return `${grouped.replace(/[0-9]/g, d => String.fromCharCode(d.charCodeAt(0) - 48 + 0x06f0))} تومان`;
}

function settingField(page: import('@playwright/test').Page, key: string) {
  return page.locator('.vz-setfield').filter({ has: page.locator('.vz-setfield__key', { hasText: new RegExp(`^${key}$`) }) });
}

async function setVat(
  request: import('@playwright/test').APIRequestContext,
  vat: { enabled: boolean; rate: number; mode: 'BeforeDiscount' | 'AfterDiscount' }
) {
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const values: Array<[string, string, string]> = [
    ['VatEnabled', vat.enabled ? 'true' : 'false', 'bool'],
    ['VatRatePercent', String(vat.rate), 'decimal'],
    ['VatCalculationMode', vat.mode, 'vatmode']
  ];
  for (const [key, value, valueType] of values) {
    await expectOk(await request.post(`${apiBaseUrl}/admin/settings`, {
      headers: bearer(admin), data: { key, value, groupName: 'Tax', valueType }
    }));
  }
}

async function createProduct(request: import('@playwright/test').APIRequestContext) {
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const suffix = `${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`;
  const created = await request.post(`${apiBaseUrl}/admin/products`, {
    headers: bearer(admin),
    data: {
      categoryId, title: `FIX13 VAT ${suffix}`, slug: `fix13-vat-${suffix}`,
      productType: 1, deliveryType: 2, basePrice: price, currencyType: 2,
      minOrderQuantity: 1, isActive: true
    }
  });
  await expectOk(created);
  const id = (await created.json() as ApiResult<{ id: string }>).data.id;
  await stockManagedProduct(request, admin, id);
  return { id };
}

async function stockCart(request: import('@playwright/test').APIRequestContext, token: string, productId: string) {
  await expectOk(await request.delete(`${apiBaseUrl}/cart/clear`, { headers: bearer(token) }));
  await expectOk(await request.post(`${apiBaseUrl}/cart/items`, { headers: bearer(token), data: { productId, quantity: 1 } }));
}

async function getCart(request: import('@playwright/test').APIRequestContext, token: string): Promise<Cart> {
  const response = await request.get(`${apiBaseUrl}/cart`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<Cart>).data;
}

async function validateCoupon(request: import('@playwright/test').APIRequestContext, token: string, orderAmount: number): Promise<CouponPreview> {
  const response = await request.post(`${apiBaseUrl}/coupons/validate`, {
    headers: bearer(token), data: { code: couponCode, orderAmount }
  });
  await expectOk(response);
  return (await response.json() as ApiResult<CouponPreview>).data;
}

async function checkout(request: import('@playwright/test').APIRequestContext, token: string, productId: string): Promise<Checkout> {
  await stockCart(request, token, productId);
  const response = await request.post(`${apiBaseUrl}/checkout`, {
    headers: { ...bearer(token), 'Idempotency-Key': `fix13-${Date.now()}-${Math.random()}` }, data: {}
  });
  await expectOk(response);
  return (await response.json() as ApiResult<Checkout>).data;
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

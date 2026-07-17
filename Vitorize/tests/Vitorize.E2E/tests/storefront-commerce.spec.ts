import { expect, test, type Page } from '@playwright/test';
import { expectRtlAndNoOverflow, loginAdmin, monitorBrowser, registerCustomer, uniqueCustomer } from './support/app';

const productUrl = '/product/e2e-seo-product';
const instantProductId = '31000000-0000-0000-0000-000000000011';

async function addConfiguredProduct(page: Page, email: string): Promise<void> {
  await page.goto(productUrl, { waitUntil: 'networkidle' });
  await expect(page.locator('.st-vcard.active')).toContainText('E2E Premium Variant');
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('#product-input-account_email')).toBeVisible();
  await page.locator('#product-input-account_email').fill(email);
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success')).toBeVisible();
}

test('storefront navigation, search, filters and sorting render seeded catalog data', async ({ page }) => {
  await page.goto('/', { waitUntil: 'networkidle' });
  await expect(page.locator('main')).toBeVisible();
  await expectRtlAndNoOverflow(page);

  await page.goto('/categories', { waitUntil: 'networkidle' });
  await expect(page.locator('main')).toContainText('E2E Category');
  await page.goto('/category/e2e-category', { waitUntil: 'networkidle' });
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');
  await page.goto('/brand/e2e-brand', { waitUntil: 'networkidle' });
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');

  await page.goto('/shop?q=E2E%20Dynamic', { waitUntil: 'networkidle' });
  const browser = monitorBrowser(page);
  await expect(page.locator('.st-lgrid')).toContainText('E2E Dynamic Product');
  await page.locator('.st-catpill').filter({ hasText: 'E2E Category' }).click();
  await expect(page.locator('.st-lgrid')).toContainText('E2E Dynamic Product');
  await page.locator('.st-sort__btn').click();
  await expect(page.locator('.st-sort__menu')).toBeVisible();
  await page.locator('.st-sort__opt').last().click();
  await expect(page.locator('.st-sort__menu')).toBeHidden();
  await expectRtlAndNoOverflow(page);
  browser.assertClean();
});

test('product page renders variant, gallery, feature card, rich HTML, related product and dynamic form validation', async ({ page }) => {
  const browser = monitorBrowser(page);
  await page.goto(productUrl, { waitUntil: 'networkidle' });
  await expect(page.getByRole('heading', { name: 'E2E Dynamic Product' })).toBeVisible();
  await expect(page.locator('.st-vcard.active')).toContainText('E2E Premium Variant');
  await expect(page.locator('.st-feature-card')).toContainText('Platform');
  await expect(page.locator('.st-feature-card')).toContainText('Browser');
  await expect(page.locator('.st-rich-content h2')).toHaveText('Rich E2E Description');
  await expect(page.locator('.st-gal__main img')).toHaveAttribute('alt', /E2E/);
  await expect(page.locator('.st-section')).toContainText('E2E Related Product');

  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('#product-input-account_email')).toBeVisible();
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.error')).toBeVisible();
  await expect(page.locator('#product-input-account_email')).toBeVisible();
  await expectRtlAndNoOverflow(page);
  browser.assertClean();
});

test('cart merges identical inputs, separates different inputs, updates quantity, edits values and applies coupon', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Cart Customer'));
  await addConfiguredProduct(page, 'same@example.test');
  await addConfiguredProduct(page, 'same@example.test');
  await addConfiguredProduct(page, 'different@example.test');

  await page.goto('/cart', { waitUntil: 'networkidle' });
  const itemCards = page.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' });
  await expect(itemCards).toHaveCount(2);
  await expect(itemCards.filter({ hasText: 'same@example.test' }).locator('.st-qty')).toContainText('۲');
  await expect(itemCards.filter({ hasText: 'different@example.test' })).toBeVisible();

  const different = itemCards.filter({ hasText: 'different@example.test' });
  await different.locator('.st-qty button').last().click();
  await expect(different.locator('.st-qty')).toContainText('۲');
  await different.getByRole('button', { name: /ویرایش/ }).click();
  await page.locator('.vz-dialog input.st-input').fill('edited@example.test');
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.st-stack > .st-card').filter({ hasText: 'edited@example.test' })).toBeVisible();
  await expectRtlAndNoOverflow(page);

  await page.locator('.st-promo input').fill('E2E10');
  const couponButton = page.locator('.st-promo button');
  await couponButton.scrollIntoViewIfNeeded();
  const couponGeometry = await couponButton.evaluate(element => {
    const rect = element.getBoundingClientRect();
    const summary = element.closest('.st-cart-sum');
    return {
      top: rect.top, right: rect.right, bottom: rect.bottom, left: rect.left,
      viewportWidth: window.innerWidth, viewportHeight: window.innerHeight,
      summaryPosition: summary ? getComputedStyle(summary).position : null
    };
  });
  expect(couponGeometry.top, JSON.stringify(couponGeometry)).toBeGreaterThanOrEqual(-1);
  expect(couponGeometry.left, JSON.stringify(couponGeometry)).toBeGreaterThanOrEqual(-1);
  expect(couponGeometry.right, JSON.stringify(couponGeometry)).toBeLessThanOrEqual(couponGeometry.viewportWidth + 1);
  expect(couponGeometry.bottom, JSON.stringify(couponGeometry)).toBeLessThanOrEqual(couponGeometry.viewportHeight + 1);
  await couponButton.click();
  await expect(page.locator('.st-promo__msg.ok')).toBeVisible();
  await expect(page.locator('.st-cart-sum')).toContainText('E2E10');

  await itemCards.filter({ hasText: 'same@example.test' }).getByRole('button', { name: /حذف/ }).click();
  await expect(page.locator('.st-stack > .st-card').filter({ hasText: 'same@example.test' })).toHaveCount(0);
});

test('gateway checkout completes through fake payment and creates an order visible to the customer', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Checkout Customer'));
  await addConfiguredProduct(page, 'checkout@example.test');
  await page.goto('/checkout', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-paycard.active')).toBeVisible();
  await page.locator('button.st-btn--accent').click();

  await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
  await expect(page.locator('main')).toContainText(/موفق|تکمیل/);
  const orderLink = page.locator('a[href*="/customer/orders/"]').first();
  await expect(orderLink).toBeVisible();
  await orderLink.click();
  await expect(page).toHaveURL(/\/customer\/orders\/[0-9a-f-]+/i);
  await expect(page.locator('main')).toContainText('checkout@example.test');
});

test('wallet top-up funds a wallet checkout and records the resulting debit', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Wallet Checkout Customer'));
  await page.goto('/customer/wallet', { waitUntil: 'networkidle' });
  await page.locator('input[type="number"]').fill('500000');
  await page.locator('.st-card').filter({ has: page.locator('input[type="number"]') }).locator('button.st-btn--primary').click();
  await expect(page.locator('.vz-toast.success')).toBeVisible();
  await expect(page.locator('.st-table tbody tr')).toHaveCount(1);

  await addConfiguredProduct(page, 'wallet-checkout@example.test');
  await page.goto('/checkout', { waitUntil: 'networkidle' });
  const walletMethod = page.locator('.st-paycard').nth(1);
  await expect(walletMethod).not.toHaveClass(/disabled/);
  await walletMethod.click();
  await expect(walletMethod).toHaveClass(/active/);
  await page.locator('button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);

  await page.goto('/customer/wallet', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-table tbody tr')).toHaveCount(2);
});

test('failed payment result keeps recovery actions available', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Failed Payment Customer'));
  const orderId = '31000000-0000-0000-0000-000000000099';
  await page.goto(`/payment/result?orderId=${orderId}&paid=0`, { waitUntil: 'networkidle' });
  await expect(page.locator('.st-state__ic')).toBeVisible();
  await expect(page.locator('a.st-btn--primary[href="/cart"]')).toBeVisible();
  await expect(page.locator(`a[href="/customer/orders/${orderId}"]`)).toBeVisible();
});

test('admin manually delivers a paid item and the customer sees the audited content', async ({ page, browser }) => {
  const customer = await registerCustomer(page, uniqueCustomer('Manual Delivery Customer'));
  await addConfiguredProduct(page, 'manual-delivery@example.test');
  await page.goto('/checkout', { waitUntil: 'networkidle' });
  await page.locator('button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
  const orderId = new URL(page.url()).searchParams.get('orderId');
  expect(orderId).toMatch(/^[0-9a-f-]{36}$/i);
  await page.locator('a[href*="/customer/orders/"]').first().click();
  const orderNumber = (await page.locator('h1 .st-mono').innerText()).trim();

  const adminContext = await browser.newContext({
    baseURL: new URL(page.url()).origin,
    locale: 'fa-IR',
    timezoneId: 'Asia/Tehran',
    colorScheme: 'light'
  });
  const adminPage = await adminContext.newPage();
  await loginAdmin(adminPage);
  await adminPage.goto('/admin/orders', { waitUntil: 'networkidle' });
  await adminPage.locator('#order-search').fill(orderNumber);
  const row = adminPage.locator('tbody tr').filter({ hasText: orderNumber });
  await expect(row).toHaveCount(1);
  await row.locator('.vz-ctx__trigger').click();
  await adminPage.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
  const details = adminPage.getByRole('dialog').filter({ hasText: orderNumber });
  await expect(details).toBeVisible();
  await details.locator('.vz-manual-delivery').click();

  const content = `E2E delivery ${Date.now()}`;
  const deliveryDialog = adminPage.getByRole('dialog').filter({ has: adminPage.locator('#manual-delivery-content') });
  await deliveryDialog.locator('#manual-delivery-content').fill(content);
  await deliveryDialog.locator('button.vz-btn--primary').click();
  await expect(deliveryDialog).toBeHidden();
  await expect(adminPage.locator('.vz-toast.success')).toBeVisible();
  await adminContext.close();

  await page.goto(`/customer/orders/${orderId}`, { waitUntil: 'networkidle' });
  await expect(page.locator('main')).toContainText(content);
});

test('an imported instant gift code is delivered into the customer code library', async ({ page, browser }) => {
  await registerCustomer(page, uniqueCustomer('Gift Delivery Customer'));
  const baseURL = new URL(page.url()).origin;
  const code = `E2E-GIFT-${Date.now()}`;

  const adminContext = await browser.newContext({ baseURL, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
  const adminPage = await adminContext.newPage();
  await loginAdmin(adminPage);
  await adminPage.goto('/admin/gift-codes', { waitUntil: 'networkidle' });
  await adminPage.locator('.vz-page-head button.vz-btn--primary').click();
  const importDialog = adminPage.getByRole('dialog');
  await importDialog.locator('select.vz-select').selectOption(instantProductId);
  await importDialog.locator('input.vz-input').nth(0).fill(`E2E Delivery Batch ${Date.now()}`);
  await importDialog.locator('textarea.vz-textarea').fill(code);
  await importDialog.locator('button.vz-btn--primary').click();
  await expect(importDialog).toBeHidden();
  await adminContext.close();

  await page.goto('/product/e2e-related-product', { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success')).toBeVisible();
  await page.goto('/checkout', { waitUntil: 'networkidle' });
  await page.locator('button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);

  await page.goto('/customer/gift-codes', { waitUntil: 'networkidle' });
  const card = page.locator('.st-codecard').filter({ hasText: 'E2E Related Product' }).first();
  await expect(card).toBeVisible();
  await card.locator('.st-codecard__actions button').first().click();
  await expect(card.locator('.st-codecard__code')).toHaveText(code);
});

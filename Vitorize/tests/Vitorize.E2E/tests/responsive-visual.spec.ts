import { expect, test, clearCustomerCart, registerCustomer, uniqueCustomer } from '../framework/fixtures';
import type { Locator, Page, TestInfo } from '@playwright/test';
import { auditResponsivePage } from '../framework/responsive';

const visualProjects = new Set([
  'phone-320-light', 'phone-390-dark', 'tablet-768-light', 'desktop-1440-dark'
]);

const commonDynamic = (page: Page) => page.locator('time,.st-mono,.vz-mono,.st-avatar');

async function ready(page: Page) {
  const splash = page.locator('#vz-initial-loader');
  if (await splash.count()) await splash.waitFor({ state: 'hidden', timeout: 15_000 });
  await expect(page.locator('.vz-spinner:visible')).toHaveCount(0, { timeout: 30_000 });
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
}

async function capture(page: Page, name: string, masks: Locator[] = []) {
  await ready(page);
  await expect(page).toHaveScreenshot(name, {
    animations: 'disabled',
    caret: 'hide',
    mask: [commonDynamic(page), ...masks],
    maskColor: '#94a3b8',
    maxDiffPixelRatio: 0.02
  });
}

async function visitAndCapture(page: Page, path: string, name: string, masks: Locator[] = []) {
  await page.goto(path, { waitUntil: 'networkidle' });
  await capture(page, name, masks);
}

test.describe('@responsive @visual @release focused responsive visual baselines', () => {
  test.describe.configure({ timeout: 360_000 });
  test.beforeEach(({}, testInfo: TestInfo) => test.skip(
    !visualProjects.has(testInfo.project.name),
    'Visual baselines cover the representative phone, tablet and desktop matrix.'
  ));

  test('public purchase surfaces match responsive baselines @mobile @tablet @desktop', async ({ page, loginAs }) => {
    await visitAndCapture(page, '/', 'responsive-public-home.png', [page.locator('.st-grid')]);
    await visitAndCapture(page, '/shop?q=E2E%20Dynamic&sort=price-desc', 'responsive-public-products.png');
    await visitAndCapture(page, '/product/e2e-seo-product', 'responsive-public-product-detail.png');

    await loginAs('Customer');
    await clearCustomerCart(page);
    await visitAndCapture(page, '/cart', 'responsive-public-cart-empty.png');
    await visitAndCapture(page, '/checkout', 'responsive-public-checkout-empty.png');
  });

  test('customer orders, details, gift codes and ticket thread match responsive baselines @mobile @tablet', async ({ page, storefront, customerTickets }) => {
    await registerCustomer(page, uniqueCustomer('Responsive Visual Customer With Long Name'));
    await storefront.addToCart('e2e-seo-product', { account_email: 'responsive-visual-long-address@example.test' });
    const orderId = await storefront.checkoutAndPay();
    const subject = 'Responsive visual support ticket with a deliberately long subject';
    const ticketId = await customerTickets.createForOrder(orderId, {
      subject,
      priority: '3',
      message: 'A deliberately long deterministic support message that verifies wrapping, readable line-height, and reachable reply controls on narrow RTL screens.'
    });

    await visitAndCapture(page, '/customer/orders', 'responsive-customer-orders.png');
    await visitAndCapture(page, `/customer/orders/${orderId}`, 'responsive-customer-order-detail.png');
    await visitAndCapture(page, '/customer/gift-codes', 'responsive-customer-gift-codes.png');
    await visitAndCapture(page, `/customer/tickets/${ticketId}`, 'responsive-customer-ticket-detail.png');
  });

  test('admin operational pages, overlays, CKEditor and Icon Picker match responsive baselines @mobile @tablet @desktop', async ({ page, storefront, customerTickets, loginAs }, testInfo) => {
    await registerCustomer(page, uniqueCustomer('Responsive Admin Visual Customer'));
    await storefront.addToCart('e2e-seo-product', { account_email: 'admin-responsive-visual@example.test' });
    const orderId = await storefront.checkoutAndPay();
    await storefront.openOrder(orderId);
    const orderNumber = (await page.locator('h1 .st-mono').innerText()).trim();
    const subject = 'Responsive admin visual ticket with long operational context';
    await customerTickets.createForOrder(orderId, {
      subject,
      priority: '3',
      message: 'Deterministic long customer message for the responsive admin master-detail ticket layout.'
    });

    const logout = page.locator('form[action="/auth/customer/logout"]').first();
    await Promise.all([page.waitForURL(/\/$/), logout.evaluate((form: HTMLFormElement) => form.requestSubmit())]);
    await loginAs('SuperAdmin');

    await visitAndCapture(page, '/admin/dashboard', 'responsive-admin-dashboard.png', [page.locator('.vz-stats,.vz-chart,.vz-feed,tbody')]);
    await visitAndCapture(page, '/admin/products', 'responsive-admin-products.png', [page.locator('tbody')]);
    await visitAndCapture(page, '/admin/products/create', 'responsive-admin-product-create-ckeditor.png');

    await page.getByTestId('add-product-feature').click();
    await page.getByTestId('icon-picker-trigger').first().click();
    await expect(page.locator('.vz-icon-picker[open]')).toBeVisible();
    await page.getByTestId('icon-picker-search').fill('wallet');
    await capture(page, 'responsive-admin-icon-picker.png');
    await page.locator('.vz-icon-picker[open] .vz-icon-picker__header .vz-iconaction').click();

    await visitAndCapture(page, '/admin/products/31000000-0000-0000-0000-000000000002', 'responsive-admin-product-edit.png');

    await page.goto('/admin/orders', { waitUntil: 'networkidle' });
    await page.locator('#order-search').fill(orderNumber);
    const orderRow = page.locator('tbody tr').filter({ hasText: orderNumber });
    await expect(orderRow).toHaveCount(1);
    await capture(page, 'responsive-admin-orders.png');
    await orderRow.locator('.vz-ctx__trigger').click();
    await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await auditResponsivePage(page, testInfo, {
      route: { route: '/admin/orders detail dialog', path: '/admin/orders', component: 'Admin/OrderDetails modal', persona: 'SuperAdmin', interactions: ['detail', 'footer actions'] },
      viewport: `${page.viewportSize()!.width}x${page.viewportSize()!.height}`,
      theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
    });
    await capture(page, 'responsive-admin-order-detail.png');
    await page.getByRole('dialog').locator('.vz-dialog__close').click();

    await page.goto('/admin/payments', { waitUntil: 'networkidle' });
    await page.locator('#pay-search').fill(orderNumber);
    const paymentRow = page.locator('tbody tr').filter({ hasText: orderNumber });
    await expect(paymentRow).toHaveCount(1);
    await capture(page, 'responsive-admin-payments.png');
    await paymentRow.locator('.vz-iconaction').click();
    await expect(page.locator('.vz-slidepanel')).toBeVisible();
    await capture(page, 'responsive-admin-payment-detail.png');
    await page.locator('.vz-slidepanel .vz-slidepanel__close').click();

    await page.goto('/admin/tickets', { waitUntil: 'networkidle' });
    const ticket = page.locator('.vz-ticket-item').filter({ hasText: subject }).first();
    await expect(ticket).toBeVisible();
    await ticket.click();
    await expect(page.locator('.vz-card__body .vz-msg')).toBeVisible();
    await capture(page, 'responsive-admin-tickets.png', [
      page.locator('.vz-stats,.vz-pill__count'),
      page.locator('.vz-inbox-grid > .vz-card:first-child .vz-card__body')
    ]);

    await visitAndCapture(page, '/admin/settings', 'responsive-admin-settings.png');
  });
});

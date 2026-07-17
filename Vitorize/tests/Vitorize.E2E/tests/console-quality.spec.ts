import { expect, test, type BrowserContext } from '@playwright/test';
import { loginAdmin, monitorBrowser, registerCustomer, uniqueCustomer } from './support/app';

async function assertRoutesClean(context: BrowserContext, routes: string[]): Promise<void> {
  for (const route of routes) {
    const page = await context.newPage();
    const browser = monitorBrowser(page);
    await page.goto(route, { waitUntil: 'networkidle' });
    await expect(page.locator('body')).toBeVisible();
    await page.waitForTimeout(250);
    browser.assertClean();
    await page.close();
  }
}

test('public pages load without JavaScript, Blazor, CSP or resource failures', async ({ context }) => {
  await assertRoutesClean(context, [
    '/', '/categories', '/category/e2e-category', '/brand/e2e-brand',
    '/shop?q=E2E%20Dynamic', '/product/e2e-seo-product', '/cart', '/faq'
  ]);
});

test('authenticated customer pages load without browser failures', async ({ page, context }) => {
  await registerCustomer(page, uniqueCustomer('Console Customer'));
  await page.close();
  await assertRoutesClean(context, [
    '/customer/dashboard', '/customer/profile', '/customer/orders',
    '/customer/wallet', '/customer/tickets', '/customer/verification'
  ]);
});

test('authenticated admin pages load without browser failures', async ({ page, context }) => {
  await loginAdmin(page);
  await page.close();
  await assertRoutesClean(context, [
    '/admin/dashboard', '/admin/products', '/admin/orders',
    '/admin/settings', '/admin/sms', '/admin/monitoring'
  ]);
});

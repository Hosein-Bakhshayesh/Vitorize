import { expect, test, type Locator, type Page } from '@playwright/test';
import { expectRtlAndNoOverflow, loginAdmin, registerCustomer, uniqueCustomer } from './support/app';

const screenshotOptions = (mask: Locator[] = []) => ({
  fullPage: true,
  animations: 'disabled' as const,
  caret: 'hide' as const,
  mask,
  maxDiffPixelRatio: 0.02
});

async function capture(page: Page, route: string, name: string, mask: Locator[] = []): Promise<void> {
  await page.goto(route, { waitUntil: 'networkidle' });
  await expect(page.locator('body')).toBeVisible();
  // Interactive Server data loads can begin after the initial document reaches
  // network-idle. Allow the configured SQL retry window to finish before taking
  // a deterministic screenshot; visual assertions still use their strict timeout.
  await expect(page.locator('.vz-spinner:visible')).toHaveCount(0, { timeout: 30_000 });
  await expectRtlAndNoOverflow(page);
  await expect(page).toHaveScreenshot(name, screenshotOptions(mask));
}

test('storefront, product, cart and checkout match approved responsive baselines', async ({ page }) => {
  await capture(page, '/', 'home.png');
  await capture(page, '/category/e2e-category', 'category.png');
  await capture(page, '/product/e2e-seo-product', 'product.png');

  await registerCustomer(page, uniqueCustomer('Visual Customer'));
  await page.goto('/product/e2e-seo-product', { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await page.locator('#product-input-account_email').fill('visual@example.test');
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success')).toBeVisible();

  await capture(page, '/cart', 'cart.png', [page.locator('.st-input-summary')]);
  await capture(page, '/checkout', 'checkout.png', [page.locator('.st-input-summary')]);
});

test('core admin operations match approved responsive baselines', async ({ page }) => {
  await loginAdmin(page);
  const dashboardDynamic = [
    page.locator('.vz-stats'),
    page.locator('.vz-chart'),
    page.locator('.vz-feed'),
    page.locator('tbody'),
    page.locator('time')
  ];
  const monitoringDynamic = [page.locator('.vz-stat__value'), page.locator('tbody'), page.locator('time')];
  await capture(page, '/admin/dashboard', 'admin-dashboard.png', dashboardDynamic);
  await capture(page, '/admin/products', 'admin-products.png', [page.locator('tbody')]);
  await capture(page, '/admin/orders', 'admin-orders.png', [page.locator('tbody')]);
  await capture(page, '/admin/settings', 'admin-settings.png');
  await capture(page, '/admin/monitoring', 'admin-monitoring.png', monitoringDynamic);
});

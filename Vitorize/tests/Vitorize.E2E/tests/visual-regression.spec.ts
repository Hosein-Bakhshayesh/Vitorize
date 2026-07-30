import { expect, test, type Locator, type Page } from '@playwright/test';
import { expectRtlAndNoOverflow, loginAdmin, registerCustomer, uniqueCustomer } from './support/app';

const screenshotOptions = (mask: Locator[] = []) => ({
  fullPage: true,
  animations: 'disabled' as const,
  caret: 'hide' as const,
  mask,
  maxDiffPixelRatio: 0.02
});

async function capture(
  page: Page,
  route: string,
  name: string,
  mask: Locator[] = [],
  prepare?: () => Promise<void>
): Promise<void> {
  await page.goto(route, { waitUntil: 'networkidle' });
  await expect(page.locator('body')).toBeVisible();
  // Interactive Server data loads can begin after the initial document reaches
  // network-idle. Allow the configured SQL retry window to finish before taking
  // a deterministic screenshot; visual assertions still use their strict timeout.
  await expect(page.locator('.vz-spinner:visible')).toHaveCount(0, { timeout: 30_000 });
  await prepare?.();
  await expectRtlAndNoOverflow(page);
  await expect(page).toHaveScreenshot(name, screenshotOptions(mask));
}

test('storefront, product, cart and checkout match approved responsive baselines', async ({ page }) => {
  const homeProductGrids = page.locator('section.st-section:has(.st-pcard__wish) > .st-grid');
  await capture(page, '/', 'home.png', [homeProductGrids], async () => {
    await expect(homeProductGrids).toHaveCount(3);
    await page.addStyleTag({
      content: 'section.st-section:has(.st-pcard__wish) > .st-grid { height: 360px !important; overflow: hidden !important; }'
    });
  });
  // Recency is intentionally live production data. Exercise the category route
  // with the supported server-side price sort so the visual baseline represents
  // a fixed catalog state rather than mutable insertion timestamps.
  await capture(page, '/category/e2e-category?sort=cheapest', 'category.png');
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
  await capture(page, '/admin/orders', 'admin-orders.png', [], async () => {
    const search = page.locator('#order-search');
    await search.fill('__visual_no_matching_order__');
    await expect(page.locator('.vz-card')).toContainText('سفارشی یافت نشد');
    // Keep the approved zero-order baseline while the component remains internally
    // filtered, so prior commerce tests cannot alter this layout screenshot.
    await search.evaluate((input: HTMLInputElement) => { input.value = ''; });
    await page.locator('.vz-stat__value, .vz-pill__count').evaluateAll(elements => {
      for (const element of elements) element.textContent = '۰';
    });
  });
  await capture(page, '/admin/settings', 'admin-settings.png');
  await capture(page, '/admin/monitoring', 'admin-monitoring.png', monitoringDynamic);
});

import { expect, test } from '@playwright/test';
import { apiBaseUrl, monitorBrowser } from './support/app';

const warning = '.st-alert.st-alert--info';

test.describe('FIX-09 Phase 1 storefront KYC policy projection @fix09p1', () => {
  test('none, always, and threshold quantity states are reactive without a reload', async ({ page }) => {
    const browser = monitorBrowser(page);

    await page.goto('/product/e2e-fix09-none', { waitUntil: 'networkidle' });
    await expect(page.locator(warning)).toHaveCount(0);

    await page.goto('/product/e2e-fix09-always', { waitUntil: 'networkidle' });
    await expect(page.locator(warning)).toContainText('احراز هویت');

    await page.goto('/product/e2e-fix09-quantity', { waitUntil: 'networkidle' });
    await expect(page.locator(warning)).toHaveCount(0);

    // Another Phase-1 spec re-versions this product's KYC policy and leaves the new threshold in
    // place, so the units needed to cross it depend on execution order. Read the live policy and
    // cross it deliberately; the contract under test is the reactivity, not a particular number.
    const detail = await page.request.get(`${apiBaseUrl}/products/slug/e2e-fix09-quantity`);
    expect(detail.ok(), await detail.text()).toBeTruthy();
    const policy = (await detail.json()).data as { finalPrice: number; kycThresholdAmount: number };
    const unitsToCross = Math.ceil(policy.kycThresholdAmount / policy.finalPrice);
    expect(unitsToCross, 'one unit must stay below the threshold').toBeGreaterThan(1);

    const quantity = page.locator('.st-buy__qty .st-qty');
    for (let units = 1; units < unitsToCross; units += 1) {
      await quantity.locator('button').last().click();
      await expect(quantity.locator('span')).not.toHaveText('۱');
    }
    await expect(page.locator(warning)).toContainText('احراز هویت');

    await quantity.locator('button').first().click();
    await expect(page.locator(warning)).toHaveCount(0);
    browser.assertClean();
  });
});

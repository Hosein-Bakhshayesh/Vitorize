import { expect, test } from '@playwright/test';
import { monitorBrowser } from './support/app';

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

    const quantity = page.locator('.st-buy__qty .st-qty');
    await quantity.locator('button').last().click();
    await expect(page.locator(warning)).toContainText('احراز هویت');

    await quantity.locator('button').first().click();
    await expect(page.locator(warning)).toHaveCount(0);
    browser.assertClean();
  });
});

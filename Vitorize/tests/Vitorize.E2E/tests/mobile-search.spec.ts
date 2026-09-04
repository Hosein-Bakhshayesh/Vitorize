import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow, monitorBrowser } from './support/app';

test('mobile search opens in place and only navigates after a query is submitted', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.startsWith('mobile'), 'Mobile-only interaction.');
  const browser = monitorBrowser(page);

  await page.goto('/', { waitUntil: 'networkidle' });
  await page.getByRole('button', { name: 'باز کردن جستجو' }).click();

  const dialog = page.getByRole('dialog', { name: 'جستجو در محصولات' });
  const input = dialog.getByRole('searchbox', { name: 'عبارت جستجو' });
  await expect(dialog).toBeVisible();
  await expect(input).toBeFocused();
  await expect(page).toHaveURL(/\/$/);

  await input.fill('E2E Dynamic Product');
  await dialog.getByRole('button', { name: 'جستجو', exact: true }).click();
  await expect(page).toHaveURL(/\/search\?q=E2E(?:%20|\+)Dynamic(?:%20|\+)Product/);
  await expect(page.getByRole('heading', { name: /نتایج جستجو/ })).toBeVisible();
  await expectRtlAndNoOverflow(page);
  browser.assertClean();
});

test('a direct empty search URL offers search instead of product results', async ({ page }) => {
  await page.goto('/search', { waitUntil: 'networkidle' });

  await expect(page.getByRole('heading', { name: 'جستجو در محصولات' })).toBeVisible();
  await expect(page.locator('.st-lgrid')).toHaveCount(0);
});

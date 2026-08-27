import { expect, test, ProductBuilder } from '../framework/fixtures';

test.describe('Product KYC configuration removal @fix09p1admin', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop product form has no KYC controls and saves normally', async ({
    page, loginAs, adminProduct, consoleGuard
  }, testInfo) => {
    test.skip(testInfo.project.name.startsWith('mobile'), 'The compact layout is covered separately.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');

    const suffix = `${Date.now().toString(36)}-${testInfo.project.name}`;
    await adminProduct.openCreate();
    await expectNoProductKycControls(page);
    await adminProduct.fill(product(`product-no-kyc-${suffix}`, `No product KYC ${suffix}`));
    await Promise.all([
      page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/i),
      page.getByTestId('product-save').click()
    ]);

    await page.reload();
    await expectNoProductKycControls(page);
    consoleGuard.assertClean();
  });

  test('compact product form has no KYC controls and its save remains reachable', async ({
    page, loginAs, adminProduct, consoleGuard
  }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'Desktop matrix covers the full save flow.');
    await page.setViewportSize({ width: 393, height: 852 });
    await loginAs('SuperAdmin');

    const suffix = `${Date.now().toString(36)}-compact`;
    await adminProduct.openCreate();
    await adminProduct.fill(product(`product-no-kyc-${suffix}`, `No product KYC ${suffix}`));
    await expectNoProductKycControls(page);
    await expect(page.getByTestId('product-save')).toBeVisible();
    const layout = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(layout).toBeLessThanOrEqual(1);
    consoleGuard.assertClean();
  });
});

function product(slug: string, title: string) {
  return new ProductBuilder(slug, title).ofType(99).deliveredBy(1).withoutBrand().priced(5_000).build();
}

async function expectNoProductKycControls(page: import('@playwright/test').Page) {
  await expect(page.getByTestId('product-kyc-mode')).toHaveCount(0);
  await expect(page.getByTestId('product-kyc-policy')).toHaveCount(0);
  await expect(page.getByText('سیاست احراز هویت', { exact: true })).toHaveCount(0);
  await expect(page.getByText('نیازمند احراز هویت', { exact: true })).toHaveCount(0);
}

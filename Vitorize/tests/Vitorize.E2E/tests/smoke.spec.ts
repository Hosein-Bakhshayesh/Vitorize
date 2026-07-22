import { test, expect, TAG } from '../framework/fixtures';

// Fast, deterministic critical-path gate across both personas. Kept intentionally lean so it stays
// well under ~5 minutes and never flakes; the deep create/purchase/delivery flows live in the
// business + regression suites (storefront-commerce.spec / admin-flows.spec).
test.describe('smoke', () => {
  test('application starts and the storefront home renders', { tag: [TAG.smoke] }, async ({ page }) => {
    const response = await page.goto('/');
    expect(response?.status()).toBeLessThan(400);
    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('body')).toBeVisible();
  });

  test('SuperAdmin signs in, keeps the session across reload, then logs out', {
    tag: [TAG.smoke, TAG.admin]
  }, async ({ loginAs, adminShell, page }) => {
    await loginAs('SuperAdmin');
    await adminShell.gotoDashboard();

    // Session persists across a full reload (regression guard for the auth-cookie/circuit fixes).
    await adminShell.reload();
    await adminShell.expectAuthenticated();

    await adminShell.logout();
    await page.goto('/admin/dashboard');
    await expect(page).toHaveURL(/\/admin\/login/);
  });

  test('SuperAdmin can reach a representative admin section', { tag: [TAG.smoke, TAG.admin] }, async ({ loginAs, adminShell, page }) => {
    await loginAs('SuperAdmin');
    await adminShell.open('products');
    await expect(page).toHaveURL(/\/admin\/products/);
    await adminShell.expectAuthenticated();
  });

  test('Admin (non-super) can enter the admin panel', { tag: [TAG.smoke, TAG.admin] }, async ({ loginAs, adminShell }) => {
    await loginAs('Admin');
    await adminShell.gotoDashboard();
  });

  test('Customer signs in, views the account area, browses the storefront and logs out', {
    tag: [TAG.smoke, TAG.customer]
  }, async ({ loginAs, storefront, page }) => {
    await loginAs('Customer');

    await storefront.openAccount('orders');
    await expect(page).toHaveURL(/\/customer\/orders/);

    await storefront.home();
    await storefront.openProduct('e2e-seo-product');
    await expect(page.locator('main')).toBeVisible();

    await storefront.openAccount('dashboard');
    await storefront.logout();
    await page.goto('/customer/dashboard');
    await expect(page).toHaveURL(/\/login/);
  });

  test('CustomerVIP (verified, funded wallet) can sign in and reach the wallet', {
    tag: [TAG.smoke, TAG.customer]
  }, async ({ loginAs, storefront, page }) => {
    await loginAs('CustomerVIP');
    await storefront.openAccount('wallet');
    await expect(page).toHaveURL(/\/customer\/wallet/);
    await expect(page.locator('main')).toBeVisible();
  });
});

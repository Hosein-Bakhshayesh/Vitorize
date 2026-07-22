import { expect, test, type Page } from '@playwright/test';
import {
  loginAdminWith,
  logoutAdmin,
  loginCustomer,
  logoutCustomer,
  monitorBrowser,
  plainAdminMobile,
  plainAdminPassword,
  seededCustomer,
  superAdminMobile,
  superAdminPassword
} from './support/app';

// Every Playwright test runs in an isolated browser context, so each scenario starts from a clean
// cookie jar unless it deliberately signs in twice. These tests exercise the FULL browser lifecycle
// (SSR render + interactive Blazor circuit) rather than only the HTTP login API.

async function expectAdminDashboard(page: Page): Promise<void> {
  await expect(page).toHaveURL(/\/admin\/dashboard/);
  await expect(page.locator('button.vz-profile')).toBeVisible();
}

async function expectCustomerDashboard(page: Page): Promise<void> {
  await expect(page).toHaveURL(/\/customer\/dashboard/);
  await expect(page.locator('form[action="/auth/customer/logout"]').first()).toBeVisible();
}

test.describe('admin authentication lifecycle', () => {
  test('Admin logs in, refreshes, navigates and logs out without losing the session', async ({ page }) => {
    const browser = monitorBrowser(page);
    await loginAdminWith(page, plainAdminMobile, plainAdminPassword);
    await expectAdminDashboard(page);

    // Refresh must not bounce back to login (the reported redirect-loop symptom).
    await page.reload();
    await expectAdminDashboard(page);

    // Navigate to another admin page and back; the interactive circuit stays authenticated.
    await page.goto('/admin/orders');
    await expect(page).toHaveURL(/\/admin\/orders/);
    await expect(page.locator('button.vz-profile')).toBeVisible();

    await page.goto('/admin/dashboard');
    await expectAdminDashboard(page);

    await logoutAdmin(page);
    await expect(page).toHaveURL(/\/admin\/login/);

    // After logout the admin area is no longer accessible.
    await page.goto('/admin/dashboard');
    await expect(page).toHaveURL(/\/admin\/login/);
    browser.assertClean();
  });

  test('SuperAdmin logs in, refreshes, navigates and logs out', async ({ page }) => {
    await loginAdminWith(page, superAdminMobile, superAdminPassword);
    await expectAdminDashboard(page);

    await page.reload();
    await expectAdminDashboard(page);

    await page.goto('/admin/products');
    await expect(page).toHaveURL(/\/admin\/products/);
    await expect(page.locator('button.vz-profile')).toBeVisible();

    await page.goto('/admin/dashboard');
    await logoutAdmin(page);
    await expect(page).toHaveURL(/\/admin\/login/);
  });
});

test.describe('customer authentication lifecycle', () => {
  test('Customer logs in, refreshes, navigates and logs out without losing the session', async ({ page }) => {
    const browser = monitorBrowser(page);
    await loginCustomer(page, seededCustomer);
    await expectCustomerDashboard(page);

    await page.reload();
    await expectCustomerDashboard(page);

    await page.goto('/customer/profile');
    await expect(page).toHaveURL(/\/customer\/profile/);

    // Storefront navigation keeps the authenticated session.
    await page.goto('/');
    await expect(page).toHaveURL(/\/$/);
    await page.goto('/customer/profile');
    await expect(page).toHaveURL(/\/customer\/profile/);

    await logoutCustomer(page);
    await page.goto('/customer/profile');
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);
    browser.assertClean();
  });
});

test.describe('mixed-cookie sessions', () => {
  test('Customer cookie then Admin login keeps the admin dashboard working', async ({ page }) => {
    await loginCustomer(page, seededCustomer);
    await expectCustomerDashboard(page);

    await loginAdminWith(page, plainAdminMobile, plainAdminPassword);
    await expectAdminDashboard(page);

    await page.reload();
    await expectAdminDashboard(page);
  });

  test('Admin cookie then Customer login keeps both flows working', async ({ page }) => {
    await loginAdminWith(page, plainAdminMobile, plainAdminPassword);
    await expectAdminDashboard(page);

    await loginCustomer(page, seededCustomer);
    await expectCustomerDashboard(page);

    // Both separate sessions coexist: the admin panel is still reachable.
    await page.goto('/admin/dashboard');
    await expectAdminDashboard(page);
  });
});

test.describe('authorization boundaries', () => {
  test('Anonymous user is redirected away from admin and customer areas', async ({ page }) => {
    await page.goto('/admin/dashboard');
    await expect(page).toHaveURL(/\/admin\/login/);

    await page.goto('/customer/dashboard');
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);
  });

  test('Customer is denied the admin panel', async ({ page }) => {
    await loginCustomer(page, seededCustomer);
    await expectCustomerDashboard(page);

    await page.goto('/admin/dashboard');
    await expect(page).not.toHaveURL(/\/admin\/dashboard/);
    await expect(page).toHaveURL(/\/admin\/login/);
  });

  test('Admin login and refresh produce no redirect loop', async ({ page }) => {
    const adminLoginVisits: string[] = [];
    page.on('framenavigated', frame => {
      if (frame === page.mainFrame() && /\/admin\/login/.test(frame.url())) {
        adminLoginVisits.push(frame.url());
      }
    });

    await loginAdminWith(page, plainAdminMobile, plainAdminPassword);
    await expectAdminDashboard(page);
    await page.reload();
    await expectAdminDashboard(page);

    // The only /admin/login visit is the initial navigation before submitting credentials;
    // a redirect loop would revisit it after authentication.
    expect(adminLoginVisits.length).toBeLessThanOrEqual(1);
  });
});

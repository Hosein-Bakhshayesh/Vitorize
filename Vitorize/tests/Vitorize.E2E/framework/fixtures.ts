import { test as base, expect, type Page } from '@playwright/test';
import { AdminLoginPage } from './pages/AdminLoginPage';
import { AdminShellPage } from './pages/AdminShellPage';
import { StoreLoginPage } from './pages/StoreLoginPage';
import { StorefrontPage } from './pages/StorefrontPage';
import { CustomerTicketsPage } from './pages/CustomerTicketsPage';
import { AdminTicketsPage } from './pages/AdminTicketsPage';
import { AdminProductPage } from './pages/AdminProductPage';
import { AdminVariantPage } from './pages/AdminVariantPage';
import { StorefrontProductPage } from './pages/StorefrontProductPage';
import { USERS, type Role, type TestUser } from './users';
// Reuse the battle-tested primitives from the existing suite instead of duplicating them.
import {
  monitorBrowser,
  latestOtp,
  expireOtp,
  uniqueCustomer,
  registerCustomer,
  expectRtlAndNoOverflow
} from '../tests/support/app';

interface Fixtures {
  adminLogin: AdminLoginPage;
  adminShell: AdminShellPage;
  storeLogin: StoreLoginPage;
  storefront: StorefrontPage;
  customerTickets: CustomerTicketsPage;
  adminTickets: AdminTicketsPage;
  adminProduct: AdminProductPage;
  adminVariant: AdminVariantPage;
  storefrontProduct: StorefrontProductPage;
  /** Captures console / pageerror / requestfailed for UI-quality assertions. */
  consoleGuard: ReturnType<typeof monitorBrowser>;
  /** Sign in as any deterministic role through the correct scheme, then assert the landing area. */
  loginAs: (role: Role) => Promise<TestUser>;
}

/**
 * The QA framework's base test. Every suite imports { test, expect } from here to get page objects,
 * an authentication helper and a console guard for free, with per-test isolation (fresh browser
 * context => fresh cookie jar => independent, parallel-safe tests).
 */
export const test = base.extend<Fixtures>({
  adminLogin: async ({ page }, use) => use(new AdminLoginPage(page)),
  adminShell: async ({ page }, use) => use(new AdminShellPage(page)),
  storeLogin: async ({ page }, use) => use(new StoreLoginPage(page)),
  storefront: async ({ page }, use) => use(new StorefrontPage(page)),
  customerTickets: async ({ page }, use) => use(new CustomerTicketsPage(page)),
  adminTickets: async ({ page }, use) => use(new AdminTicketsPage(page)),
  adminProduct: async ({ page }, use) => use(new AdminProductPage(page)),
  adminVariant: async ({ page }, use) => use(new AdminVariantPage(page)),
  storefrontProduct: async ({ page }, use) => use(new StorefrontProductPage(page)),
  consoleGuard: async ({ page }, use) => use(monitorBrowser(page)),
  loginAs: async ({ adminLogin, adminShell, storeLogin, storefront }, use) => {
    await use(async (role: Role) => {
      const u = USERS[role];
      if (u.isAdmin) {
        await adminLogin.signIn(u);
        await adminShell.expectAuthenticated();
      } else {
        await storeLogin.signIn(u);
        await storefront.expectCustomerDashboard();
      }
      return u;
    });
  }
});

/** Remove every item from the signed-in customer's cart through the real storefront UI. */
export async function clearCustomerCart(page: Page): Promise<void> {
  await page.goto('/cart');
  const clearCart = page.locator('.st-stack > button.st-btn--ghost');
  const emptyCart = page.locator('.st-errpage--inline');
  await expect(clearCart.or(emptyCart)).toBeVisible();
  if (await clearCart.isVisible()) {
    await clearCart.click();
    await expect(clearCart).toHaveCount(0);
    await expect(emptyCart).toBeVisible();
  }
}

/** Sign the deterministic Customer into an auxiliary context and isolate its cart via the real UI. */
export async function loginSeededCustomerWithEmptyCart(page: Page): Promise<TestUser> {
  const customer = USERS.Customer;
  await new StoreLoginPage(page).signIn(customer);
  await clearCustomerCart(page);
  return customer;
}

export { expect };
export { USERS, user, type Role, type TestUser } from './users';
export { TAG } from './tags';
export { uniqueCustomer, registerCustomer, latestOtp, expireOtp, expectRtlAndNoOverflow };
// Re-export page-object classes so specs can drive a second browser context (e.g. admin) manually.
export { AdminLoginPage } from './pages/AdminLoginPage';
export { AdminShellPage } from './pages/AdminShellPage';
export { AdminTicketsPage } from './pages/AdminTicketsPage';
export { StoreLoginPage } from './pages/StoreLoginPage';
export { AdminProductPage } from './pages/AdminProductPage';
export { AdminVariantPage } from './pages/AdminVariantPage';
export { StorefrontProductPage } from './pages/StorefrontProductPage';
export { ProductBuilder, type ProductInput } from './builders/ProductBuilder';
export { VariantBuilder, type VariantInput } from './builders/VariantBuilder';
export { ProductScenarioFactory, type ProductMatrixScenario } from './builders/ProductScenarioFactory';
export { getProductState, expectCatalogIntegrity, type ProductState } from './productState';

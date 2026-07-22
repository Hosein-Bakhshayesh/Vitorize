import { test as base, expect } from '@playwright/test';
import { AdminLoginPage } from './pages/AdminLoginPage';
import { AdminShellPage } from './pages/AdminShellPage';
import { StoreLoginPage } from './pages/StoreLoginPage';
import { StorefrontPage } from './pages/StorefrontPage';
import { CustomerTicketsPage } from './pages/CustomerTicketsPage';
import { AdminTicketsPage } from './pages/AdminTicketsPage';
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

export { expect };
export { USERS, user, type Role, type TestUser } from './users';
export { TAG } from './tags';
export { uniqueCustomer, registerCustomer, latestOtp, expireOtp, expectRtlAndNoOverflow };
// Re-export page-object classes so specs can drive a second browser context (e.g. admin) manually.
export { AdminLoginPage } from './pages/AdminLoginPage';
export { AdminShellPage } from './pages/AdminShellPage';
export { AdminTicketsPage } from './pages/AdminTicketsPage';
export { StoreLoginPage } from './pages/StoreLoginPage';

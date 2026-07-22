import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const apiBaseUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5177/api';
export const adminMobile = process.env.E2E_ADMIN_MOBILE!;
export const adminPassword = process.env.E2E_ADMIN_PASSWORD!;

let identitySequence = 0;

export type CustomerIdentity = {
  fullName: string;
  mobile: string;
  email: string;
  password: string;
};

export function uniqueCustomer(label = 'Browser Customer'): CustomerIdentity {
  identitySequence += 1;
  const suffix = `${Date.now() % 10_000_000}${identitySequence % 10}`.padStart(8, '0');
  return {
    fullName: `${label} ${suffix}`,
    mobile: `090${suffix}`,
    email: `e2e-${suffix}@example.test`,
    password: `E2e-${suffix}-aA1!`
  };
}

export async function registerCustomer(page: Page, customer = uniqueCustomer()): Promise<CustomerIdentity> {
  await page.goto('/register');
  await page.locator('input[name="fullName"]').fill(customer.fullName);
  await page.locator('input[name="mobile"]').fill(customer.mobile);
  await page.locator('input[name="email"]').fill(customer.email);
  await page.locator('input[name="password"]').fill(customer.password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/i),
    page.locator('button[type="submit"]').click()
  ]);
  return customer;
}

export async function loginCustomer(page: Page, customer: CustomerIdentity, returnUrl?: string): Promise<void> {
  const loginUrl = returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login';
  const current = new URL(page.url());
  if (current.pathname !== '/login' || (returnUrl && !current.searchParams.has('returnUrl'))) {
    await page.goto(loginUrl);
  }
  await page.locator('#pw-mobile').fill(customer.mobile);
  await page.locator('#pw-pass').fill(customer.password);
  await Promise.all([
    page.waitForURL(returnUrl ? new RegExp(returnUrl.replaceAll('/', '\\/')) : /\/customer\/dashboard/i),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

export async function logoutCustomer(page: Page): Promise<void> {
  const form = page.locator('form[action="/auth/customer/logout"]').first();
  await Promise.all([page.waitForURL(/\/$/), form.locator('button[type="submit"]').click()]);
}

export async function loginAdmin(page: Page): Promise<void> {
  await page.goto('/admin/login');
  await page.locator('input[name="mobile"]').fill(adminMobile);
  await page.locator('input[name="password"]').fill(adminPassword);
  await Promise.all([
    page.waitForURL(/\/admin\/(dashboard)?$/i),
    page.locator('button[type="submit"]').click()
  ]);
}

// The bootstrap admin (adminMobile) is promoted to SuperAdmin by the E2E fixture. The fixture also
// seeds a dedicated plain-Admin and a Customer so role separation can be exercised deterministically.
export const superAdminMobile = process.env.E2E_SUPERADMIN_MOBILE ?? adminMobile;
export const superAdminPassword = process.env.E2E_SUPERADMIN_PASSWORD ?? adminPassword;
export const plainAdminMobile = process.env.E2E_PLAIN_ADMIN_MOBILE ?? '09120000012';
export const plainAdminPassword = process.env.E2E_PLAIN_ADMIN_PASSWORD ?? adminPassword;

export const seededCustomer: CustomerIdentity = {
  fullName: 'E2E Customer',
  mobile: process.env.E2E_CUSTOMER_MOBILE ?? '09120000013',
  email: 'e2e-customer@example.test',
  password: process.env.E2E_CUSTOMER_PASSWORD ?? adminPassword
};

export async function loginAdminWith(page: Page, mobile: string, password: string): Promise<void> {
  await page.goto('/admin/login');
  await page.locator('input[name="mobile"]').fill(mobile);
  await page.locator('input[name="password"]').fill(password);
  await Promise.all([
    page.waitForURL(/\/admin(\/dashboard)?$/i),
    page.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
  ]);
}

// Admin logout is a real form POST rendered only inside the open profile menu. Submit it
// programmatically (still a full-document POST) so the menu's close-overlay can't intercept the click.
export async function logoutAdmin(page: Page): Promise<void> {
  await page.locator('button.vz-profile').click();
  const form = page.locator('form[action="/admin/auth/logout"]');
  await expect(form).toBeVisible();
  await Promise.all([
    page.waitForURL(/\/admin\/login/),
    form.evaluate((f: HTMLFormElement) => f.requestSubmit())
  ]);
}

export async function latestOtp(request: APIRequestContext, mobile: string, previousCode?: string): Promise<string> {
  let code: string | null = null;
  await expect.poll(async () => {
    const response = await request.get(`${apiBaseUrl}/testing/sms/latest-otp?mobile=${encodeURIComponent(mobile)}`);
    if (!response.ok()) return null;
    const body = await response.json();
    code = body.code !== previousCode ? body.code as string : null;
    return code;
  }).not.toBeNull();
  expect(code).toMatch(/^\d{6}$/);
  return code!;
}

export async function expireOtp(request: APIRequestContext, mobile: string): Promise<void> {
  const response = await request.post(`${apiBaseUrl}/testing/otp/expire?mobile=${encodeURIComponent(mobile)}`);
  expect(response.ok()).toBe(true);
  expect((await response.json()).affected).toBeGreaterThan(0);
}

export function monitorBrowser(page: Page) {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(`pageerror: ${error.message}`));
  page.on('console', message => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', request => {
    const reason = request.failure()?.errorText ?? 'unknown failure';
    if (!reason.includes('ERR_ABORTED')) errors.push(`request: ${request.url()} (${reason})`);
  });
  return {
    assertClean: () => expect(errors, errors.join('\n')).toEqual([]),
    errors
  };
}

export async function expectRtlAndNoOverflow(page: Page): Promise<void> {
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  const layout = await page.evaluate(() => {
    const root = document.documentElement;
    const overflow = root.scrollWidth - root.clientWidth;
    const offenders = Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .filter(element => {
        const rect = element.getBoundingClientRect();
        return !element.closest('.vz-splash, .st-marquee, .st-hslider, .st-catrail, .st-news, .st-trustchips, .vz-sidebar, .vz-table-wrap, .vz-tabs, .vz-settabs')
          && !Array.from(element.classList).some(className => className.includes('aurora'))
          && rect.width > 0
          && (rect.left < -1 || rect.right > window.innerWidth + 1);
      })
      .slice(0, 8)
      .map(element => {
        const rect = element.getBoundingClientRect();
        const selector = `${element.tagName.toLowerCase()}${element.id ? `#${element.id}` : ''}${
          element.classList.length ? `.${Array.from(element.classList).join('.')}` : ''
        }`;
        return `${selector}[${Math.round(rect.left)},${Math.round(rect.right)}] parent=${element.parentElement?.className ?? ''}`;
      });
    return { overflow, clientWidth: root.clientWidth, scrollWidth: root.scrollWidth, offenders };
  });
  expect(layout.overflow, JSON.stringify(layout)).toBeLessThanOrEqual(1);
  expect(layout.offenders, JSON.stringify(layout)).toEqual([]);
}

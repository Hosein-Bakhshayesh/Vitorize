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

/**
 * Registers a customer through the real two-step flow.
 *
 * Registration is gated on a code sent to the mobile, so the form alone no longer signs anybody in:
 * it lands on the verification step, and only the correct code establishes the session. The code is
 * read from the fake SMS provider the Testing host exposes, exactly as the OTP login specs do.
 */
export async function registerCustomer(page: Page, customer = uniqueCustomer()): Promise<CustomerIdentity> {
  await page.goto('/register');
  await page.locator('input[name="fullName"]').fill(customer.fullName);
  await page.locator('input[name="mobile"]').fill(customer.mobile);
  await page.locator('input[name="email"]').fill(customer.email);
  await page.locator('input[name="password"]').fill(customer.password);
  await Promise.all([
    page.waitForURL(/\/register\?stage=verify/i),
    page.locator('button[type="submit"]').click()
  ]);

  const code = await latestOtp(page.request, customer.mobile);
  await page.getByTestId('register-otp-code').fill(code);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard|\/checkout|\/cart/i),
    page.getByTestId('register-otp-submit').click()
  ]);
  return customer;
}

/**
 * Gives a freshly created Manual/SupportRequired product a sellable quantity.
 *
 * Inventory is tracked per SKU, so creating such a product also creates its canonical SKU — with
 * zero stock, because nothing should be offered for sale before someone says how much of it
 * exists. A test that creates a product through the admin API and then buys it has to perform the
 * administrator's next step too, exactly as it already imports gift codes for Instant products.
 */
export async function stockManagedProduct(
  request: APIRequestContext, adminToken: string, productId: string, quantity = 500
): Promise<void> {
  const listed = await request.get(`${apiBaseUrl}/admin/products/${productId}/variants`, {
    headers: { Authorization: `Bearer ${adminToken}` }
  });
  expect(listed.ok(), await listed.text()).toBeTruthy();
  const variants = (await listed.json() as { data: Array<Record<string, unknown>> }).data;
  expect(variants.length, 'a managed product always owns at least one SKU').toBeGreaterThan(0);

  for (const variant of variants) {
    const updated = await request.put(`${apiBaseUrl}/admin/product-variants/${variant.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { ...variant, stockQuantity: quantity }
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();
  }
}

/**
 * Product-required information is collected at Checkout. Fills every field the checkout page asks
 * for, using explicit values where given and a deterministic placeholder otherwise. Safe to call
 * when the cart needs nothing: it simply does nothing.
 */
export async function fillCheckoutProductInformation(
  page: Page, values: Record<string, string> = {}
): Promise<void> {
  const section = page.getByTestId('checkout-product-inputs');
  if (!(await section.count())) return;
  for (const field of await section.locator('input.st-input, textarea, select').all()) {
    const id = (await field.getAttribute('id')) ?? '';
    const key = id.replace(/^checkout-input-[0-9a-f]+-/i, '');
    const tag = await field.evaluate(el => el.tagName.toLowerCase());
    if (tag === 'select') { await field.selectOption({ index: 1 }).catch(() => {}); continue; }
    const value = values[key] ?? (key.includes('email') ? `e2e-${Date.now()}@example.test` : `e2e-${key || 'value'}`);
    await field.fill(value);
  }
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
  // The account layout that owns these controls renders once the Blazor circuit is connected, so
  // wait for one of them to appear. Sampling visibility the instant a navigation resolves reports
  // "no logout control" for a signed-in customer whose dashboard is still rendering.
  const form = page.locator('form[action="/auth/customer/logout"]:visible').first();
  const mobileToggle = page.locator('button.st-acc__mobile-toggle:visible').first();
  await expect(form.or(mobileToggle)).toBeVisible();

  if (await form.isVisible()) {
    await Promise.all([page.waitForURL(/\/$/), form.locator('button[type="submit"]').click()]);
    return;
  }

  await mobileToggle.click();
  const mobileForm = page.locator('#customer-account-nav form[action="/auth/customer/logout"]');
  await mobileForm.waitFor({ state: 'visible' });
  await Promise.all([page.waitForURL(/\/$/), mobileForm.locator('button[type="submit"]').click()]);
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

/**
 * Store-wide order-total KYC threshold (Toman). The seed pins it far above every fixture price so
 * ordinary commerce flows never detour into verification; KYC specs lower it around their own
 * scenario and MUST restore it (the checkout reads it live, so the change is instant).
 */
export async function setOrderKycThreshold(request: APIRequestContext, adminToken: string, toman: number): Promise<void> {
  const response = await request.post(`${apiBaseUrl}/admin/settings`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: {
      key: 'Verification.OrderAmountThresholdToman', value: String(toman),
      groupName: 'Verification', valueType: 'decimal', description: 'آستانه احراز هویت سفارش'
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

/** Self-contained variant of setOrderKycThreshold: signs in as the admin, applies the value, and
 * disposes its own request context - usable from beforeAll/afterAll and mid-test alike. */
export async function withOrderKycThreshold(toman: number): Promise<void> {
  const { request: apiRequest } = await import('@playwright/test');
  const ctx = await apiRequest.newContext();
  try {
    const login = await ctx.post(`${apiBaseUrl}/auth/login`, { data: { mobile: adminMobile, password: adminPassword } });
    const token = (await login.json()).data.accessToken as string;
    await setOrderKycThreshold(ctx, token, toman);
  } finally {
    await ctx.dispose();
  }
}

import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, adminMobile, adminPassword, registerCustomer, uniqueCustomer } from './support/app';
import type { APIRequestContext, Page } from '@playwright/test';

/**
 * The August-24 client batch, pinned:
 *  1. the purchase CTAs stay inside the viewport however many variants a product has;
 *  3. the home product row is titled «محصولات» and follows the configured default sort;
 *  4. after a storefront logout the header stays signed-out and the login button works
 *     immediately - even when an ADMIN session coexists in the same browser (the circuit used
 *     to adopt the admin identity via /_blazor and flip the header back to an avatar);
 *  5. the category mega menu opens, previews the hovered category's products, and always
 *     offers "see all";
 *  6. maintenance mode keeps the storefront closed for EVERYONE, admin cookies included,
 *     and its status code sits below the mascot rather than on it.
 */
test.describe('Client batch 6 @clientbatch6 @ui @regression', () => {
  test.describe.configure({ timeout: 180_000 });

  const CUSTOMER = '09120000013';
  const CUSTOMER_PW = 'E2E-Admin-Only-aA1!';

  async function adminToken(request: APIRequestContext): Promise<string> {
    const response = await request.post(`${apiBaseUrl}/auth/login`, {
      data: { mobile: adminMobile, password: adminPassword }
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    return (await response.json()).data.accessToken as string;
  }

  async function createManyVariantProduct(request: APIRequestContext, variants: number): Promise<string> {
    const token = await adminToken(request);
    const headers = { Authorization: `Bearer ${token}` };
    const key = `${Date.now().toString(36)}`;
    const categories = await request.get(`${apiBaseUrl}/admin/categories`, { headers });
    const categoryId = (await categories.json()).data[0].id;
    const created = await request.post(`${apiBaseUrl}/admin/products`, {
      headers,
      data: {
        title: `Batch6 CTA ${key}`, slug: `batch6-cta-${key}`, categoryId,
        productType: 1, deliveryType: 2, basePrice: 150000, currencyType: 2,
        minOrderQuantity: 1, isActive: true, shortDescription: 'CTA reachability fixture.',
        features: [], inputFields: [], tagIds: []
      }
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const productId = (await created.json()).data.id as string;
    for (let i = 1; i <= variants; i += 1) {
      const variant = await request.post(`${apiBaseUrl}/admin/products/${productId}/variants`, {
        headers,
        data: {
          title: `نسخه ${i}`, sku: `B6C-${key}-${i}`, price: 150000 + i * 1000, value: `B6C-${key}-${i}`,
          stockMode: 1, stockQuantity: 9, isDefault: i === 1, isActive: true, sortOrder: i
        }
      });
      expect(variant.ok(), await variant.text()).toBeTruthy();
    }
    return `batch6-cta-${key}`;
  }

  async function loginCustomer(page: Page) {
    await page.goto('/login', { waitUntil: 'networkidle' });
    await page.locator('#pw-mobile').fill(CUSTOMER);
    await page.locator('#pw-pass').fill(CUSTOMER_PW);
    await Promise.all([
      page.waitForURL(/\/customer\/dashboard/i, { timeout: 60_000 }),
      page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
    ]);
  }

  async function loginAdminUi(page: Page) {
    await page.goto('/admin/login', { waitUntil: 'networkidle' });
    await page.locator('input[type="tel"], input[name="mobile"], #mobile').first().fill(adminMobile);
    await page.locator('input[type="password"]').first().fill(adminPassword);
    await Promise.all([
      page.waitForURL(/\/admin(?!\/login)/i, { timeout: 60_000 }),
      page.locator('button[type="submit"]').first().click()
    ]);
  }

  test('1: both purchase CTAs stay inside the viewport with 14 variants', async ({ page, request }) => {
    const slug = await createManyVariantProduct(request, 14);
    await page.setViewportSize({ width: 1440, height: 830 });
    await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });

    // At the top of the page and while scrolling through the purchase zone, both actions are on
    // screen - the failure mode was a sticky column taller than the viewport whose lower half
    // (the buttons) could never be scrolled into view at all.
    for (const y of [0, 400]) {
      await page.evaluate(scroll => window.scrollTo(0, scroll), y);
      await page.waitForTimeout(200);
      const geometry = await page.evaluate(() => {
        const buttons = Array.from(document.querySelectorAll<HTMLElement>('.st-buy__card .st-btn'))
          .filter(b => /افزودن به سبد|خرید فوری/.test(b.textContent ?? ''));
        return buttons.map(b => {
          const r = b.getBoundingClientRect();
          return { bottom: r.bottom, top: r.top, viewport: window.innerHeight };
        });
      });
      expect(geometry.length).toBeGreaterThanOrEqual(2);
      for (const g of geometry) {
        expect(g.bottom).toBeLessThanOrEqual(g.viewport);
        expect(g.bottom).toBeGreaterThan(0);
      }
    }
  });

  test('3: the home product row is titled "محصولات" and follows the default sort', async ({ page, request }) => {
    await page.goto('/', { waitUntil: 'networkidle' });
    const row = page.locator('section.st-section').filter({
      has: page.locator('.st-section__title', { hasText: /^\s*محصولات\s*$/ })
    });
    await expect(row).toHaveCount(1);
    await expect(row.locator('.st-section__more')).toHaveAttribute('href', '/shop');

    const homeOrder = await row.locator('.st-pcard__title').allTextContents();
    const api = await request.get(`${apiBaseUrl}/products?pageSize=8`);
    const apiOrder = ((await api.json()).data.items as Array<{ title: string }>).map(p => p.title);
    expect(homeOrder.map(t => t.trim())).toEqual(apiOrder.slice(0, homeOrder.length));
  });

  test('4: after logout the login button appears, stays, and works - with an admin session present', async ({ page }) => {
    await loginAdminUi(page);
    await loginCustomer(page);

    await page.goto('/', { waitUntil: 'networkidle' });
    await page.locator('.st-avatar').click();
    await Promise.all([
      page.waitForURL(/\/$/, { timeout: 30_000 }),
      page.locator('form[action="/auth/customer/logout"]:visible button[type="submit"]').first().click()
    ]);

    // The old defect: once the circuit booted (about a second), it adopted the ADMIN identity and
    // the header flipped back to a signed-in avatar. Give it ample time to do that, then insist
    // the header still shows the login action.
    const loginButton = page.locator('.st-header a[href="/login"]');
    await expect(loginButton).toBeVisible();
    await page.waitForTimeout(2500);
    await expect(loginButton).toBeVisible();
    await expect(page.locator('.st-avatar')).toHaveCount(0);

    await loginButton.click();
    await expect(page).toHaveURL(/\/login/);
    await expect(page.locator('form[action="/auth/customer/login"]')).toBeVisible();

    // The admin area is untouched by the customer's logout.
    await page.goto('/admin/dashboard', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/admin\/dashboard/);
  });

  test('5: the category mega menu previews products and always offers "see all"', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });
    await page.getByTestId('catmenu-trigger').hover();
    const menu = page.getByTestId('catmenu');
    await expect(menu).toBeVisible();

    await expect(menu.locator('.st-catmenu__all')).toHaveAttribute('href', '/categories');
    const categories = menu.locator('.st-catmenu__cat');
    expect(await categories.count()).toBeGreaterThan(0);

    await categories.first().hover();
    const slug = (await categories.first().getAttribute('href'))!;
    // The panel resolves to either a product preview with a "see all" tile or an honest empty
    // state - never a blank pane.
    await expect(menu.locator('.st-catmenu__seeall, .st-catmenu__empty').first()).toBeVisible({ timeout: 15_000 });
    if (await menu.locator('.st-catmenu__seeall').count() > 0) {
      await expect(menu.locator('.st-catmenu__seeall')).toHaveAttribute('href', slug);
      expect(await menu.locator('.st-catmenu__item').count()).toBeGreaterThan(0);
    }

    // Clicking the trigger again closes it; the page underneath stays usable.
    await page.getByTestId('catmenu-trigger').click();
    await expect(menu).toHaveCount(0);
  });

  test('6: maintenance keeps the storefront closed for an admin-cookied browser too', async ({ page, request }) => {
    const token = await adminToken(request);
    const setMaintenance = (enabled: boolean) => request.post(`${apiBaseUrl}/admin/settings`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { key: 'MaintenanceMode', value: enabled ? 'true' : 'false', groupName: 'General', valueType: 'bool', description: 'maintenance' }
    });

    await loginAdminUi(page);
    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    const toggle = page.getByTestId('setting-MaintenanceMode').locator('input[type="checkbox"]');
    if (!(await toggle.isChecked())) await toggle.evaluate((el: HTMLInputElement) => el.click());
    await expect.poll(async () => {
      const response = await request.get(`${apiBaseUrl}/settings/public`);
      const settings = (await response.json()).data as Array<{ key: string; value: string }>;
      return settings.find(x => x.key === 'MaintenanceMode')?.value;
    }, { timeout: 15_000 }).toBe('true');

    try {
      const response = await page.goto('/shop', { waitUntil: 'networkidle' });
      expect(response?.status()).toBe(503);
      await expect(page.locator('.st-errpage')).toBeVisible();

      // The old leak: the booting circuit exempted the admin identity and swapped the maintenance
      // page for the live shop after a couple of seconds. Wait past that window and re-assert.
      await page.waitForTimeout(4000);
      await expect(page.locator('.st-errpage')).toBeVisible();
      await expect(page.locator('.st-pcard__title')).toHaveCount(0);

      // The status code renders BELOW the mascot, never on its face.
      const overlap = await page.evaluate(() => {
        const code = document.querySelector('.st-errpage__code');
        const mascot = document.querySelector('.st-mascot');
        if (!code || !mascot) return null;
        return code.getBoundingClientRect().top < mascot.getBoundingClientRect().bottom - 4;
      });
      expect(overlap).toBe(false);

      // A customer sign-in attempted DURING maintenance is refused outright, not half-completed.
      // It used to issue tokens and set the cookie, then bounce into the maintenance page - which
      // read as "login is broken". The POST now redirects to the maintenance page and no customer
      // session cookie is created.
      // Credentials are irrelevant: the refusal must happen before the API is ever consulted.
      const signIn = await request.post('/auth/customer/login', {
        form: { mobile: '09120000000', password: 'irrelevant-Aa1!', returnUrl: '' },
        maxRedirects: 0
      });
      expect([302, 303]).toContain(signIn.status());
      expect(signIn.headers()['location']).toBe('/');
      expect(signIn.headers()['set-cookie'] ?? '').not.toContain('Vitorize.Customer.Auth');

      // The admin panel itself stays reachable - that is where maintenance is operated from.
      await page.goto('/admin/settings', { waitUntil: 'networkidle' });
      await expect(page.getByTestId('setting-MaintenanceMode')).toBeVisible();
    } finally {
      // Off through the admin UI, because only that path invalidates the Web's settings cache
      // immediately; the direct API write is the belt-and-braces fallback for a mid-test failure.
      try {
        await page.goto('/admin/settings', { waitUntil: 'networkidle' });
        const off = page.getByTestId('setting-MaintenanceMode').locator('input[type="checkbox"]');
        if (await off.isChecked()) await off.evaluate((el: HTMLInputElement) => el.click());
        await page.waitForTimeout(1500);
      } catch { /* the API fallback below still reopens the shop */ }
      await setMaintenance(false);
      await expect.poll(async () => {
        const response = await request.get(`${apiBaseUrl}/settings/public`);
        const settings = (await response.json()).data as Array<{ key: string; value: string }>;
        return settings.find(x => x.key === 'MaintenanceMode')?.value;
      }, { timeout: 15_000 }).toBe('false');
    }
  });

  test('8: trust seals render once as styled badges, scripts split out, and none of it loads during maintenance', async ({ page, request }) => {
    const token = await adminToken(request);
    const saveSetting = (key: string, value: string, valueType: string, groupName: string) =>
      request.post(`${apiBaseUrl}/admin/settings`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { key, value, groupName, valueType, description: key }
      });
    // The exact provider shapes an administrator pastes: two anchor badges, the Zarinpal
    // document.write script (inert under Blazor), and an unrelated widget script.
    const pasted = [
      `<a referrerpolicy='origin' target='_blank' href='https://trustseal.enamad.ir/?id=1'><img referrerpolicy='origin' src='https://trustseal.enamad.ir/logo.aspx?id=1' alt=''></a>`,
      `<a href='https://emalls.ir/Shop/1/' target='_blank'><img width='75' height='112' src='https://service.emalls.ir/neshan?id=1'/></a>`,
      `<script src="https://www.zarinpal.com/webservice/TrustCode" type="text/javascript"></script>`,
      `<script>window.__vzSealProbe = 1;</script>`
    ].join('\n');

    await loginAdminUi(page);
    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    await page.locator('.vz-settab').nth(4).click();
    const field = page.getByTestId('setting-TrustSeal.FooterHtml');
    await field.locator('textarea').fill(pasted);
    await field.locator('button.vz-btn').click(); // the UI save is what invalidates the Web's branding cache
    await expect(field.locator('.vz-saved-tick')).toBeVisible();

    try {
      await page.goto('/', { waitUntil: 'networkidle' });
      const box = page.locator('.st-footer__seals');
      await expect(box).toHaveCount(1);
      // The styled box holds the two pasted badges plus the substituted static Zarinpal badge...
      await expect(box.locator('a[href*="trustseal.enamad.ir"]')).toHaveCount(1);
      await expect(box.locator('a[href*="emalls.ir"]')).toHaveCount(1);
      await expect(box.locator('a[href*="zarinpal.com/trustPage"]')).toHaveCount(1);
      await expect(box.locator('script')).toHaveCount(0);
      // ...and no second, unstyled copy exists anywhere else on the page.
      await expect(page.locator('img[src*="enamad"]')).toHaveCount(1);
      await expect(page.locator('img[src*="emalls"]')).toHaveCount(1);
      // The pasted widget script still executes - once, from the end of the body.
      await expect.poll(() => page.evaluate(() => (window as unknown as { __vzSealProbe?: number }).__vzSealProbe ?? null))
        .toBe(1);

      // While the shop is closed, none of it loads: no badges, no third-party scripts.
      await page.goto('/admin/settings', { waitUntil: 'networkidle' });
      const toggle = page.getByTestId('setting-MaintenanceMode').locator('input[type="checkbox"]');
      if (!(await toggle.isChecked())) await toggle.evaluate((el: HTMLInputElement) => el.click());
      await expect.poll(async () => {
        const response = await request.get(`${apiBaseUrl}/settings/public`);
        const settings = (await response.json()).data as Array<{ key: string; value: string }>;
        return settings.find(x => x.key === 'MaintenanceMode')?.value;
      }, { timeout: 15_000 }).toBe('true');

      const closed = await page.goto('/shop', { waitUntil: 'networkidle' });
      expect(closed?.status()).toBe(503);
      await expect(page.locator('.st-footer__seals')).toHaveCount(0);
      await expect(page.locator('img[src*="enamad"], img[src*="emalls"], a[href*="zarinpal.com/trustPage"]')).toHaveCount(0);
      expect(await page.evaluate(() => (window as unknown as { __vzSealProbe?: number }).__vzSealProbe ?? null)).toBeNull();
    } finally {
      // UI FIRST: only the admin-UI save invalidates the Web's cached flag. Restoring via the API
      // first leaves the checkbox already-off, the click never happens, and the Web serves the
      // maintenance page to the NEXT tests until the cache TTL expires.
      await page.goto('/admin/settings', { waitUntil: 'networkidle' });
      const toggle = page.getByTestId('setting-MaintenanceMode').locator('input[type="checkbox"]');
      if (await toggle.isChecked()) await toggle.evaluate((el: HTMLInputElement) => el.click());
      await page.locator('.vz-settab').nth(4).click();
      const sealField = page.getByTestId('setting-TrustSeal.FooterHtml');
      await sealField.locator('textarea').fill('');
      await sealField.locator('button.vz-btn').click();
      await expect(sealField.locator('.vz-saved-tick')).toBeVisible();
      // Belt and braces on the stored values, then prove the storefront actually reopened.
      await saveSetting('MaintenanceMode', 'false', 'bool', 'General');
      await saveSetting('TrustSeal.FooterHtml', '', 'trustedhtml', 'TrustSeals');
      const reopened = await page.goto('/', { waitUntil: 'networkidle' });
      expect(reopened?.status()).toBe(200);
    }
  });

  test('9: the verification birth-date picker registers every tap on a phone-width screen', async ({ page }) => {
    // THE DEFECT: the phone-width override put the calendar in static flow, which nullifies its
    // z-index - so the picker's transparent close-backdrop sat over the whole calendar and every
    // tap inside it (year, month, day) just closed the panel without registering anything.
    await registerCustomer(page, uniqueCustomer('Datepicker Mobile'));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/customer/verification', { waitUntil: 'networkidle' });

    await page.locator('.st-datepicker .vz-datepicker__toggle').click();
    const calendar = page.locator('.vz-cal');
    await expect(calendar).toBeVisible();

    const selects = calendar.locator('select');
    await selects.first().selectOption({ index: 5 });
    await expect(calendar).toBeVisible();
    await selects.nth(1).selectOption({ index: 3 });
    await expect(calendar).toBeVisible();

    await calendar.locator('.vz-cal__day:not(.empty):not([disabled])').first().click();
    await expect(calendar).toBeHidden();
    await expect(page.getByTestId('persian-date-input')).not.toHaveValue('');
  });

  test('7: a failed guest-cart merge no longer hijacks a successful sign-in', async ({ page }) => {
    // A malformed guest token makes the API refuse the merge - the same outcome as an API hiccup at
    // the moment of login. The old flow redirected the ALREADY signed-in customer to
    // /cart?mergeError=1, which read as a broken login; the destination must stay theirs.
    await page.context().clearCookies();
    await page.context().addCookies([
      { name: 'Vitorize.GuestCart', value: 'not-a-well-formed-guest-token', url: 'http://localhost:5077' }
    ]);
    await loginCustomer(page); // asserts the landing URL is /customer/dashboard itself
    // The header's avatar specifically: the dashboard body renders its own .st-avatar span.
    await expect(page.locator('.st-header .st-avatar')).toBeVisible();
    expect(page.url()).not.toContain('mergeError');
  });
});

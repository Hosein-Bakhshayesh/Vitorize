import { expect, test } from '../framework/fixtures';
import {
  apiBaseUrl, adminMobile, adminPassword,
  loginAdmin, logoutCustomer, uniqueCustomer, registerCustomer
} from './support/app';
import type { APIRequestContext } from '@playwright/test';

/**
 * The final pre-deployment fixes, exercised in a real browser.
 *
 * Each block names the defect it guards, because several of these looked fine in a green suite: the
 * maintenance test only ever checked a page's layout and never switched the setting on, and the
 * logout test only ever ran in a browser holding a single cookie.
 */

async function adminToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${apiBaseUrl}/auth/login`, {
    data: { mobile: adminMobile, password: adminPassword }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json()).data.accessToken as string;
}

async function setMaintenance(request: APIRequestContext, token: string, enabled: boolean) {
  const response = await request.post(`${apiBaseUrl}/admin/settings`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      key: 'MaintenanceMode', value: enabled ? 'true' : 'false',
      groupName: 'General', valueType: 'bool', description: 'maintenance'
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

// ---------------------------------------------------------------- 1. variants and the purchase CTAs

test.describe('Purchase actions stay reachable @product @ui', () => {
  test.describe.configure({ timeout: 180_000 });

  /** A product with enough versions to reproduce the complaint, built through the admin API. */
  async function createManyVariantProduct(request: APIRequestContext, token: string): Promise<string> {
    const headers = { Authorization: `Bearer ${token}` };
    const key = `${Date.now().toString(36)}`;

    const categories = await request.get(`${apiBaseUrl}/admin/categories`, { headers });
    const categoryId = (await categories.json()).data[0].id;

    const created = await request.post(`${apiBaseUrl}/admin/products`, {
      headers,
      data: {
        title: `CTA Geometry ${key}`, slug: `cta-geometry-${key}`, categoryId,
        productType: 1, deliveryType: 2, basePrice: 150000, currencyType: 2,
        minOrderQuantity: 1, isActive: true, shortDescription: 'CTA geometry fixture.',
        features: [], inputFields: [], tagIds: []
      }
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const productId = (await created.json()).data.id as string;

    // Twelve versions: comfortably past the point where the selector is bounded.
    for (let index = 1; index <= 12; index += 1) {
      const variant = await request.post(`${apiBaseUrl}/admin/products/${productId}/variants`, {
        headers,
        data: {
          title: `نسخه ${index}`, sku: `CTA-${key}-${index}`, price: 150000 + index * 1000,
          value: `CTA-${key}-${index}`, stockMode: 1, stockQuantity: 25,
          isDefault: index === 1, isActive: true, sortOrder: index
        }
      });
      expect(variant.ok(), await variant.text()).toBeTruthy();
    }

    return `cta-geometry-${key}`;
  }

  test('both buy actions are on screen on desktop however many versions a product has', async ({ page, request }) => {
    const token = await adminToken(request);
    const slug = await createManyVariantProduct(request, token);

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
    await expect(page.locator('.st-buy__card')).toBeVisible();
    await expect(page.getByTestId('product-variants')).toBeVisible();

    const geometry = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll<HTMLElement>('.st-buy__card button'));
      const addToCart = buttons.find(b => /افزودن به سبد|ناموجود/.test(b.textContent ?? ''));
      const buyNow = buttons.find(b => /خرید فوری/.test(b.textContent ?? ''));
      const variants = document.querySelector<HTMLElement>('[data-testid="product-variants"]');
      return {
        viewport: window.innerHeight,
        addToCartBottom: addToCart?.getBoundingClientRect().bottom ?? null,
        buyNowBottom: buyNow?.getBoundingClientRect().bottom ?? null,
        documentWidth: document.documentElement.scrollWidth,
        viewportWidth: window.innerWidth,
        variantScrolls: variants ? variants.scrollHeight > variants.clientHeight + 1 : false
      };
    });

    expect(geometry.addToCartBottom).not.toBeNull();
    expect(geometry.buyNowBottom).not.toBeNull();
    // Both actions, not just the first: leaving "buy now" below the fold reproduces the complaint.
    expect(geometry.addToCartBottom!).toBeLessThanOrEqual(geometry.viewport);
    expect(geometry.buyNowBottom!).toBeLessThanOrEqual(geometry.viewport);
    // Twelve versions must be bounded and scroll inside themselves rather than growing the card.
    expect(geometry.variantScrolls).toBe(true);
    expect(geometry.documentWidth).toBeLessThanOrEqual(geometry.viewportWidth);
  });

  test('a phone gets both actions pinned above the bottom navigation', async ({ page, request }) => {
    const token = await adminToken(request);
    const slug = await createManyVariantProduct(request, token);

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });

    await expect(page.getByTestId('product-sticky-buy')).toBeVisible();
    await expect(page.getByTestId('sticky-add-to-cart')).toBeVisible();
    await expect(page.getByTestId('sticky-buy-now')).toBeVisible();

    const layout = await page.evaluate(() => {
      const rect = (selector: string) => document.querySelector<HTMLElement>(selector)?.getBoundingClientRect() ?? null;
      return {
        bar: rect('[data-testid="product-sticky-buy"]'),
        nav: rect('.st-bottomnav'),
        add: rect('[data-testid="sticky-add-to-cart"]'),
        buy: rect('[data-testid="sticky-buy-now"]'),
        viewport: window.innerHeight,
        documentWidth: document.documentElement.scrollWidth,
        viewportWidth: window.innerWidth
      };
    });

    // Reachable without scrolling past the version list...
    expect(layout.bar!.bottom).toBeLessThanOrEqual(layout.viewport + 1);
    // ...and clear of the bottom navigation rather than underneath it.
    if (layout.nav) expect(layout.bar!.bottom).toBeLessThanOrEqual(layout.nav.top + 1);
    expect(layout.add!.height).toBeGreaterThanOrEqual(44);
    expect(layout.buy!.height).toBeGreaterThanOrEqual(44);
    expect(layout.documentWidth).toBeLessThanOrEqual(layout.viewportWidth);

    // And it actually works, not merely renders.
    await page.getByTestId('sticky-add-to-cart').click();
    await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
  });
});

// ---------------------------------------------------------------- 2. one font everywhere

test.describe('Typography is one contract @ui', () => {
  test('Peyda is the Persian face on the storefront, the customer panel and the admin panel', async ({ page, loginAs }) => {
    const family = (selector: string) => page.evaluate(
      s => getComputedStyle(document.querySelector(s)!).fontFamily, selector);

    await page.goto('/shop', { waitUntil: 'networkidle' });
    expect(await family('body')).toContain('Peyda');
    // Native controls do not inherit typography unless told to; the variant cards and the quantity
    // stepper were the ones rendering in the browser's own font beside Persian text.
    expect(await family('input')).toContain('Peyda');

    await loginAs('SuperAdmin');
    await page.goto('/admin/products', { waitUntil: 'networkidle' });
    // The admin panel had its own hard-coded face and never declared Peyda at all, so no setting
    // could have reached it. This is the assertion that would have caught that.
    expect(await family('body')).toContain('Peyda');
    expect(await family('select')).toContain('Peyda');
  });
});

// ---------------------------------------------------------------- 3. logout

test.describe('Customer logout @customer @security', () => {
  test.describe.configure({ timeout: 180_000 });

  test('signs the customer out even when an admin session exists in the same browser', async ({ page }) => {
    // THE DEFECT: with both cookies present, a storefront request resolved to the admin identity once
    // the customer cookie was deleted, so the header still rendered as signed in and the logout looked
    // like it had done nothing. A browser holding only one cookie never showed it.
    await loginAdmin(page);
    await expect(page).toHaveURL(/\/admin/);

    await registerCustomer(page, uniqueCustomer('Logout Both'));
    await page.goto('/customer/dashboard', { waitUntil: 'networkidle' });
    await expect(page.locator('form[action="/auth/customer/logout"]').first()).toBeAttached();

    await logoutCustomer(page);

    // Signed out of the shop...
    await page.goto('/customer/profile', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);

    // ...while the admin session is untouched.
    await page.goto('/admin/dashboard', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/admin\/dashboard/);
  });

  test('stays signed out across a reload and the back button', async ({ page }) => {
    await registerCustomer(page, uniqueCustomer('Logout Back'));
    await page.goto('/customer/dashboard', { waitUntil: 'networkidle' });
    await logoutCustomer(page);

    await page.reload({ waitUntil: 'networkidle' });
    await page.goto('/customer/dashboard', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);

    await page.goBack();
    await page.goto('/customer/orders', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);
  });
});

// ---------------------------------------------------------------- 6. maintenance mode

test.describe('Maintenance mode @admin @security', () => {
  test.describe.configure({ timeout: 180_000 });

  /**
   * Flips the switch the way an administrator does.
   *
   * Deliberately not the API: the Web process caches the public settings, and only the admin save path
   * invalidates that cache. Saving straight to the API leaves the storefront serving the previous
   * value until the cache expires, so a test that used the API would be testing a path no
   * administrator takes and would report a shop that is still open.
   */
  async function toggleMaintenanceInAdmin(
    page: import('@playwright/test').Page, request: APIRequestContext, enabled: boolean
  ) {
    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    const toggle = page.getByTestId('setting-MaintenanceMode').locator('input[type="checkbox"]');
    await expect(toggle).toBeAttached();
    if (await toggle.isChecked() !== enabled) {
      await toggle.evaluate((element: HTMLInputElement) => element.click());
    }
    await expect(toggle).toBeChecked({ checked: enabled });

    // A bool setting saves on toggle and marks itself saved rather than raising a toast, so wait on
    // the stored value instead. Once the API reports it, the save has returned - and the same handler
    // invalidated the Web's settings cache on its way back.
    await expect.poll(async () => {
      const response = await request.get(`${apiBaseUrl}/settings/public`);
      const settings = (await response.json()).data as Array<{ key: string; value: string }>;
      return settings.find(x => x.key === 'MaintenanceMode')?.value;
    }, { timeout: 15_000 }).toBe(enabled ? 'true' : 'false');
  }

  test('closes the shop, keeps the admin panel usable, and can be switched back off', async ({ page, browser, request, loginAs }) => {
    const token = await adminToken(request);
    await loginAs('SuperAdmin');
    await toggleMaintenanceInAdmin(page, request, true);

    try {
      // A separate browser, because this one holds an admin cookie and administrators are deliberately
      // exempt - they have to be, or enabling maintenance would lock out the person who can disable it.
      const shopper = await browser.newContext();
      try {
        const shopPage = await shopper.newPage();
        const response = await shopPage.goto('/shop', { waitUntil: 'networkidle' });
        // Genuinely closed, not merely wearing a 503 header over a working shop.
        expect(response?.status()).toBe(503);
        await expect(shopPage.locator('.st-errpage')).toBeVisible();
      } finally {
        await shopper.close();
      }

      // Purchasing is refused at the API too, so a page opened before the switch cannot buy.
      const blocked = await request.post(`${apiBaseUrl}/cart/items`, { data: {} });
      expect(blocked.status()).toBe(503);

      // THE OTHER HALF: the admin panel shares one Blazor transport with the storefront, so blocking
      // that endpoint would have taken the admin UI down too - including the page that reverses this.
      await page.goto('/admin/products', { waitUntil: 'networkidle' });
      await expect(page.getByTestId('storefront-default-sort')).toBeVisible();
      await page.getByTestId('storefront-default-sort-select').selectOption('Newest');
      await page.getByTestId('storefront-default-sort-save').click();
      await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
    } finally {
      await toggleMaintenanceInAdmin(page, request, false);
      // Belt and braces: if the UI path failed midway, make sure the shop is not left closed.
      await setMaintenance(request, token, false);
      // Leave the catalogue ordering as the suite expects to find it.
      await request.post(`${apiBaseUrl}/admin/settings`, {
        headers: { Authorization: `Bearer ${token}` },
        data: {
          key: 'StorefrontDefaultProductSort', value: 'AvailabilityFirst',
          groupName: 'General', valueType: 'sortmode', description: 'default order'
        }
      });
    }

    await page.goto('/shop', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-pcard__title').first()).toBeVisible();
  });
});

// ---------------------------------------------------------------- 8. the default sort is an enum

test.describe('Storefront default sort is a fixed set @admin', () => {
  test('the generic Settings screen offers a select, not a text box', async ({ page, loginAs }) => {
    await loginAs('SuperAdmin');
    await page.goto('/admin/settings', { waitUntil: 'networkidle' });

    // It rendered as free text because its ValueType was a plain "string", which the renderer does not
    // recognise and falls back to an input for.
    const select = page.getByTestId('setting-storefront-default-sort');
    await expect(select).toBeVisible();
    expect(await select.evaluate(el => el.tagName.toLowerCase())).toBe('select');

    const options = await select.locator('option').allTextContents();
    expect(options).toContain('موجودها اول');
    expect(options).toContain('پرفروش‌ترین');
    // No popularity metric exists, so no popularity option may be offered.
    expect(options.join('|')).not.toContain('محبوب‌ترین');
  });
});

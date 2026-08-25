import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, loginAdmin } from './support/app';

/**
 * The administrator picks one default order for the storefront; a customer who asks for a different
 * order still gets it.
 *
 * Ordering is asserted against the public API's own response for the same mode rather than a
 * hard-coded list, so the test proves the page follows the configured mode without re-encoding the
 * catalogue. Availability is read from the card's out-of-stock badge, which is rendered from the
 * canonical availability rules, not from a stock number.
 */
test.describe('Storefront default product sort @storefrontsort', () => {
  test.describe.configure({ timeout: 180_000 });

  async function setDefaultSort(page: import('@playwright/test').Page, code: string) {
    await page.goto('/admin/products', { waitUntil: 'networkidle' });
    const control = page.getByTestId('storefront-default-sort');
    await expect(control).toBeVisible();
    await control.getByTestId('storefront-default-sort-select').selectOption(code);
    await control.getByTestId('storefront-default-sort-save').click();
    await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
  }

  /** Product titles in the order the shop renders them. */
  async function shopOrder(page: import('@playwright/test').Page, query = '') {
    await page.goto(`/shop${query}`, { waitUntil: 'networkidle' });
    await expect(page.locator('.st-pcard__title').first()).toBeVisible();
    return page.locator('.st-pcard__title').allTextContents();
  }

  /** The same listing straight from the API, used as the oracle for each mode. */
  async function apiOrder(request: import('@playwright/test').APIRequestContext, sort: string) {
    const response = await request.get(`${apiBaseUrl}/products?page=1&pageSize=24&sort=${sort}`);
    expect(response.ok(), await response.text()).toBeTruthy();
    return ((await response.json()).data.items as Array<{ title: string }>).map(x => x.title);
  }

  test('the saved default drives the shop, and an explicit customer sort overrides it', async ({ page, request }) => {
    await loginAdmin(page);

    // ---- availability first: every out-of-stock product must sit after every in-stock one
    await setDefaultSort(page, 'AvailabilityFirst');
    // The shop's first page follows the configured default order...
    expect(await shopOrder(page)).toEqual(await apiOrder(request, 'availability'));
    // ...and across the WHOLE catalogue the availability boundary is monotone. The available set
    // comes from the server's own inStock filter, so the availability rule is never re-encoded
    // here - and the check no longer depends on an out-of-stock product happening to fit on the
    // first page, which stopped being true as the suite's fixture pool grew.
    const everything = (((await (await request.get(`${apiBaseUrl}/products?page=1&pageSize=200`)).json())
      .data.items) as Array<{ title: string }>).map(x => x.title);
    const inStock = new Set((((await (await request.get(`${apiBaseUrl}/products?page=1&pageSize=200&inStock=true`)).json())
      .data.items) as Array<{ title: string }>).map(x => x.title));
    const available = everything.map(title => inStock.has(title));
    expect(available).toContain(true);
    expect(available).toContain(false);
    expect(available.lastIndexOf(true)).toBeLessThan(available.indexOf(false));

    // ---- best selling: the only real ranking signal Vitorize stores
    await setDefaultSort(page, 'BestSelling');
    expect(await shopOrder(page)).toEqual(await apiOrder(request, 'bestselling'));

    // ---- newest
    await setDefaultSort(page, 'Newest');
    const newest = await apiOrder(request, 'newest');
    expect(await shopOrder(page)).toEqual(newest);

    // ---- an explicit customer choice wins over the saved default
    expect(await shopOrder(page, '?sort=cheapest')).toEqual(await apiOrder(request, 'cheapest'));

    // ---- dropping the explicit choice returns the customer to the saved default
    expect(await shopOrder(page)).toEqual(newest);

    // ---- restore the QA default
    await setDefaultSort(page, 'AvailabilityFirst');
    expect(await shopOrder(page)).toEqual(await apiOrder(request, 'availability'));
  });

  test('the control configures the storefront only and leaves the admin grid alone', async ({ page }) => {
    await loginAdmin(page);
    await page.goto('/admin/products', { waitUntil: 'networkidle' });
    const rows = page.locator('tbody tr');
    await expect(rows.first()).toBeVisible();
    const before = await rows.allTextContents();

    await setDefaultSort(page, 'PriceHighToLow');
    await page.goto('/admin/products', { waitUntil: 'networkidle' });
    await expect(rows.first()).toBeVisible();
    expect(await rows.allTextContents()).toEqual(before);

    // The saved value survives a reload of the product list.
    await expect(page.getByTestId('storefront-default-sort-select')).toHaveValue('PriceHighToLow');

    await setDefaultSort(page, 'AvailabilityFirst');
    await page.goto('/admin/products', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('storefront-default-sort-select')).toHaveValue('AvailabilityFirst');
  });

  test('the customer sort menu names the order the listing is actually in', async ({ page }) => {
    await loginAdmin(page);
    await setDefaultSort(page, 'Oldest');

    await page.goto('/shop', { waitUntil: 'networkidle' });
    // No explicit choice, so the menu must say the configured default rather than something else.
    await expect(page.locator('.st-sort')).toContainText('قدیمی‌ترین');

    await page.goto('/shop?sort=cheapest', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-sort')).toContainText('ارزان‌ترین');

    await setDefaultSort(page, 'AvailabilityFirst');
  });
});

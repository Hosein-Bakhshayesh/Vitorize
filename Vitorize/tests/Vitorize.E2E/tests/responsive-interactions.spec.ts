import { test, expect } from '../framework/fixtures';
import { auditResponsivePage, type ResponsiveRoute } from '../framework/responsive';

const interactionProjects = new Set([
  'phone-320-light', 'phone-390-dark', 'phone-landscape-667-light',
  'tablet-768-light', 'tablet-landscape-1024-light', 'desktop-1440-dark'
]);

const route = (path: string, component: string, interactions: string[]): ResponsiveRoute => ({
  route: path, path, component, persona: 'SuperAdmin', interactions, highRisk: true
});

test.describe('@responsive @regression @release responsive interactions and overlays', () => {
  test.describe.configure({ timeout: 180_000 });

  test('storefront header, product media and checkout product information stay reachable @mobile @overflow', async ({ page, loginAs }, testInfo) => {
    test.skip(!interactionProjects.has(testInfo.project.name));
    await loginAs('Customer');
    await page.goto('/product/e2e-seo-product', { waitUntil: 'domcontentloaded' });

    const headerActions = page.locator('.st-header__actions');
    await expect(headerActions).toBeVisible();
    const headerBox = await headerActions.evaluate(element => {
      const rect = element.getBoundingClientRect();
      return { x: rect.x, width: rect.width };
    });
    expect(headerBox.x).toBeGreaterThanOrEqual(-1);
    expect(headerBox.x + headerBox.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1);

    const gallery = page.locator('.st-gal__main');
    await expect(gallery).toBeVisible();
    if (page.viewportSize()!.width <= 560) {
      const galleryBox = await gallery.boundingBox();
      expect(galleryBox!.height).toBeLessThanOrEqual(261);
    }

    if (page.viewportSize()!.width <= 1000) {
      const responsiveOrder = await page.evaluate(() => {
        const gallery = document.querySelector('.st-gal')!.getBoundingClientRect();
        const buy = document.querySelector('.st-buy')!.getBoundingClientRect();
        const features = document.querySelector('.st-product-features')?.getBoundingClientRect();
        return { galleryBottom: gallery.bottom, buyTop: buy.top, buyBottom: buy.bottom, featuresTop: features?.top ?? Number.POSITIVE_INFINITY };
      });
      expect(responsiveOrder.buyTop, JSON.stringify(responsiveOrder)).toBeGreaterThanOrEqual(responsiveOrder.galleryBottom - 1);
      expect(responsiveOrder.buyBottom, JSON.stringify(responsiveOrder)).toBeLessThanOrEqual(responsiveOrder.featuresTop + 1);
    }

    const buy = page.locator('.st-buy__card button.st-btn--accent');
    await expect(buy).toHaveCount(1);
    await buy.click();
    // Adding to the cart is immediate now; product information is collected at checkout instead.
    await expect(page.locator('.vz-toast.success, .vz-toast--success').first()).toBeVisible();
    await page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('checkout-product-inputs')).toBeVisible();
    await auditResponsivePage(page, testInfo, {
      route: { route: '/checkout', path: '/checkout', component: 'Store/Checkout product information', persona: 'Customer', interactions: ['checkout product information'] },
      viewport: `${page.viewportSize()!.width}x${page.viewportSize()!.height}`,
      theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
    });
  });

  test('Admin shell, drawer and product editor shrink below intrinsic CKEditor width @mobile @overflow', async ({ page, loginAs }, testInfo) => {
    test.skip(!interactionProjects.has(testInfo.project.name));
    await loginAs('SuperAdmin');
    await page.goto('/admin/products/31000000-0000-0000-0000-000000000002', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vz-ck .ck-editor__editable_inline')).toHaveCount(1);

    const title = page.getByTestId('product-title');
    await expect(title).toBeVisible();
    const geometry = await title.evaluate(element => {
      const input = element.getBoundingClientRect();
      const grid = element.closest('.vz-form-grid')!.getBoundingClientRect();
      return { input: { left: input.left, right: input.right, width: input.width }, grid: { left: grid.left, right: grid.right, width: grid.width } };
    });
    expect(geometry.input.left).toBeGreaterThanOrEqual(geometry.grid.left - 1);
    expect(geometry.input.right).toBeLessThanOrEqual(geometry.grid.right + 1);
    expect(geometry.input.width).toBeLessThanOrEqual(geometry.grid.width + 1);

    const topbarActions = page.locator('.vz-topbar__actions');
    const topbarBox = await topbarActions.boundingBox();
    expect(topbarBox!.x).toBeGreaterThanOrEqual(-1);
    expect(topbarBox!.x + topbarBox!.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1);

    if (page.viewportSize()!.width <= 900) {
      const menu = page.getByRole('button', { name: 'منو' });
      await expect(menu).toHaveCount(1);
      await menu.click();
      await expect(page.locator('.vz-sidebar.open')).toBeVisible();
      await expect.poll(async () => {
        const box = await page.locator('.vz-sidebar.open').boundingBox();
        return box ? Math.round(box.x + box.width) : Number.POSITIVE_INFINITY;
      }).toBeLessThanOrEqual(page.viewportSize()!.width + 1);
      await auditResponsivePage(page, testInfo, {
        route: route('/admin/products/{Id:guid}', 'Admin/ProductEdit drawer', ['mobile drawer']),
        viewport: `${page.viewportSize()!.width}x${page.viewportSize()!.height}`,
        theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
      });
      await page.locator('.vz-sidebar__backdrop.show').click({ position: { x: 5, y: 5 } });
      await expect(page.locator('.vz-sidebar.open')).toHaveCount(0);
    }
  });

  test('CKEditor toolbar and full icon picker remain locally scrollable and viewport-bound @mobile', async ({ page, loginAs }, testInfo) => {
    test.skip(!interactionProjects.has(testInfo.project.name));
    await loginAs('SuperAdmin');
    await page.goto('/admin/products/create', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vz-ck .ck-editor__editable_inline')).toHaveCount(1);
    await expect(page.locator('.vz-ck .ck-toolbar')).toBeVisible();

    await page.getByTestId('add-product-feature').click();
    const triggers = page.getByTestId('icon-picker-trigger');
    await expect(triggers).toHaveCount(1);
    await triggers.first().click();
    const picker = page.locator('.vz-icon-picker[open]');
    await expect(picker).toBeVisible();
    const pickerBox = await picker.boundingBox();
    expect(pickerBox!.x).toBeGreaterThanOrEqual(-1);
    expect(pickerBox!.y).toBeGreaterThanOrEqual(-1);
    expect(pickerBox!.x + pickerBox!.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1);
    expect(pickerBox!.y + pickerBox!.height).toBeLessThanOrEqual(page.viewportSize()!.height + 1);

    await page.getByTestId('icon-picker-search').fill('wallet');
    await expect(page.locator('.vz-icon-picker__cell').first()).toBeVisible();
    await auditResponsivePage(page, testInfo, {
      route: route('/admin/products/create', 'Shared/LucideIconPicker', ['search', 'collections', 'icon grid', 'sticky actions']),
      viewport: `${page.viewportSize()!.width}x${page.viewportSize()!.height}`,
      theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
    });
  });

  test('all 17 Settings tabs keep values and save controls accessible @mobile @tablet', async ({ page, loginAs }, testInfo) => {
    test.skip(!interactionProjects.has(testInfo.project.name));
    await loginAs('SuperAdmin');
    await page.goto('/admin/settings', { waitUntil: 'domcontentloaded' });
    const tabs = page.locator('.vz-settab');
    await expect(tabs).toHaveCount(17);

    for (let index = 0; index < 17; index++) {
      await tabs.nth(index).click();
      await expect(tabs.nth(index)).toHaveClass(/active/);
      await expect(page.locator('.vz-setfield').first()).toBeVisible();
      await auditResponsivePage(page, testInfo, {
        route: route('/admin/settings', `Admin/Settings tab ${index + 1}`, ['setting values', 'save controls']),
        viewport: `${page.viewportSize()!.width}x${page.viewportSize()!.height}`,
        theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
      });
    }
  });
});

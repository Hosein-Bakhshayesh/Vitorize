import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow, loginAdmin, monitorBrowser } from './support/app';

const productId = '31000000-0000-0000-0000-000000000002';
const instantProductId = '31000000-0000-0000-0000-000000000011';

test('admin routes require authentication and reject customer-area assumptions', async ({ page }) => {
  await page.goto('/admin/products');
  await expect(page).toHaveURL(/\/admin\/login/);
  await page.goto('/admin/settings');
  await expect(page).toHaveURL(/\/admin\/login/);
});

test('SuperAdmin can navigate every operational admin surface without browser errors or overflow', async ({ page }) => {
  const browser = monitorBrowser(page);
  await loginAdmin(page);
  const routes = [
    '/admin/dashboard', '/admin/categories', '/admin/brands', '/admin/products',
    `/admin/products/${productId}/details`, `/admin/products/${productId}/images`,
    '/admin/product-tags', '/admin/coupons', '/admin/orders', '/admin/gift-codes',
    '/admin/wallets', '/admin/payments', '/admin/users', '/admin/roles',
    '/admin/verifications', '/admin/tickets', '/admin/reviews', '/admin/notifications',
    '/admin/sms', '/admin/monitoring', '/admin/settings', '/admin/banners',
    '/admin/reports', '/admin/audit-logs', '/admin/security-logs', '/admin/error-logs', '/admin/tools'
  ];

  for (const route of routes) {
    await page.goto(route, { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(new RegExp(route.replaceAll('/', '\\/')));
    await expect(page.locator('.vz-content')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
    await expectRtlAndNoOverflow(page);
  }
  browser.assertClean();
});

test('admin can create, filter and delete an isolated category through the real drawer form', async ({ page }) => {
  await loginAdmin(page);
  await page.goto('/admin/categories', { waitUntil: 'networkidle' });
  const title = `E2E Category ${Date.now()}`;
  const slug = `e2e-category-${Date.now()}`;

  await page.locator('.vz-page-head button.vz-btn--primary').click();
  await expect(page.locator('.vz-slidepanel')).toBeVisible();
  const form = page.locator('#cat-form');
  await form.locator('input.vz-input').nth(0).fill(title);
  const slugInput = form.locator('input.vz-input').nth(1);
  await slugInput.fill(slug);
  await expect(page.locator('.vz-slidepanel__foot button[type="submit"]')).toBeEnabled();
  await slugInput.press('Enter');
  await expect(page.locator('.vz-slidepanel')).toBeHidden();
  await expect(page.locator('.vz-toast.success, .vz-toast--success')).toBeVisible();

  await page.locator('.vz-filterbar input.vz-input').fill(title);
  const row = page.locator('tbody tr').filter({ hasText: title });
  await expect(row).toHaveCount(1);
  await row.locator('button.danger').click();
  await expect(page.locator('.vz-dialog')).toBeVisible();
  await page.locator('.vz-dialog button.vz-btn--danger').click();
  await expect(row).toHaveCount(0);
});

test('global context menu portals above the grid, supports Escape and remains inside the viewport', async ({ page }) => {
  await loginAdmin(page);
  await page.goto('/admin/products', { waitUntil: 'networkidle' });
  await expect(page.locator('.vz-spinner:visible')).toHaveCount(0, { timeout: 20_000 });
  const trigger = page.locator('.vz-ctx__trigger').first();
  await expect(trigger).toBeVisible();
  await trigger.click();
  const menu = page.locator('.vz-ctx__menu:popover-open');
  await expect(menu).toBeVisible();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  const box = await menu.boundingBox();
  const viewport = page.viewportSize()!;
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width + 1);
  expect(box!.y + box!.height).toBeLessThanOrEqual(viewport.height + 1);

  await page.keyboard.press('Escape');
  await expect(menu).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('product editor initializes rich text, variants, features, dynamic fields and Lucide picker', async ({ page }) => {
  await loginAdmin(page);
  await page.goto(`/admin/products/${productId}`, { waitUntil: 'networkidle' });
  await expect(page.locator('.vz-rich-editor .ql-editor')).toBeVisible();
  await expect(page.locator('.vz-rich-editor .ql-editor')).toContainText('Rich E2E Description');
  const editorInputs = page.locator('.vz-builder-row input.vz-input');
  const featureInputIndex = await editorInputs.evaluateAll(inputs =>
    inputs.findIndex(input => (input as HTMLInputElement).value === 'Platform'));
  const dynamicFieldInputIndex = await editorInputs.evaluateAll(inputs =>
    inputs.findIndex(input => (input as HTMLInputElement).value === 'account_email'));
  expect(featureInputIndex).toBeGreaterThanOrEqual(0);
  expect(dynamicFieldInputIndex).toBeGreaterThanOrEqual(0);
  const featureRow = editorInputs.nth(featureInputIndex)
    .locator('xpath=ancestor::div[contains(@class,"vz-builder-row")]');
  const dynamicFieldRow = editorInputs.nth(dynamicFieldInputIndex)
    .locator('xpath=ancestor::div[contains(@class,"vz-builder-row")]');
  await expect(featureRow).toBeVisible();
  await expect(dynamicFieldRow).toBeVisible();
  await expect(page.locator('.vz-table').filter({ hasText: 'E2E Premium Variant' })).toBeVisible();

  await featureRow.locator('.vz-icon-field__trigger').click();
  const picker = page.locator('dialog.vz-icon-picker');
  await expect(picker).toBeVisible();
  await picker.locator('input[type="search"]').fill('wallet');
  await expect(picker.locator('.vz-icon-picker__cell-main').filter({ hasText: 'wallet' }).first()).toBeVisible();
  await picker.locator('.vz-icon-picker__cell-main').filter({ hasText: 'wallet' }).first().click();
  await picker.locator('footer button.vz-btn--primary').click();
  await expect(picker).toBeHidden();
  await expect(featureRow.locator('.vz-icon-field__trigger')).toContainText('wallet');
});

test('settings exposes branding uploads, typography preview, trust seals and only two SMS template IDs', async ({ page }) => {
  await loginAdmin(page);
  await page.goto('/admin/settings', { waitUntil: 'networkidle' });
  const tabs = page.locator('.vz-settab');
  await expect(tabs).toHaveCount(17);

  await tabs.nth(2).click();
  await expect(page.locator('input[type="file"]').first()).toBeAttached();
  await expect(page.locator('.vz-card__body')).toContainText(/FaviconPath|HeaderLogoPath/);

  await tabs.nth(3).click();
  await expect(page.locator('.vz-font-preview')).toBeVisible();
  await expect(page.locator('input[type="file"]')).toHaveAttribute('accept', /woff2/);
  await expect(page.locator('.vz-font-list')).toContainText('Vazirmatn');

  await tabs.nth(4).click();
  await expect(page.locator('.vz-card__body')).toContainText(/Trust|Enamad|اعتماد/);

  await tabs.nth(15).click();
  const keys = page.locator('.vz-setfield__key');
  await expect(keys.filter({ hasText: 'Sms.OtpTemplateId' })).toHaveCount(1);
  await expect(keys.filter({ hasText: 'Sms.NotificationTemplateId' })).toHaveCount(1);
  await expect(keys.filter({ hasText: 'Sms.LoginOtpTemplateId' })).toHaveCount(0);
  await expect(page.locator('.vz-card__body')).toContainText('CODE');
  await expect(page.locator('.vz-card__body')).toContainText('EXPIRE');
  await expect(page.locator('.vz-card__body')).toContainText('ORDER_NUMBER');
});

test('admin imports and removes an isolated encrypted gift-code batch', async ({ page }) => {
  await loginAdmin(page);
  await page.goto('/admin/gift-codes', { waitUntil: 'networkidle' });
  const title = `E2E Gift Batch ${Date.now()}`;
  await page.locator('.vz-page-head button.vz-btn--primary').click();
  const dialog = page.getByRole('dialog');
  await dialog.locator('select.vz-select').selectOption(instantProductId);
  await dialog.locator('input.vz-input').nth(0).fill(title);
  await dialog.locator('textarea.vz-textarea').fill(`E2E-CODE-${Date.now()}`);
  await dialog.locator('button.vz-btn--primary').click();
  await expect(dialog).toBeHidden();
  await expect(page.locator('.vz-toast.success')).toBeVisible();

  const row = page.locator('tbody tr').filter({ hasText: title });
  await expect(row).toHaveCount(1);
  await row.locator('button.danger').click();
  const confirm = page.getByRole('dialog');
  await confirm.locator('button.vz-btn--danger').click();
  await expect(row).toHaveCount(0);
});

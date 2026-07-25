import { test, expect, TAG } from '../framework/fixtures';

// CKEditor 5 integration + multi-collection icon picker.
// Runs against the live admin app; validates lifecycle, no duplicate instances,
// no console errors, and the namespaced icon-picker behaviour.
test.describe('CKEditor 5 and icon picker', () => {
  test.describe.configure({ timeout: 120_000 });

  test('editor initialises on create and edit without duplicates or console errors', {
    tag: [TAG.admin, TAG.product, TAG.ui, TAG.regression]
  }, async ({ page, loginAs, adminProduct }) => {
    const errors: string[] = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
    page.on('pageerror', err => errors.push(err.message));

    await loginAs('SuperAdmin');

    // Create page: exactly one editor, toolbar + chrome present.
    await adminProduct.openCreate();
    const editable = page.locator('.vz-ck .ck-editor__editable_inline');
    await expect(editable).toHaveCount(1);
    await expect(editable).toBeVisible();
    await expect(page.locator('.vz-ck .ck-toolbar')).toBeVisible();
    await expect(page.locator('.vz-ck__chrome .vz-ck__chrome-btn')).toHaveCount(2);

    // Type Persian + English; the model must accept both.
    await editable.click();
    await page.keyboard.type('توضیح فارسی and English 123');
    await expect(editable).toContainText('توضیح فارسی');
    await expect(editable).toContainText('English 123');

    // Navigate away and back — no leaked/duplicate editor instance.
    await page.goto('/admin/dashboard');
    await adminProduct.openCreate();
    await expect(page.locator('.vz-ck .ck-editor__editable_inline')).toHaveCount(1);

    expect(errors, `console errors: ${errors.join(' | ')}`).toHaveLength(0);
  });

  test('icon picker searches, filters collections and persists a namespaced identifier', {
    tag: [TAG.admin, TAG.product, TAG.ui, TAG.regression]
  }, async ({ page, loginAs, adminProduct }) => {
    await loginAs('SuperAdmin');
    await adminProduct.openCreate();

    // A feature row exposes the icon picker.
    await page.getByTestId('add-product-feature').click();
    await page.getByTestId('icon-picker-trigger').first().click();

    // Search returns matches in the default (Lucide) collection.
    await page.getByTestId('icon-picker-search').fill('wallet');
    await expect(page.locator('.vz-icon-picker__cell').first()).toBeVisible();
    await expect(page.getByTestId('icon-picker-empty')).toHaveCount(0);

    // Empty search state.
    await page.getByTestId('icon-picker-search').fill('zzzzz-no-such-icon');
    await expect(page.getByTestId('icon-picker-empty')).toBeVisible();

    // Switch to the Tabler collection and pick a brand glyph Lucide lacks.
    await page.getByTestId('icon-collection-tabler').click();
    await page.getByTestId('icon-picker-search').fill('steam');
    const steam = page.locator('.vz-icon-picker__cell', { hasText: 'brand-steam' }).first();
    await expect(steam).toBeVisible();
    await steam.locator('.vz-icon-picker__cell-main').click();
    await page.getByTestId('icon-picker-confirm').click();

    // The trigger now shows the stored namespaced identifier.
    await expect(page.getByTestId('icon-picker-trigger').first()).toContainText('tabler:brand-steam');
  });
});

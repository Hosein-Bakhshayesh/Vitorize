import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';
import type { ProductInput } from '../builders/ProductBuilder';

/** Admin product create/edit/details/gallery workflows. */
export class AdminProductPage extends BasePage {
  readonly form = this.page.getByTestId('admin-product-form');

  constructor(page: Page) { super(page); }

  async openCreate(): Promise<void> {
    await this.goto('/admin/products/create');
    await expect(this.form).toBeVisible();
  }

  async openEdit(productId: string): Promise<void> {
    await this.goto(`/admin/products/${productId}`);
    await expect(this.form).toBeVisible();
  }

  async fill(product: ProductInput): Promise<void> {
    await this.page.getByTestId('product-title').fill(product.title);
    await this.page.getByTestId('product-category').selectOption({ label: product.category });
    await this.page.getByTestId('product-brand').selectOption(product.brand ? { label: product.brand } : { value: '' });
    await this.page.getByTestId('product-type').selectOption(String(product.productType));
    await this.page.getByTestId(`product-delivery-${product.deliveryType}`).click();
    await this.page.getByTestId('product-currency').selectOption(String(product.currencyType));
    await this.page.getByTestId('product-base-price').fill(String(product.basePrice));
    await this.fillOptionalNumber('product-discount-price', product.discountPrice);
    await this.page.getByTestId('product-min-quantity').fill(String(product.minQuantity));
    await this.fillOptionalNumber('product-max-quantity', product.maxQuantity);
    await this.page.getByTestId('product-short-description').fill(product.shortDescription ?? '');
    await this.page.getByTestId('product-seo-title').fill(product.seoTitle ?? '');
    await this.page.getByTestId('product-seo-description').fill(product.seoDescription ?? '');
    await this.page.getByTestId('product-focus-keyword').fill(product.focusKeyword ?? '');
    await this.setHiddenCheckbox(this.page.getByTestId('product-active'), product.active);
    await this.setHiddenCheckbox(this.page.getByTestId('product-featured'), product.featured);

    if (product.htmlDescription !== undefined) {
      // CKEditor 5 owns its DOM; set content through its instance API so the
      // model, view and the Blazor-bound value all stay consistent.
      const editable = this.page.locator('.vz-ck .ck-editor__editable_inline').first();
      await editable.waitFor({ state: 'visible' });
      await editable.evaluate((element, html) => {
        const editor = (element as unknown as { ckeditorInstance?: { setData(v: string): void } }).ckeditorInstance;
        editor?.setData(html);
      }, product.htmlDescription);
      // Settle the editor's fixed 220ms change debounce so the HTML reaches the
      // Blazor-bound value before save. The persisted value is asserted from the
      // DB in the spec, so this is a debounce settle, not a correctness crutch.
      await this.page.waitForTimeout(400);
    }

    for (const feature of product.features) await this.addFeature(feature.title, feature.value, feature.active ?? true);
    for (const field of product.dynamicFields) await this.addDynamicField(field);

    for (const tagTitle of product.tagTitles) {
      const label = this.page.locator('.vz-check').filter({ hasText: tagTitle });
      await this.setHiddenCheckbox(label.locator('input[type="checkbox"]'), true);
    }

    // On new products the title change asynchronously suggests a slug. Assign the explicit scenario
    // slug last so the test drives the final value a real Admin intends to persist.
    await this.page.getByTestId('product-slug').fill(product.slug);
    await expect(this.page.getByTestId('product-slug')).toHaveValue(product.slug);
  }

  async create(product: ProductInput): Promise<string> {
    await this.openCreate();
    await this.fill(product);
    await Promise.all([
      this.page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/i),
      this.page.getByTestId('product-save').click()
    ]);
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success')).toBeVisible();
    const id = /\/admin\/products\/([0-9a-f-]{36})$/i.exec(this.page.url())?.[1];
    if (!id) throw new Error(`Product id missing from ${this.page.url()}`);
    return id;
  }

  async saveEdit(): Promise<void> {
    await this.page.getByTestId('product-save').click();
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success')).toBeVisible();
  }

  async saveExpectingError(): Promise<void> {
    await this.page.getByTestId('product-save').click();
    await expect(this.page.locator('.vz-toast.error, .vz-toast--error, .vz-val-msg:visible').first()).toBeVisible();
  }

  /** Retire test-created catalog rows through the same editor an Admin uses. */
  async setActive(productId: string, active: boolean): Promise<void> {
    await this.openEdit(productId);
    await this.setHiddenCheckbox(this.page.getByTestId('product-active'), active);
    await this.saveEdit();
    await expect(this.page.getByTestId('product-active')).toBeChecked({ checked: active });
  }

  async expectPersisted(product: ProductInput): Promise<void> {
    await expect(this.page.getByTestId('product-title')).toHaveValue(product.title);
    await expect(this.page.getByTestId('product-slug')).toHaveValue(product.slug);
    await expect(this.page.getByTestId('product-category')).toHaveValue(/.+/);
    await expect(this.page.getByTestId('product-type')).toHaveValue(String(product.productType));
    await expect(this.page.getByTestId(`product-delivery-${product.deliveryType}`)).toHaveClass(/active/);
    expect(Number(await this.page.getByTestId('product-base-price').inputValue())).toBe(product.basePrice);
    await expect(this.page.getByTestId('product-active')).toBeChecked({ checked: product.active });
    await expect(this.page.getByTestId('product-featured')).toBeChecked({ checked: product.featured });
  }

  async openDetails(productId: string): Promise<void> {
    await this.goto(`/admin/products/${productId}/details`);
    await expect(this.page.locator('.vz-content')).toBeVisible();
  }

  async importGiftCodes(productId: string, codes: string[], variantId?: string): Promise<void> {
    await this.goto('/admin/gift-codes');
    await this.page.locator('.vz-page-head button.vz-btn--primary').click();
    const dialog = this.page.getByRole('dialog');
    await dialog.locator('select.vz-select').first().selectOption(productId);
    if (variantId) {
      const selects = dialog.locator('select.vz-select');
      await expect(selects).toHaveCount(2);
      await selects.nth(1).selectOption(variantId);
    }
    await dialog.locator('input.vz-input').first().fill(`Matrix batch ${productId.slice(0, 8)} ${Date.now()}`);
    await dialog.locator('textarea.vz-textarea').fill(codes.join('\n'));
    await dialog.locator('button.vz-btn--primary').click();
    await expect(dialog).toBeHidden();
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success')).toBeVisible();
  }

  async uploadGalleryImage(productId: string, path: string): Promise<void> {
    await this.goto(`/admin/products/${productId}/images`);
    await this.page.locator('input[type="file"]').first().setInputFiles(path);
    await expect(this.page.locator('.vz-gallery__item')).toHaveCount(1);
  }

  private async addFeature(title: string, value: string, active: boolean): Promise<void> {
    const existing = await this.page.getByTestId('product-feature-row').count();
    await this.page.getByTestId('add-product-feature').click();
    await expect(this.page.getByTestId('product-feature-row')).toHaveCount(existing + 1);
    const row = this.page.getByTestId('product-feature-row').nth(existing);
    await row.locator('input.vz-input').nth(0).fill(title);
    await row.locator('input.vz-input').nth(1).fill(value);
    await this.setHiddenCheckbox(row.locator('input[type="checkbox"]'), active);
  }

  private async addDynamicField(field: ProductInput['dynamicFields'][number]): Promise<void> {
    const existing = await this.page.getByTestId('product-input-row').count();
    await this.page.getByTestId('add-product-input').click();
    await expect(this.page.getByTestId('product-input-row')).toHaveCount(existing + 1);
    const row = this.page.getByTestId('product-input-row').nth(existing);
    const inputs = row.locator('input.vz-input');
    await inputs.nth(0).fill(field.key);
    await inputs.nth(1).fill(field.label);
    await row.locator('select.vz-select').nth(0).selectOption(String(field.fieldType ?? 1));
    if (field.placeholder) await inputs.nth(2).fill(field.placeholder);
    await row.locator('select.vz-select').nth(1).selectOption(String(field.displayStage ?? 1));
    await this.setHiddenCheckbox(row.locator('input[type="checkbox"]').nth(0), field.required ?? false);
    await this.setHiddenCheckbox(row.locator('input[type="checkbox"]').nth(2), field.active ?? true);
  }

  private async fillOptionalNumber(testId: string, value?: number): Promise<void> {
    const input = this.page.getByTestId(testId);
    if (value === undefined) await input.fill(''); else await input.fill(String(value));
  }

  private async setHiddenCheckbox(input: import('@playwright/test').Locator, checked: boolean): Promise<void> {
    await input.evaluate((element: HTMLInputElement, desired) => {
      if (element.checked !== desired) element.click();
    }, checked);
    await expect(input).toBeChecked({ checked });
  }
}

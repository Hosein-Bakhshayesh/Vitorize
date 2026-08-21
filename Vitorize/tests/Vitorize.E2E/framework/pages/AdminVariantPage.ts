import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';
import type { VariantInput } from '../builders/VariantBuilder';

/** Variant modal/table behavior embedded in the Admin product editor. */
export class AdminVariantPage extends BasePage {
  constructor(page: Page) { super(page); }

  row(title: string) { return this.page.getByTestId('product-variant-row').filter({ hasText: title }); }

  async create(variant: VariantInput): Promise<void> {
    await this.page.getByTestId('add-product-variant').click();
    await this.fill(variant);
    await this.page.locator('button[form="variant-form"]').click();
    await expect(this.row(variant.title)).toHaveCount(1);
  }

  async edit(currentTitle: string, variant: VariantInput): Promise<void> {
    await this.row(currentTitle).locator('button[title="ویرایش"]').click();
    await this.fill(variant);
    await this.page.locator('button[form="variant-form"]').click();
    await expect(this.page.getByTestId('variant-form')).toHaveCount(0);
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
    await expect(this.row(variant.title)).toHaveCount(1);
  }

  /**
   * Gives a managed-stock SKU a sellable quantity without touching its other fields.
   *
   * A non-Instant product is created with one canonical SKU at zero stock, so it is deliberately
   * not purchasable until an administrator enters a quantity. This is that step — the same one a
   * real administrator performs — and exercising it here is what proves the product becomes
   * sellable rather than being stuck unsellable.
   */
  async setStock(variantTitle: string, quantity: number): Promise<void> {
    await this.row(variantTitle).locator('button[title="ویرایش"]').click();
    await this.page.getByTestId('variant-stock-quantity').fill(String(quantity));
    await this.page.locator('button[form="variant-form"]').click();
    await expect(this.page.getByTestId('variant-form')).toHaveCount(0);
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
  }

  async createExpectingError(variant: VariantInput): Promise<void> {
    await this.page.getByTestId('add-product-variant').click();
    await this.fill(variant);
    await this.page.locator('button[form="variant-form"]').click();
    await expect(this.page.locator('.vz-toast.error, .vz-toast--error')).toBeVisible();
    await expect(this.page.getByTestId('variant-form')).toBeVisible();
  }

  async delete(title: string): Promise<void> {
    await this.row(title).locator('button[title="حذف"]').click();
    const dialog = this.page.getByRole('dialog');
    await dialog.locator('button.vz-btn--danger').click();
    await expect(this.row(title)).toHaveCount(0);
  }

  private async fill(variant: VariantInput): Promise<void> {
    await this.page.getByTestId('variant-title').fill(variant.title);
    await this.page.getByTestId('variant-sku').fill(variant.sku ?? '');
    await this.page.getByTestId('variant-value').fill(variant.value ?? '');
    await this.page.getByTestId('variant-price').fill(String(variant.price));
    await this.page.getByTestId('variant-discount-price').fill(variant.discountPrice === undefined ? '' : String(variant.discountPrice));
    // Inventory policy is only a choice where it applies. An Instant product draws its units from
    // the gift-code pool, so the product editor deliberately does not render this control for one -
    // and the backend would refuse a policy anyway. Selecting it unconditionally made every
    // Instant-delivery case hang until the test timed out.
    const stockMode = this.page.getByTestId('variant-stock-mode');
    if (await stockMode.count()) await stockMode.selectOption(String(variant.stockMode));
    await this.page.getByTestId('variant-sort-order').fill(String(variant.sortOrder));
    await this.setHiddenCheckbox(this.page.getByTestId('variant-default'), variant.isDefault);
    await this.setHiddenCheckbox(this.page.getByTestId('variant-active'), variant.active);
  }

  private async setHiddenCheckbox(input: import('@playwright/test').Locator, checked: boolean): Promise<void> {
    await input.evaluate((element: HTMLInputElement, desired) => {
      if (element.checked !== desired) element.click();
    }, checked);
    await expect(input).toBeChecked({ checked });
  }
}

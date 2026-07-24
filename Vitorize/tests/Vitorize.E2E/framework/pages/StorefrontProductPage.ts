import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';
import type { ProductInput } from '../builders/ProductBuilder';

/** Storefront product details, variant selection, dynamic inputs, and cart behavior. */
export class StorefrontProductPage extends BasePage {
  constructor(page: Page) { super(page); }

  async open(slug: string): Promise<void> {
    await this.goto(`/product/${slug}`);
    await expect(this.page.locator('.st-buy__card')).toBeVisible();
  }

  async expectProduct(product: ProductInput, expectedPrice = product.discountPrice ?? product.basePrice): Promise<void> {
    await expect(this.page.getByRole('heading', { name: product.title })).toBeVisible();
    await expect(this.page.locator('.st-metarow')).toContainText(product.category);
    if (product.brand) await expect(this.page.locator('.st-metarow')).toContainText(product.brand);
    else await expect(this.page.locator('.st-metarow')).not.toContainText('برند:');
    expect(await this.currentPrice()).toBe(expectedPrice);
    // The storefront intentionally uses ShortDescription only as a fallback when
    // a rich FullDescription is absent (and as the SEO fallback in the page head).
    if (product.shortDescription && !product.htmlDescription) {
      await expect(this.page.locator('.st-rich-content')).toContainText(product.shortDescription);
    }
  }

  async selectVariant(title: string): Promise<void> {
    const card = this.page.locator('.st-vcard').filter({ hasText: title });
    await card.click();
    await expect(card).toHaveClass(/active/);
  }

  async addToCart(
    inputs: Record<string, string> = {},
    expectsInputForm = Object.keys(inputs).length > 0
  ): Promise<void> {
    const button = this.page.locator('.st-buy__card button.st-btn--accent');
    await expect(button).toBeEnabled();
    await button.click();
    const dialog = this.page.locator('.vz-dialog');
    if (expectsInputForm) {
      await expect(dialog).toBeVisible();
      for (const [key, value] of Object.entries(inputs)) {
        const input = dialog.locator(`#product-input-${key}`);
        await input.fill(value);
        await expect(input).toHaveValue(value);
      }
      await dialog.locator('button.st-btn--accent').click();
      await expect(dialog).toBeHidden();
    }
    await expect(this.page.locator('.vz-toast.success').last()).toBeVisible();
  }

  async expectOutOfStock(): Promise<void> {
    await expect(this.page.locator('.st-buy__card button.st-btn--accent')).toBeDisabled();
  }

  async expectRequiredFieldRejected(key: string): Promise<void> {
    await this.page.locator('.st-buy__card button.st-btn--accent').click();
    await expect(this.page.locator(`#product-input-${key}`)).toBeVisible();
    await this.page.locator('.vz-dialog button.st-btn--accent').click();
    await expect(this.page.locator('.vz-toast.error')).toBeVisible();
  }

  async expectNotPublic(slug: string): Promise<void> {
    const response = await this.page.goto(`/product/${slug}`);
    expect(response?.status()).toBe(404);
    await expect(this.page.locator('.st-buy__card')).toHaveCount(0);
  }

  async currentPrice(): Promise<number> {
    const raw = await this.page.locator('.st-buy__now').innerText();
    const normalized = raw
      .replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))
      .replace(/[^0-9.-]/g, '');
    return Number(normalized);
  }
}

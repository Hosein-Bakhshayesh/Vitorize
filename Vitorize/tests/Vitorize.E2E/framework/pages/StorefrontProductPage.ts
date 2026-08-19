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

  /**
   * Adds to the cart. There is no longer a product-page dialog: product information is collected at
   * Checkout, so any supplied values are remembered for {@link fillProductInformationAtCheckout}.
   */
  async addToCart(
    inputs: Record<string, string> = {},
    _expectsInputForm = false
  ): Promise<void> {
    const button = this.page.locator('.st-buy__card button.st-btn--accent');
    await expect(button).toBeEnabled();
    await button.click();
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
    for (const [key, value] of Object.entries(inputs)) this.pendingInputs.set(key, value);
  }

  private readonly pendingInputs = new Map<string, string>();

  /** Fills the checkout information section, using anything supplied to {@link addToCart}. */
  async fillProductInformationAtCheckout(): Promise<void> {
    const section = this.page.getByTestId('checkout-product-inputs');
    if (!(await section.count())) return;
    for (const field of await section.locator('input.st-input, textarea, select').all()) {
      const id = (await field.getAttribute('id')) ?? '';
      const key = id.replace(/^checkout-input-[0-9a-f]+-/i, '');
      const value = this.pendingInputs.get(key) ?? `e2e-${key || 'value'}`;
      const tag = await field.evaluate(el => el.tagName.toLowerCase());
      if (tag === 'select') await field.selectOption({ index: 1 }).catch(() => {});
      else await field.fill(value);
    }
  }

  async expectOutOfStock(): Promise<void> {
    await expect(this.page.locator('.st-buy__card button.st-btn--accent')).toBeDisabled();
  }

  /**
   * Adds the product and then proves the missing required value stops the purchase at Checkout —
   * the product page itself no longer asks for it, and no payment may begin without it.
   */
  async expectRequiredFieldRejected(key: string): Promise<void> {
    await this.page.locator('.st-buy__card button.st-btn--accent').click();
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();

    await this.page.goto('/checkout', { waitUntil: 'networkidle' });
    const field = this.page.locator(`[data-testid=checkout-input-card] [id$="-${key}"]`).first();
    await expect(field).toBeVisible();

    await this.page.locator('button.st-btn--accent').last().click();
    await expect(this.page).toHaveURL(/\/checkout/);
    await expect(field).toHaveAttribute('aria-invalid', 'true');
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

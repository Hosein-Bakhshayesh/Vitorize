import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';

/** Public storefront + authenticated customer account areas. */
export class StorefrontPage extends BasePage {
  private readonly customerLogout = this.page.locator('form[action="/auth/customer/logout"]').first();

  constructor(page: Page) {
    super(page);
  }

  async home(): Promise<void> {
    await this.goto('/');
    await this.expectUrl(/\/$/);
  }

  async openProduct(slug: string): Promise<void> {
    await this.goto(`/product/${slug}`);
  }

  /**
   * Adds a product to the cart. Product-required information is collected at Checkout now, so the
   * values are remembered here and filled in by {@link fillProductInformation} before payment.
   */
  async addToCart(slug: string, inputs: Record<string, string> = {}): Promise<void> {
    await this.page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
    await this.page.locator('.st-buy__card button.st-btn--accent').click();
    await expect(this.page.locator('.vz-toast.success, .vz-toast--success').first()).toBeVisible();
    for (const [key, value] of Object.entries(inputs)) this.pendingInputs.set(key, value);
  }

  private readonly pendingInputs = new Map<string, string>();

  /** Fills every product-information field the checkout page is asking for. */
  async fillProductInformation(): Promise<void> {
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

  /** Gateway checkout through the fake payment provider; returns the created order id. */
  async checkoutAndPay(): Promise<string> {
    await this.page.goto('/checkout', { waitUntil: 'networkidle' });
    await expect(this.page.locator('.st-paycard.active')).toBeVisible();
    await this.fillProductInformation();
    await this.page.locator('button.st-btn--accent').last().click();
    await expect(this.page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
    const match = /orderId=([0-9a-f-]+)/i.exec(this.page.url());
    if (!match) throw new Error(`No orderId in payment result URL: ${this.page.url()}`);
    return match[1];
  }

  async openOrder(orderId: string): Promise<void> {
    await this.page.goto(`/customer/orders/${orderId}`, { waitUntil: 'networkidle' });
    await expect(this.page).toHaveURL(new RegExp(`/customer/orders/${orderId}`, 'i'));
  }

  async expectCustomerDashboard(): Promise<void> {
    await this.expectUrl(/\/customer\/dashboard/);
    await expect(this.customerLogout).toBeVisible();
  }

  async openAccount(section: string): Promise<void> {
    await this.goto(section.startsWith('/') ? section : `/customer/${section}`);
  }

  async logout(): Promise<void> {
    await Promise.all([
      this.page.waitForURL(/\/$/),
      this.customerLogout.locator('button[type="submit"]').click()
    ]);
  }
}

import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';
import type { TestUser } from '../users';

/** /login — the storefront customer sign-in page (password + OTP tabs). */
export class StoreLoginPage extends BasePage {
  private readonly mobile = this.page.locator('#pw-mobile');
  private readonly password = this.page.locator('#pw-pass');
  private readonly submit = this.page.locator('form[action="/auth/customer/login"] button[type="submit"]');
  readonly error = this.page.locator('.st-alert--danger');

  constructor(page: Page) {
    super(page);
  }

  async open(returnUrl?: string): Promise<void> {
    await this.goto(returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login');
  }

  async signIn(u: TestUser, returnUrl?: string): Promise<void> {
    await this.open(returnUrl);
    await this.mobile.fill(u.mobile);
    await this.password.fill(u.password);
    await Promise.all([
      this.page.waitForURL(returnUrl ? new RegExp(returnUrl.replaceAll('/', '\\/')) : /\/customer\/dashboard/i),
      this.submit.click()
    ]);
  }

  async signInExpectingFailure(mobile: string, password: string): Promise<void> {
    await this.open();
    await this.mobile.fill(mobile);
    await this.password.fill(password);
    await this.submit.click();
    await expect(this.page).toHaveURL(/\/login\?error=/);
    await expect(this.error).toBeVisible();
  }
}

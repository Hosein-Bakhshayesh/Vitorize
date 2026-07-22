import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';
import type { TestUser } from '../users';

/** /admin/login — the admin panel sign-in page (full-document POST form). */
export class AdminLoginPage extends BasePage {
  private readonly mobile = this.page.locator('input[name="mobile"]');
  private readonly password = this.page.locator('input[name="password"]');
  private readonly submit = this.page.locator('form[action="/admin/auth/login"] button[type="submit"]');
  readonly error = this.page.locator('.st-alert--danger, .vz-alert--danger');

  constructor(page: Page) {
    super(page);
  }

  async open(returnUrl?: string): Promise<void> {
    await this.goto(returnUrl ? `/admin/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/admin/login');
  }

  /** Fill + submit and wait for the admin area (used for the happy path). */
  async signIn(u: TestUser): Promise<void> {
    await this.open();
    await this.mobile.fill(u.mobile);
    await this.password.fill(u.password);
    await Promise.all([
      this.page.waitForURL(/\/admin(\/dashboard)?$/i),
      this.submit.click()
    ]);
  }

  /** Fill + submit expecting rejection (stays on the login page with an error). */
  async signInExpectingFailure(mobile: string, password: string): Promise<void> {
    await this.open();
    await this.mobile.fill(mobile);
    await this.password.fill(password);
    await this.submit.click();
    await expect(this.page).toHaveURL(/\/admin\/login/);
  }
}

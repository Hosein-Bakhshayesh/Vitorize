import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';

/** The authenticated admin shell (topbar + sidebar) common to every /admin/* page. */
export class AdminShellPage extends BasePage {
  private readonly profile = this.page.locator('button.vz-profile');
  private readonly logoutForm = this.page.locator('form[action="/admin/auth/logout"]');

  constructor(page: Page) {
    super(page);
  }

  /** Asserts an authenticated admin shell is rendered on the current page. */
  async expectAuthenticated(): Promise<void> {
    await expect(this.profile).toBeVisible();
  }

  async open(section: string): Promise<void> {
    await this.goto(section.startsWith('/') ? section : `/admin/${section}`);
  }

  async gotoDashboard(): Promise<void> {
    await this.open('/admin/dashboard');
    await this.expectUrl(/\/admin\/dashboard/);
    await this.expectAuthenticated();
  }

  async logout(): Promise<void> {
    // The logout form is rendered only inside the open profile menu. Submit it programmatically once
    // present: a plain click can be intercepted by the menu's full-screen close-overlay right after a
    // reload. requestSubmit() still performs the real full-document POST (form is data-enhance="false").
    await this.profile.click();
    await expect(this.logoutForm).toBeVisible();
    await Promise.all([
      this.page.waitForURL(/\/admin\/login/),
      this.logoutForm.evaluate((f: HTMLFormElement) => f.requestSubmit())
    ]);
  }
}

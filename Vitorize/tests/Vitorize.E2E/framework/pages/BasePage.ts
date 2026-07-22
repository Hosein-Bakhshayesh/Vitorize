import { expect, type Page } from '@playwright/test';

/** Shared behaviour for every page object. */
export abstract class BasePage {
  constructor(protected readonly page: Page) {}

  async goto(path: string): Promise<void> {
    await this.page.goto(path);
  }

  url(): string {
    return this.page.url();
  }

  // 'load' (default), NOT 'networkidle': Blazor Server keeps a SignalR WebSocket open, so the network
  // never idles. Page objects assert readiness via a concrete element instead.
  async reload(): Promise<void> {
    await this.page.reload();
  }

  async expectUrl(pattern: RegExp): Promise<void> {
    await expect(this.page).toHaveURL(pattern);
  }
}

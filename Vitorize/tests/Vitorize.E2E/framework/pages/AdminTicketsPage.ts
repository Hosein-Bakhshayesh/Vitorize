import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';

/** Admin support inbox (/admin/tickets): list, thread, reply (delivers support content), close/reopen. */
export class AdminTicketsPage extends BasePage {
  private readonly items = this.page.locator('.vz-ticket-item');
  private readonly thread = this.page.locator('.vz-card__body .vz-msg');
  private readonly replyBox = this.page.locator('textarea.vz-textarea');
  private readonly replyButton = this.page.getByRole('button', { name: /ارسال پاسخ/ });
  private readonly closeButton = this.page.getByRole('button', { name: /بستن تیکت/ });
  private readonly reopenButton = this.page.getByRole('button', { name: /بازگشایی/ });

  constructor(page: Page) {
    super(page);
  }

  async open(): Promise<void> {
    await this.goto('/admin/tickets');
    await expect(this.page.locator('.vz-inbox-grid')).toBeVisible();
  }

  /** Select the ticket whose list row contains the given subject and wait for its thread to load. */
  async openBySubject(subject: string): Promise<void> {
    const row = this.items.filter({ hasText: subject }).first();
    await expect(row).toBeVisible();
    await row.click();
    await expect(this.replyBox).toBeVisible();
  }

  async reply(text: string): Promise<void> {
    await this.replyBox.fill(text);
    await this.replyButton.click();
    await expect(this.page.locator('.vz-msg__bubble').filter({ hasText: text })).toBeVisible();
  }

  async expectMessage(text: string): Promise<void> {
    await expect(this.page.locator('.vz-msg__bubble').filter({ hasText: text })).toBeVisible();
  }

  async close(): Promise<void> {
    await this.closeButton.click();
    await expect(this.reopenButton).toBeVisible();
  }

  async reopen(): Promise<void> {
    await this.reopenButton.click();
    await expect(this.closeButton).toBeVisible();
  }
}

import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage';

/** Customer support-ticket pages: create (from an order), list, and the message thread. */
export class CustomerTicketsPage extends BasePage {
  private readonly subject = this.page.locator('input.st-input').first();
  private readonly prioritySelect = this.page.locator('select.st-select').nth(1);
  private readonly message = this.page.locator('textarea.st-textarea');
  private readonly submit = this.page.locator('.st-card button.st-btn--primary');
  private readonly thread = this.page.locator('.st-chat');
  private readonly replyBox = this.page.locator('.st-textarea');
  private readonly replyButton = this.page.getByRole('button', { name: /ارسال پاسخ/ });
  readonly closedNotice = this.page.getByText('این تیکت بسته شده است');

  constructor(page: Page) {
    super(page);
  }

  /** Create a ticket for an order through the real "create ticket for this order" flow; returns id. */
  async createForOrder(orderId: string, opts: { subject: string; priority?: string; message: string }): Promise<string> {
    return this.createAt(`/customer/tickets/new?orderId=${orderId}`, opts);
  }

  /** Create a general (order-less) support ticket; returns id. */
  async createGeneral(opts: { subject: string; priority?: string; message: string }): Promise<string> {
    return this.createAt('/customer/tickets/new', opts);
  }

  private async createAt(url: string, opts: { subject: string; priority?: string; message: string }): Promise<string> {
    await this.goto(url);
    await this.subject.fill(opts.subject);
    if (opts.priority) await this.prioritySelect.selectOption(opts.priority);
    await this.message.fill(opts.message);
    await Promise.all([
      this.page.waitForURL(/\/customer\/tickets\/[0-9a-f-]+$/i),
      this.submit.click()
    ]);
    const match = /\/customer\/tickets\/([0-9a-f-]+)/i.exec(this.page.url());
    if (!match) throw new Error(`No ticket id in URL: ${this.page.url()}`);
    return match[1];
  }

  async open(ticketId: string): Promise<void> {
    await this.goto(`/customer/tickets/${ticketId}`);
    await expect(this.thread).toBeVisible();
  }

  async reply(text: string): Promise<void> {
    await this.replyBox.fill(text);
    await this.replyButton.click();
    await expect(this.thread).toContainText(text);
  }

  async expectMessage(text: string): Promise<void> {
    await expect(this.thread).toContainText(text);
  }

  /** The raw HTML of the thread, to assert markup is encoded (no live <script>/<img onerror>). */
  async threadHtml(): Promise<string> {
    return this.thread.innerHTML();
  }

  async expectClosed(): Promise<void> {
    await expect(this.closedNotice).toBeVisible();
    await expect(this.replyButton).toHaveCount(0);
  }
}

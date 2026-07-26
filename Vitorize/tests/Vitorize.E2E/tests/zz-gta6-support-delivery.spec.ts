import {
  test, expect, TAG, USERS,
  loginSeededCustomerWithEmptyCart, registerCustomer
} from '../framework/fixtures';
import { apiBaseUrl } from './support/app';
import { seedGta6Product, GTA6, GTA6_SLUG, type Gta6Seeded } from '../framework/gta6Seed';
import type { APIRequestContext } from '@playwright/test';

// End-to-end GTA VI SupportRequired scenario. The product is created/reconciled through the real
// Admin API (idempotent by slug); the purchase, auto-ticket, admin reply, isolation and idempotency
// all run against the live stack. Deterministic QA credentials are fake test data only.

const CREDENTIALS = {
  email: 'qa.gta6.standard@vitorize.test',
  password: 'Vt-GTA6-Std-2026!'
};
const SUPPORT_REPLY = [
  'سلام،', 'اکانت تست مربوط به سفارش شما آماده شده است.', '',
  'پلتفرم: PlayStation 5', 'نسخه: Standard',
  `ایمیل تست: ${CREDENTIALS.email}`, `رمز عبور تست: ${CREDENTIALS.password}`, 'ریجن: Test Region', '',
  'راهنمای فعال‌سازی:',
  '1. در کنسول PlayStation 5 وارد بخش Add User شوید.',
  '2. اطلاعات اکانت بالا را وارد کنید.',
  '3. Library را بررسی کنید.',
  '4. مراحل فعال‌سازی نمایش‌داده‌شده در کنسول را انجام دهید.',
  '5. نتیجه را در همین تیکت اعلام کنید.', '',
  'این اطلاعات صرفاً برای تست QA هستند و متعلق به اکانت واقعی نیستند.'
].join('\n');

async function apiToken(request: APIRequestContext, mobile: string, password: string): Promise<string> {
  const res = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile, password } });
  expect(res.ok(), `login ${mobile} -> ${res.status()}`).toBeTruthy();
  return (await res.json()).data.accessToken as string;
}
const bearer = (token: string) => ({ headers: { Authorization: `Bearer ${token}` } });

let seeded: Gta6Seeded;

test.describe.serial('GTA VI support-delivery scenario', () => {
  test.describe.configure({ timeout: 180_000 });

  test.beforeAll(async ({ request }) => { seeded = await seedGta6Product(request); });

  test('product is created and reconciled idempotently through the Admin API', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.regression]
  }, async ({ request }) => {
    const again = await seedGta6Product(request);
    expect(again.productId).toBe(seeded.productId);
    expect(again.standardVariantId).toBe(seeded.standardVariantId);
    expect(again.ultimateVariantId).toBe(seeded.ultimateVariantId);

    const token = await apiToken(request, USERS.SuperAdmin.mobile, USERS.SuperAdmin.password);
    const products = (await (await request.get(`${apiBaseUrl}/admin/products`, bearer(token))).json()).data as any[];
    expect(products.filter(p => p.slug === GTA6_SLUG)).toHaveLength(1);
    const variants = (await (await request.get(`${apiBaseUrl}/admin/products/${seeded.productId}/variants`, bearer(token))).json()).data as any[];
    expect(variants.filter(v => v.sku === GTA6.standardSku || v.sku === GTA6.ultimateSku)).toHaveLength(2);
  });

  test('storefront renders the product, both edition prices, CKEditor content and inline images cleanly', {
    tag: [TAG.product, TAG.catalog, TAG.ui, TAG.seo, TAG.regression]
  }, async ({ page, storefrontProduct, consoleGuard }) => {
    const store = storefrontProduct;
    await store.open(GTA6_SLUG);
    await expect(page.locator('h1, .st-phead__title')).toContainText('GTA VI');

    // CKEditor RTL rich content: headings, table, notice, lists.
    const content = page.locator('.st-rich-content');
    await expect(content).toContainText('معرفی بازی GTA VI');
    await expect(content).toContainText('مقایسه نسخه Standard و Ultimate');
    await expect(content).toContainText('سؤالات متداول');
    await expect(content.locator('table')).toBeVisible();
    await expect(content.locator('blockquote').first()).toContainText('تحویل با پشتیبانی');

    // Inline uploaded images load (naturalWidth > 0).
    const inlineImages = content.locator('img');
    await expect(inlineImages).toHaveCount(2);
    for (let i = 0; i < 2; i++) {
      await expect.poll(async () => inlineImages.nth(i).evaluate((el: HTMLImageElement) => el.complete && el.naturalWidth > 0))
        .toBe(true);
    }

    // Default Standard price (value + تومان unit), then Ultimate.
    await expect(page.locator('.st-buy__card')).toContainText('تومان');
    await expect.poll(async () => store.currentPrice()).toBe(GTA6.standardPrice);
    await store.selectVariant(GTA6.ultimateTitle);
    await expect.poll(async () => store.currentPrice()).toBe(GTA6.ultimatePrice);

    consoleGuard.assertClean();
  });

  test('Standard purchase auto-creates one linked ticket; admin credential reply is visible only to the buyer; retries do not duplicate', {
    tag: [TAG.supportDelivery, TAG.business, TAG.customer, TAG.admin, TAG.ticket, TAG.regression, TAG.release]
  }, async ({ page, browser, request, storefront, storefrontProduct, customerTickets }) => {
    // --- Buyer completes a Standard purchase through the storefront + mock gateway ---
    await loginSeededCustomerWithEmptyCart(page);
    // GTA VI has no dynamic input fields, so add-to-cart is direct (no dialog);
    // the default (Standard) variant is pre-selected.
    await storefrontProduct.open(GTA6_SLUG);
    await storefrontProduct.addToCart({});
    const orderId = await storefront.checkoutAndPay();

    const state = async () => (await request.get(`${apiBaseUrl}/testing/support-state?orderId=${orderId}`)).json();
    const s1 = await state();
    expect(s1.paid).toBe(true);
    expect(s1.supportItems).toBe(1);
    expect(s1.giftCodesAssigned).toBe(0);
    expect(s1.instantDeliveries).toBe(0);
    expect(s1.tickets).toBe(1);
    expect(s1.ticketUserId).toBe(s1.orderUserId);

    // Buyer context (API): order references Standard at the correct price; the auto-ticket names the edition.
    const buyerToken = await apiToken(request, USERS.Customer.mobile, USERS.Customer.password);
    const order = (await (await request.get(`${apiBaseUrl}/orders/${orderId}`, bearer(buyerToken))).json()).data;
    expect(order.finalAmount).toBe(GTA6.standardPrice);
    expect(order.items[0].variantTitle).toBe(GTA6.standardTitle);

    const myTickets = (await (await request.get(`${apiBaseUrl}/tickets`, bearer(buyerToken))).json()).data as any[];
    const ticketSummary = myTickets.find(t => t.orderId === orderId);
    expect(ticketSummary, 'auto-created ticket for the order').toBeTruthy();
    const ticketId: string = ticketSummary.id;
    expect(ticketSummary.subject).toContain('تحویل اکانت GTA VI');

    const ticketBefore = (await (await request.get(`${apiBaseUrl}/tickets/${ticketId}`, bearer(buyerToken))).json()).data;
    expect(ticketBefore.messages[0].message).toContain(GTA6.standardTitle); // edition in the initial message
    expect(JSON.stringify(ticketBefore)).not.toContain(CREDENTIALS.password); // no creds yet

    // Idempotency: refreshing the order and re-querying state creates no second ticket.
    await storefront.openOrder(orderId);
    expect((await state()).tickets).toBe(1);

    // --- Admin sends the customer-visible credential reply through the real support workflow ---
    const adminToken = await apiToken(request, USERS.SuperAdmin.mobile, USERS.SuperAdmin.password);
    const reply = await request.post(`${apiBaseUrl}/admin/tickets/${ticketId}/messages`,
      { ...bearer(adminToken), data: { message: SUPPORT_REPLY, isInternalNote: false } });
    expect(reply.ok(), `admin reply ${reply.status()}`).toBeTruthy();

    // Buyer can read the credentials in the ticket thread (API + real UI).
    const ticketAfter = (await (await request.get(`${apiBaseUrl}/tickets/${ticketId}`, bearer(buyerToken))).json()).data;
    const thread = JSON.stringify(ticketAfter);
    expect(thread).toContain(CREDENTIALS.email);
    expect(thread).toContain(CREDENTIALS.password);
    expect(ticketAfter.messages.some((m: any) => m.isInternalNote)).toBe(false);

    await customerTickets.open(ticketId);
    await customerTickets.expectMessage(CREDENTIALS.email);

    // Security: credentials are not copied into the buyer's notifications.
    const notifications = await request.get(`${apiBaseUrl}/notifications`, bearer(buyerToken));
    expect(await notifications.text()).not.toContain(CREDENTIALS.password);

    // --- Cross-customer isolation: a different customer cannot read the ticket (API + UI) ---
    const otherContext = await browser.newContext();
    const otherPage = await otherContext.newPage();
    try {
      const other = await registerCustomer(otherPage);
      const otherToken = await apiToken(request, other.mobile, other.password);
      // Authoritative isolation: the ticket API denies a non-owner (403/404).
      const forbidden = await request.get(`${apiBaseUrl}/tickets/${ticketId}`, bearer(otherToken));
      expect([403, 404]).toContain(forbidden.status());
      // The SPA shell always returns 200; the guarantee is that the non-owner's
      // ticket page never renders the ticket thread or the credentials.
      await otherPage.goto(`/customer/tickets/${ticketId}`, { waitUntil: 'networkidle' });
      await expect(otherPage.locator('body')).not.toContainText(CREDENTIALS.password);
      await expect(otherPage.locator('body')).not.toContainText(CREDENTIALS.email);
    } finally {
      await otherContext.close();
    }
  });
});

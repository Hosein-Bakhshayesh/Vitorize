import {
  test, expect, TAG, USERS,
  AdminLoginPage, AdminTicketsPage, StoreLoginPage
} from '../framework/fixtures';
import { apiBaseUrl } from './support/app';

// End-to-end SupportRequired / ticket-delivery lifecycle. The product is seeded deterministically
// (seed-e2e.sql, DeliveryType=SupportRequired, no gift-code inventory); the PURCHASE, ticket and
// support delivery all happen through the real browser UI. DB invariants are asserted through the
// Testing-only /api/testing/support-state endpoint (never returns codes, message text or PII).

const SUPPORT_SLUG = 'e2e-support-product';

async function supportState(page: import('@playwright/test').Page, orderId: string) {
  const res = await page.request.get(`${apiBaseUrl}/testing/support-state?orderId=${orderId}`);
  expect(res.ok(), `support-state ${res.status()}`).toBeTruthy();
  return res.json();
}

test.describe('support/ticket delivery', () => {
  test('buy support product -> open ticket -> admin delivers via reply + closes -> customer verifies', {
    tag: [TAG.supportDelivery, TAG.business, TAG.customer, TAG.admin, TAG.ticket, TAG.regression, TAG.release]
  }, async ({ page, browser, storeLogin, storefront, customerTickets }) => {
    // --- Customer buys the SupportRequired product through the storefront UI ---
    await storeLogin.signIn(USERS.Customer);
    await storefront.addToCart(SUPPORT_SLUG, { support_ref: 'e2e-buyer-ref' });
    const orderId = await storefront.checkoutAndPay();

    // Order page: the support reference is shown, a "create ticket for this order" action exists,
    // and NO instant gift code is exposed.
    await storefront.openOrder(orderId);
    await expect(page.locator('main')).toContainText('e2e-buyer-ref');
    await expect(page.locator(`a[href*="/customer/tickets/new?orderId=${orderId}"]`)).toBeVisible();

    // DB: exactly this paid order, one support item, no gift code / instant delivery, no ticket yet.
    const s1 = await supportState(page, orderId);
    expect(s1.paid).toBe(true);
    expect(s1.supportItems).toBe(1);
    expect(s1.giftCodesAssigned).toBe(0);
    expect(s1.instantDeliveries).toBe(0);
    expect(s1.tickets).toBe(0);

    // --- Customer opens a support ticket for the order ---
    const subject = `E2E Support ${Date.now()}`;
    const ticketId = await customerTickets.createForOrder(orderId, {
      subject, priority: '3', message: 'Please deliver my support service for this order.'
    });
    await customerTickets.expectMessage('Please deliver my support service');

    // DB: exactly one ticket, owned by the buyer, still no gift code.
    const s2 = await supportState(page, orderId);
    expect(s2.tickets).toBe(1);
    expect(s2.ticketUserId).toBe(s2.orderUserId);
    expect(s2.giftCodesAssigned).toBe(0);

    // --- Admin (isolated context) finds the ticket, delivers the support content, then closes ---
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    try {
      await new AdminLoginPage(adminPage).signIn(USERS.SuperAdmin);
      const adminTickets = new AdminTicketsPage(adminPage);
      await adminTickets.open();
      await adminTickets.openBySubject(subject);
      await adminTickets.expectMessage('Please deliver my support service'); // sees the customer request
      await adminTickets.reply('Delivered support content: activation ACT-4242.');
      await adminTickets.close();
    } finally {
      await adminContext.close();
    }

    // --- Customer sees the delivered content, the closed state, and can no longer reply ---
    await customerTickets.open(ticketId);
    await customerTickets.expectMessage('ACT-4242');
    await customerTickets.expectClosed();

    // Final DB invariants: still one paid order, one ticket, and NO gift-code assignment/delivery.
    const s3 = await supportState(page, orderId);
    expect(s3.paid).toBe(true);
    expect(s3.tickets).toBe(1);
    expect(s3.giftCodesAssigned).toBe(0);
    expect(s3.instantDeliveries).toBe(0);
  });

  test('anonymous users cannot open a ticket', {
    tag: [TAG.ticket, TAG.security, TAG.regression, TAG.release]
  }, async ({ page }) => {
    await page.goto('/customer/tickets/31000000-0000-0000-0000-0000000000aa');
    await expect(page).toHaveURL(/\/login\?returnUrl=/i);
  });

  test('a non-existent ticket shows a safe not-found to its owner (no leak)', {
    tag: [TAG.ticket, TAG.security]
  }, async ({ storeLogin, page }) => {
    await storeLogin.signIn(USERS.Customer);
    await page.goto('/customer/tickets/31000000-0000-0000-0000-0000000000bb');
    await expect(page.getByText('تیکت یافت نشد')).toBeVisible();
  });

  test('a customer cannot access another customer\'s ticket (IDOR)', {
    tag: [TAG.ticket, TAG.security, TAG.regression, TAG.release]
  }, async ({ page, browser, storeLogin, customerTickets }) => {
    // Buyer creates a ticket.
    await storeLogin.signIn(USERS.Customer);
    const ticketId = await customerTickets.createGeneral({
      subject: `E2E IDOR ${Date.now()}`, message: 'Private support conversation.'
    });

    // A different customer must not be able to read it by id.
    const otherContext = await browser.newContext();
    const otherPage = await otherContext.newPage();
    try {
      await new StoreLoginPage(otherPage).signIn(USERS.CustomerVIP);
      await otherPage.goto(`/customer/tickets/${ticketId}`);
      await expect(otherPage.getByText('تیکت یافت نشد')).toBeVisible();
      await expect(otherPage.locator('.st-chat')).toHaveCount(0);
      // The API path is likewise owner-scoped.
      const api = await otherPage.request.get(`${apiBaseUrl}/tickets/${ticketId}`);
      expect([401, 403, 404]).toContain(api.status());
    } finally {
      await otherContext.close();
    }
  });

  test('a script payload in a ticket message is encoded, never executed', {
    tag: [TAG.ticket, TAG.security, TAG.regression, TAG.release]
  }, async ({ page, storeLogin, customerTickets }) => {
    let dialogFired = false;
    page.on('dialog', async d => { dialogFired = true; await d.dismiss(); });

    await storeLogin.signIn(USERS.Customer);
    const payload = '<script>alert(1)</script><img src=x onerror=alert(2)>';
    const ticketId = await customerTickets.createGeneral({ subject: `E2E XSS ${Date.now()}`, message: payload });
    await customerTickets.open(ticketId);

    const html = await customerTickets.threadHtml();
    expect(html).not.toContain('<script>alert(1)</script>');
    expect(html).not.toContain('<img src=x onerror');
    expect(html).toContain('&lt;script&gt;');
    expect(dialogFired).toBe(false);
  });
});

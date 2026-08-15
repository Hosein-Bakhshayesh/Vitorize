import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow, loginAdmin, loginCustomer, monitorBrowser, seededCustomer } from './support/app';

const order = {
  main: 'P2F-MAIN',
  mixed: 'P2F-MIXED'
} as const;

const heldCanary = 'FIX09-P2F-HELD-CANARY-DO-NOT-RENDER';

test.describe('FIX-09 Phase 2F Admin order KYC projection @fix09p2fadmin', () => {
  test.describe.configure({ timeout: 180_000 });

  test('Admin details render lifecycle, snapshot, fulfillment, support, and mixed-item truth', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'The full lifecycle matrix is exercised once in desktop light.');
    const browser = monitorBrowser(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAdmin(page);

    const kycContextCalls: string[] = [];
    page.on('request', request => {
      if (request.url().includes('/kyc-context')) kycContextCalls.push(request.url());
    });

    const main = await openAdminOrder(page, order.main);
    await expect(main).toContainText('P2F Awaiting Submission');
    await expect(main).toContainText('احراز هویت:');
    await expect(main).toContainText('2F V1 Purchase Policy');
    await expect(main).toContainText('آستانه:');
    await expect(main).toContainText('مبلغ ارزیابی:');

    await expect(itemRow(main, 'P2F Awaiting Submission')).toContainText('در انتظار ارسال مدارک');
    await expect(itemRow(main, 'P2F Awaiting Submission')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(main, 'P2F Awaiting Review')).toContainText('در انتظار بررسی');
    await expect(itemRow(main, 'P2F Awaiting Review')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(main, 'P2F Rejected')).toContainText('نیازمند اصلاح و ارسال مجدد');
    await expect(itemRow(main, 'P2F Final Rejected')).toContainText('رد نهایی');
    await expect(itemRow(main, 'P2F Instant Delivered')).toContainText('تأیید شده');
    await expect(itemRow(main, 'P2F Instant Delivered')).toContainText('تحویل شده');
    await expect(itemRow(main, 'P2F Instant Delivered').locator('.vz-manual-delivery')).toHaveCount(0);
    await expect(itemRow(main, 'P2F Manual Held')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(main, 'P2F Manual Held').locator('.vz-manual-delivery')).toHaveCount(0);
    await expect(itemRow(main, 'P2F Manual Pending')).toContainText('انسداد تحویل: خیر');
    await expect(itemRow(main, 'P2F Manual Pending').locator('.vz-manual-delivery')).toBeVisible();
    await expect(itemRow(main, 'P2F Manual Pending')).not.toContainText('تحویل شده');
    await expect(itemRow(main, 'P2F Support Held')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(main, 'P2F Support Held').locator('.vz-manual-delivery')).toHaveCount(0);
    await expect(itemRow(main, 'P2F Support Pending')).toContainText('تأیید شده');
    await expect(itemRow(main, 'P2F Support Pending')).toContainText('پشتیبانی');
    await expect(itemRow(main, 'P2F Support Pending').locator('.vz-manual-delivery')).toHaveCount(0);
    await expect(itemRow(main, 'P2F Support Pending').locator('button').filter({ hasText: 'ایجاد' })).toHaveCount(0);
    await expect(main).not.toContainText(heldCanary);
    expect(kycContextCalls).toEqual([]);
    await expectRtlAndNoOverflow(page);
    browser.assertClean();

    const mixed = await openAdminOrder(page, order.mixed);
    await expect(itemRow(mixed, 'P2F Mixed Delivered Legacy')).toContainText('تحویل شده');
    await expect(itemRow(mixed, 'P2F Mixed Awaiting Submission')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(mixed, 'P2F Mixed Manual Pending')).toContainText('انسداد تحویل: خیر');
    await expect(itemRow(mixed, 'P2F Mixed Manual Pending').locator('.vz-manual-delivery')).toBeVisible();
    await expect(itemRow(mixed, 'P2F Mixed Support Satisfied')).toContainText('انسداد تحویل: خیر');
    await expect(mixed).not.toContainText(heldCanary);
    await expectRtlAndNoOverflow(page);
  });

  test('Admin order details remain usable in light, dark, and compact layouts', async ({ page }, testInfo) => {
    const browser = monitorBrowser(page);
    const compact = testInfo.project.name.startsWith('mobile');
    await page.setViewportSize(compact ? { width: 393, height: 852 } : { width: 1440, height: 900 });
    await loginAdmin(page);
    const details = await openAdminOrder(page, order.main);
    await expect(itemRow(details, 'P2F Manual Held')).toContainText('انسداد تحویل: بله');
    await expect(itemRow(details, 'P2F Manual Pending').locator('.vz-manual-delivery')).toBeVisible();
    await openAdminOrder(page, order.mixed);
    await expectRtlAndNoOverflow(page);
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    browser.assertClean();
  });

  test('Customer cannot access the Admin order KYC projection', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Authorization is exercised once.');
    await loginCustomer(page, seededCustomer);
    await page.goto('/admin/orders', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/admin\/login/);
    await expect(page.locator('body')).not.toContainText('2F V1 Purchase Policy');
  });
});

async function openAdminOrder(page: import('@playwright/test').Page, number: string) {
  await page.goto('/admin/orders', { waitUntil: 'networkidle' });
  await page.locator('#order-search').fill(number);
  const row = page.locator('tbody tr').filter({ hasText: number });
  await expect(row).toHaveCount(1);
  await row.locator('.vz-ctx__trigger').click();
  await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
  const details = page.getByRole('dialog').filter({ hasText: number });
  await expect(details).toBeVisible();
  return details;
}

function itemRow(details: import('@playwright/test').Locator, title: string) {
  return details.locator('tbody tr').filter({ hasText: title });
}

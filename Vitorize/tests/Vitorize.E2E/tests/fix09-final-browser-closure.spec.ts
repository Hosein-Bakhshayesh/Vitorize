import { expect, test, type Page } from '@playwright/test';
import { adminPassword, loginAdmin, loginCustomer, monitorBrowser, seededCustomer } from './support/app';

const financeItemId = '32000000-0000-0000-0000-000000000104';
const supportCustomer = { fullName: 'FIX09 Final Support Customer', mobile: '09120000070', email: 'fix09-final-support@example.test', password: adminPassword };

test.describe.serial('FIX-09 final browser closure @fix09final', () => {
  test.describe.configure({ timeout: 180_000 });

  test('Finance Pending -> external resolution -> Customer Resolved', async ({ page, browser }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Final closure runs once in desktop light.');
    const customerRuntime = monitorRuntime(page);
    await loginCustomer(page, seededCustomer);
    await page.goto(`/customer/verification?orderItem=${financeItemId}`, { waitUntil: 'networkidle' });
    await expect(page.getByText('احراز هویت این آیتم تأیید نشد و وضعیت مالی آن در حال بررسی است.', { exact: true })).toBeVisible();
    await expect(page.locator('main')).not.toContainText('بازپرداخت مالی ثبت شده است.');
    await expect(page.locator('main')).not.toContainText('پرداخت لغو شد');
    await expect(page.locator('main')).not.toContainText('ارسال مجدد مدارک امکان‌پذیر نیست');

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    const adminRuntime = monitorRuntime(adminPage);
    try {
      await loginAdmin(adminPage);
      const details = await openAdminOrder(adminPage, 'P2F-MAIN');
      const finalRejected = itemRow(details, 'P2F Final Rejected');
      const answers = ['FIX09 external finance resolution', 'FIX09-EXTERNAL-REFUND'];
      adminPage.on('dialog', async dialog => { await dialog.accept(answers.shift()); });
      await finalRejected.getByRole('button', { name: 'ثبت بازپرداخت خارجی', exact: true }).click();
      await expect(adminPage.locator('.vz-toast.success')).toContainText('بازپرداخت خارجی ثبت شد.');
    } finally {
      await adminContext.close();
    }

    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.getByText('بازپرداخت مالی ثبت شده است.', { exact: true })).toBeVisible();
    await expect(page.getByText('احراز هویت این آیتم تأیید نشد و وضعیت مالی آن در حال بررسی است.', { exact: true })).toHaveCount(0);
    await expect(page.locator('main')).not.toContainText('پرداخت لغو شد');
    await expect(page.locator('main')).not.toContainText('FIX09 external finance resolution');
    customerRuntime.assertClean();
    adminRuntime.assertClean();
  });

  test('Manual delivery -> truthful customer notification without duplicates', async ({ page, browser }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Final closure runs once in desktop light.');
    const customerRuntime = monitorRuntime(page);
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    const adminRuntime = monitorRuntime(adminPage);
    try {
      await loginAdmin(adminPage);
      const details = await openAdminOrder(adminPage, 'P2F-MAIN');
      await itemRow(details, 'P2F Closure Delivery').locator('.vz-manual-delivery').click();
      const deliveryDialog = adminPage.getByRole('dialog').filter({ has: adminPage.locator('#manual-delivery-content') });
      await deliveryDialog.locator('#manual-delivery-content').fill('FIX09 final manual delivery reference');
      await deliveryDialog.locator('#manual-delivery-content').press('Tab');
      await deliveryDialog.getByRole('button', { name: 'ثبت تحویل', exact: true }).click();
      await expect(deliveryDialog).toBeHidden();
      await expect(adminPage.locator('.vz-toast.success')).toContainText('تحویل دستی با موفقیت ثبت شد.');
      await expect(adminPage.locator('.vz-toast.success')).not.toContainText('گیفت');
    } finally {
      await adminContext.close();
    }

    await loginCustomer(page, seededCustomer);
    await page.goto('/customer/notifications', { waitUntil: 'networkidle' });
    const manualNotification = page.locator('.st-card').filter({ hasText: 'تحویل دستی آیتم سفارش P2F-MAIN با موفقیت ثبت شد.' });
    await expect(manualNotification).toHaveCount(1);
    await expect(manualNotification).toContainText('تحویل دستی سفارش');
    await expect(manualNotification).not.toContainText('GiftCode');
    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.locator('.st-card').filter({ hasText: 'تحویل دستی آیتم سفارش P2F-MAIN با موفقیت ثبت شد.' })).toHaveCount(1);
    customerRuntime.assertClean();
    adminRuntime.assertClean();
  });

  test('fresh SupportRequired item -> KYC release -> one support notification', async ({ page, browser }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Final closure runs once in desktop light.');
    const customerContext = await browser.newContext();
    const customerPage = await customerContext.newPage();
    const customerRuntime = monitorRuntime(customerPage);
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    const adminRuntime = monitorRuntime(adminPage);
    try {
      await loginAdmin(adminPage);
      const before = await openAdminOrder(adminPage, 'FIX09-FINAL-SUPPORT');
      await expect(itemRow(before, 'FIX09 Fresh Support Required').locator('.vz-manual-delivery')).toHaveCount(0);

      await adminPage.goto('/admin/verifications', { waitUntil: 'networkidle' });
      await adminPage.locator('input[placeholder*="نام"]').fill(supportCustomer.mobile);
      await adminPage.locator('input[placeholder*="نام"]').press('Tab');
      const profileRow = adminPage.locator('tbody tr').filter({ hasText: 'FIX09 Support Customer' });
      await expect(profileRow).toHaveCount(1);
      await profileRow.locator('button[title="تأیید سریع"]').click();
      await expect(adminPage.locator('.vz-toast.success')).toContainText('احراز هویت تأیید شد.');

      const after = await openAdminOrder(adminPage, 'FIX09-FINAL-SUPPORT');
      const supportItem = itemRow(after, 'FIX09 Fresh Support Required');
      await expect(supportItem).toContainText('پشتیبانی');
      await expect(supportItem.locator('.vz-manual-delivery')).toHaveCount(0);

      await loginCustomer(customerPage, supportCustomer);
      await customerPage.goto('/customer/notifications', { waitUntil: 'networkidle' });
      const supportNotification = customerPage.locator('.st-card').filter({ hasText: 'یک تیکت پشتیبانی جهت تحویل محصول ایجاد شد.' });
      await expect(supportNotification).toHaveCount(1);
      await expect(supportNotification).toContainText('تیکت پشتیبانی ایجاد شد');
      await expect(supportNotification).not.toContainText('GiftCode');
      await customerPage.reload({ waitUntil: 'networkidle' });
      await expect(customerPage.locator('.st-card').filter({ hasText: 'یک تیکت پشتیبانی جهت تحویل محصول ایجاد شد.' })).toHaveCount(1);
      customerRuntime.assertClean();
      adminRuntime.assertClean();
    } finally {
      await customerContext.close();
      await adminContext.close();
    }
  });
});

function monitorRuntime(page: Page) {
  const monitor = monitorBrowser(page);
  const serverErrors: string[] = [];
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 500)
      serverErrors.push(`${response.status()} ${response.url()}`);
  });
  return {
    assertClean: () => {
      monitor.assertClean();
      expect(serverErrors, serverErrors.join('\n')).toEqual([]);
    }
  };
}

async function openAdminOrder(page: Page, number: string) {
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

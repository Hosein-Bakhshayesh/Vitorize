import { expect, test, expectRtlAndNoOverflow } from '../framework/fixtures';
import { readFile } from 'node:fs/promises';
import { apiBaseUrl, loginAdmin, logoutAdmin, logoutCustomer, monitorBrowser } from './support/app';

const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
const uploadFixture = 'D:\\Vitorize\\Vitorize\\Vitorize.Api\\wwwroot\\uploads\\products\\947c2fd1b9a84f2ea86a683008e7fdc0.jpg';
const docA = '31000000000000000000000000000044';
const scenarios = {
  future: { mobile: '09120000051', item: '38000000-0000-0000-0000-000000000091', order: 'FIX09-3BB-FUTURE' },
  overdue: { mobile: '09120000052', item: '38000000-0000-0000-0000-000000000092', order: 'FIX09-3BB-OVERDUE' },
  rejected: { mobile: '09120000053', item: '38000000-0000-0000-0000-000000000093', order: 'FIX09-3BB-REJECTED' },
  expired: { mobile: '09120000054', item: '38000000-0000-0000-0000-000000000094', order: 'FIX09-3BB-EXPIRED' },
  reopen: { mobile: '09120000055', item: '38000000-0000-0000-0000-000000000095', order: 'FIX09-3BB-REOPEN' },
  final: { mobile: '09120000056', item: '38000000-0000-0000-0000-000000000096', order: 'FIX09-3BB-FINAL' },
  review: { mobile: '09120000057', item: '38000000-0000-0000-0000-000000000097', order: 'FIX09-3BB-REVIEW' },
  noDeadline: { mobile: '09120000058', item: '38000000-0000-0000-0000-000000000098', order: 'FIX09-3BB-NODEADLINE' },
  security: { mobile: '09120000060', item: '38000000-0000-0000-0000-000000000100', order: 'FIX09-3BB-SECURITY' }
} as const;

test.describe('FIX-09 Phase 3B-B KYC deadline browser closure @fix09p3bb', () => {
  test.describe.configure({ timeout: 60_000 });

  test('Customer deadline truth is responsive before convergence and for persisted states', async ({ page, consoleGuard }, testInfo) => {
    const monitor = monitorBrowser(page);
    await page.setViewportSize(testInfo.project.name.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });

    await loginCustomer(page, scenarios.future.mobile);
    await verification(page, scenarios.future.item);
    await expect(page.locator('main')).toContainText('مهلت اقدام:');
    await expect(page.locator('main')).not.toContainText('مهلت اقدام این آیتم پایان یافته است');
    await expect(page.locator('button.st-btn--primary')).toBeVisible();
    await logoutCustomer(page);

    // The QA command for this suite sets KycDeadlineProcessing__Enabled=false.
    // Therefore this row is still AwaitingSubmission in storage, while the UI
    // must use the effective deadline projection rather than stale persistence.
    await loginCustomer(page, scenarios.overdue.mobile);
    await verification(page, scenarios.overdue.item);
    await expect(page.locator('main')).toContainText('مهلت اقدام این آیتم پایان یافته است');
    await expect(page.locator('button.st-btn--primary')).toHaveCount(0);
    await expect(page.locator('input[type=file]')).toHaveCount(0);
    await logoutCustomer(page);

    await loginCustomer(page, scenarios.expired.mobile);
    await verification(page, scenarios.expired.item);
    await expect(page.locator('main')).toContainText('مهلت اقدام این آیتم پایان یافته است');
    await expect(page.locator('button.st-btn--primary')).toHaveCount(0);
    await expect(page.locator('input[type=file]')).toHaveCount(0);
    await expectRtlAndNoOverflow(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('Overdue submit, resubmit, and upload are blocked by the real API without the worker', async ({ request }, testInfo) => {
    onceDesktop(testInfo);
    const overdueToken = await tokenFor(request, scenarios.overdue.mobile);
    const rejectedToken = await tokenFor(request, scenarios.rejected.mobile);
    const submit = await request.post(`${apiBaseUrl}/verification/submit`, {
      headers: bearer(overdueToken), data: { firstName: 'Overdue', lastName: 'Customer', nationalCode: '1234567890' }
    });
    expect(submit.status(), await submit.text()).toBe(409);
    const resubmit = await request.post(`${apiBaseUrl}/verification/submit`, {
      headers: bearer(rejectedToken), data: { firstName: 'Rejected', lastName: 'Customer', nationalCode: '1234567890' }
    });
    expect(resubmit.status(), await resubmit.text()).toBe(409);
    const upload = await request.post(`${apiBaseUrl}/uploads/verification-document?orderItemId=${scenarios.overdue.item}`, {
      headers: bearer(overdueToken),
      multipart: { file: { name: 'overdue.jpg', mimeType: 'image/jpeg', buffer: await readFile(uploadFixture) } }
    });
    expect(upload.status(), await upload.text()).toBe(409);
  });

  test('Admin Extend is absolute and customer sees the new active deadline', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    await loginAdmin(page);
    const detail = await openAdminOrder(page, scenarios.future.order);
    const input = detail.locator('input[aria-label="مهلت اقدام مشتری"]');
    const later = futureLocalValue(96);
    await input.fill(later);
    await detail.getByRole('button', { name: 'تمدید مهلت', exact: true }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('مهلت اقدام به‌روزرسانی شد');
    await input.fill(later);
    await detail.getByRole('button', { name: 'تمدید مهلت', exact: true }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('مهلت اقدام به‌روزرسانی شد');
    await closeAdminOrder(detail);
    await logoutAdmin(page);
    await loginCustomer(page, scenarios.future.mobile);
    await page.goto(`/customer/orders/37000000-0000-0000-0000-000000000091`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).toContainText('مهلت اقدام:');
    await expect(page.locator(`a[href="/customer/verification?orderItem=${scenarios.future.item}"]`)).toBeVisible();
  });

  test('Admin Reopen restores the Customer redaction/upload/submit journey', async ({ page, consoleGuard }, testInfo) => {
    onceDesktop(testInfo);
    const monitor = monitorBrowser(page);
    await loginAdmin(page);
    const detail = await openAdminOrder(page, scenarios.reopen.order);
    const input = detail.locator('input[aria-label="مهلت جدید اقدام مشتری"]');
    await input.fill(futureLocalValue(72));
    await detail.getByRole('button', { name: 'بازگشایی', exact: true }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('فرصت اقدام مشتری بازگشایی شد');
    await closeAdminOrder(detail);
    await logoutAdmin(page);
    await loginCustomer(page, scenarios.reopen.mobile);
    await verification(page, scenarios.reopen.item);
    await expect(page.getByTestId(`redaction-open-${docA}`)).toBeVisible();
    await redact(page, docA);
    // Uploading the reopened policy's required document is itself the customer's action: the item
    // moves to review on its own and the form stops offering a submit button. Asserting a separate
    // submit click only ever passed by clicking before that transition committed.
    await expect(page.getByRole('button', { name: 'ثبت اطلاعات احراز هویت', exact: true })).toHaveCount(0);
    await expect(page.locator('main')).toContainText('در انتظار بررسی');
    await expectRtlAndNoOverflow(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('Admin FinalReject produces terminal Customer truth without financial claims', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    await loginAdmin(page);
    const detail = await openAdminOrder(page, scenarios.final.order);
    await detail.getByRole('button', { name: 'رد نهایی', exact: true }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('نهایی');
    await closeAdminOrder(detail);
    await logoutAdmin(page);
    await loginCustomer(page, scenarios.final.mobile);
    await page.goto(`/customer/orders/37000000-0000-0000-0000-000000000096`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).toContainText('رد نهایی');
    await expect(page.locator(`a[href="/customer/verification?orderItem=${scenarios.final.item}"]`)).toHaveCount(0);
    await expect(page.locator('main')).not.toContainText(/بازگشت وجه|لغو پرداخت/);
  });

  test('No-deadline policy remains conventional and cross-customer item upload is protected', async ({ page, request }, testInfo) => {
    onceDesktop(testInfo);
    await loginCustomer(page, scenarios.noDeadline.mobile);
    await verification(page, scenarios.noDeadline.item);
    await expect(page.locator('main')).not.toContainText('مهلت اقدام:');
    await expect(page.locator('input[type=file]')).toHaveCount(1);
    await logoutCustomer(page);
    const token = await tokenFor(request, scenarios.security.mobile);
    const attempt = await request.post(`${apiBaseUrl}/uploads/verification-document?orderItemId=${scenarios.expired.item}`, {
      headers: bearer(token), multipart: { file: { name: 'foreign.jpg', mimeType: 'image/jpeg', buffer: await readFile(uploadFixture) } }
    });
    expect(attempt.status(), await attempt.text()).toBe(404);
  });
});

function onceDesktop(testInfo: import('@playwright/test').TestInfo) {
  test.skip(testInfo.project.name !== 'desktop-light', 'Stateful command coverage runs once; the dedicated UX test is responsive.');
}

async function loginCustomer(page: import('@playwright/test').Page, mobile: string) {
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function verification(page: import('@playwright/test').Page, item: string) {
  await page.goto(`/customer/verification?orderItem=${item}`, { waitUntil: 'networkidle' });
}

async function tokenFor(request: import('@playwright/test').APIRequestContext, mobile: string) {
  const response = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile, password } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { data: { accessToken: string } }).data.accessToken;
}

function bearer(token: string) { return { Authorization: `Bearer ${token}` }; }

function futureLocalValue(hours: number) {
  const local = new Date(Date.now() + hours * 60 * 60 * 1000);
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${local.getFullYear()}-${pad(local.getMonth() + 1)}-${pad(local.getDate())}T${pad(local.getHours())}:${pad(local.getMinutes())}`;
}

async function openAdminOrder(page: import('@playwright/test').Page, number: string) {
  await page.goto('/admin/orders', { waitUntil: 'networkidle' });
  await page.locator('#order-search').fill(number);
  const row = page.locator('tbody tr').filter({ hasText: number });
  await expect(row).toHaveCount(1);
  await row.locator('.vz-ctx__trigger').click();
  await page.locator('.vz-ctx__menu:popover-open .vz-ctx__item').first().click();
  const detail = page.getByRole('dialog').filter({ hasText: number });
  await expect(detail).toBeVisible();
  return detail;
}

async function closeAdminOrder(detail: import('@playwright/test').Locator) {
  await detail.getByRole('button', { name: 'بستن', exact: true }).last().click();
  await expect(detail).toBeHidden();
}

async function redact(page: import('@playwright/test').Page, documentType: string) {
  const chooser = page.waitForEvent('filechooser', { timeout: 10_000 });
  await page.getByTestId(`redaction-open-${documentType}`).click();
  await (await chooser).setFiles({ name: 'fix09-3bb-source.jpg', mimeType: 'image/jpeg', buffer: await readFile(uploadFixture) });
  const canvas = page.locator('.vz-redaction-modal__canvas');
  await expect(canvas).toBeVisible();
  const box = await canvas.boundingBox(); expect(box).not.toBeNull();
  await page.mouse.move(box!.x + box!.width * .25, box!.y + box!.width * .25);
  await page.mouse.down(); await page.mouse.move(box!.x + box!.width * .7, box!.y + box!.height * .7); await page.mouse.up();
  await page.getByRole('dialog').getByRole('button').last().click();
  await expect(page.locator('.vz-redaction-modal')).toHaveCount(0);
  // Closing the modal only starts the upload. The slot keeps offering its "choose and redact"
  // button until the uploaded value comes back, and the form re-renders when it does - so returning
  // at modal-close handed the caller a page that was about to replace the submit button underneath
  // it. Wait for the slot to reach its uploaded state instead.
  await expect(page.getByTestId(`redaction-open-${documentType}`)).toHaveCount(0);
}

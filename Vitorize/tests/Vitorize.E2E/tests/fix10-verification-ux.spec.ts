import { expect, test } from '../framework/fixtures';
import { expectRtlAndNoOverflow, loginAdmin, logoutAdmin, monitorBrowser } from './support/app';

const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
const docA = '31000000000000000000000000000044';
const docB = '31000000000000000000000000000045';
const adminDocA = '31000000-0000-0000-0000-000000000044';
// FIX-10 uses its own Phase-3A-B scenario rows. Sharing DL/ML/MULTI with the Phase-3A specs meant
// whichever ran first spent the upload slot or submitted the profile, leaving this spec a read-only
// form and no upload controls to assert against.
const policy = '35000000-0000-0000-0000-000000000078';

test.describe('FIX-10 Verification DOB and instruction UX @fix10', () => {
  test.describe.configure({ timeout: 90_000 });

  test('desktop Persian DOB entry persists, bounds future values, and uses direct year navigation', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Desktop coverage runs once.');
    const monitor = monitorBrowser(page);
    await login(page, '09120000042');
    await page.goto('/customer/verification?orderItem=38000000-0000-0000-0000-000000000082', { waitUntil: 'networkidle' });
    const dob = page.getByLabel('تاریخ تولد به تقویم شمسی');
    await dob.fill('۱۳۷۵/۰۶/۱۵');
    await page.getByRole('button', { name: 'ثبت اطلاعات احراز هویت', exact: true }).click();
    await expect(page.getByText('اطلاعات احراز هویت ثبت شد.')).toBeVisible();
    await page.reload({ waitUntil: 'networkidle' });
    await expect(dob).toHaveValue('۱۳۷۵/۰۶/۱۵');

    await page.getByRole('button', { name: 'باز کردن تقویم تاریخ تولد' }).click();
    await page.locator('select[aria-label="سال"]').selectOption('1370');
    await expect(page.locator('select[aria-label="سال"]')).toHaveValue('1370');
    await page.locator('div[style*="z-index:115"]').click({ position: { x: 5, y: 5 } });
    await expect(page.locator('select[aria-label="سال"]')).toHaveCount(0);

    await dob.fill('۱۵۰۰/۰۱/۰۱');
    await expect(page.getByText('تاریخ تولد واردشده معتبر نیست.').first()).toBeVisible();
    await expectRtlAndNoOverflow(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('mobile English-digit entry is reachable without overflow', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'Mobile coverage runs once.');
    const monitor = monitorBrowser(page);
    await login(page, '09120000043');
    await page.goto('/customer/verification?orderItem=38000000-0000-0000-0000-000000000083', { waitUntil: 'networkidle' });
    const dob = page.getByLabel('تاریخ تولد به تقویم شمسی');
    await dob.fill('1375/06/15');
    await page.getByRole('button', { name: 'ثبت اطلاعات احراز هویت', exact: true }).click();
    await expect(page.getByText('اطلاعات احراز هویت ثبت شد.')).toBeVisible();
    await page.reload({ waitUntil: 'networkidle' });
    await expect(dob).toHaveValue('۱۳۷۵/۰۶/۱۵');
    await page.getByRole('button', { name: 'باز کردن تقویم تاریخ تولد' }).click();
    await expect(page.locator('select[aria-label="سال"]')).toBeVisible();
    await expectRtlAndNoOverflow(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('customer sees required and optional multiline instructions as text only', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Instruction rendering runs once.');
    const monitor = monitorBrowser(page);
    await login(page, '09120000044');
    await page.goto('/customer/verification?orderItem=38000000-0000-0000-0000-000000000084', { waitUntil: 'networkidle' });
    const required = page.getByTestId(`document-redaction-upload-${docA}`).locator('xpath=..');
    const optional = page.getByTestId(`document-redaction-upload-${docB}`).locator('xpath=..');
    await expect(required).toContainText('راهنمای مدرک الزامی - خط اول');
    await expect(required).toContainText('<strong>این متن باید عادی نمایش داده شود</strong>');
    await expect(optional).toContainText('راهنمای مدرک اختیاری - خط اول');
    await expect(optional).toContainText('خط دوم');
    await expect(page.locator('strong').filter({ hasText: 'این متن باید عادی نمایش داده شود' })).toHaveCount(0);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('Admin draft exposes and persists per-document customer instructions', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin draft coverage runs once.');
    const monitor = monitorBrowser(page);
    await loginAdmin(page);
    await page.goto('/admin/kyc-policies', { waitUntil: 'networkidle' });
    await page.getByTestId(`kyc-policy-create-version-${policy}`).click();
    await page.getByTestId(`kyc-requirement-enabled-${adminDocA}`).locator('xpath=..').click();
    const instructions = page.getByTestId(`kyc-requirement-instructions-${adminDocA}`);
    await expect(instructions).toBeVisible();
    await instructions.fill('FIX10 draft customer guidance\nsecond line');
    await page.getByTestId('kyc-draft-save').click();
    await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
    await expect(page.getByTestId('kyc-draft-save')).toHaveCount(0);
    monitor.assertClean(); consoleGuard.assertClean();
    await logoutAdmin(page);
  });

  test('AwaitingReview profile exposes its DOB but disables all DOB controls', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Read-only state runs once.');
    const monitor = monitorBrowser(page);
    await login(page, '09120000013');
    await page.goto('/customer/verification?orderItem=32000000-0000-0000-0000-000000000102', { waitUntil: 'networkidle' });
    await expect(page.getByLabel('تاریخ تولد به تقویم شمسی')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'باز کردن تقویم تاریخ تولد' })).toBeDisabled();
    await expect(page.getByLabel('تاریخ تولد به تقویم شمسی')).not.toHaveValue('');
    monitor.assertClean(); consoleGuard.assertClean();
  });
});

async function login(page: import('@playwright/test').Page, mobile: string) {
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

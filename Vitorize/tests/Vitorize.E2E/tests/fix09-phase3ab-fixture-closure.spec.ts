import { expect, test, expectRtlAndNoOverflow } from '../framework/fixtures';
import { loginAdmin, logoutAdmin, logoutCustomer, monitorBrowser } from './support/app';
import { readFile } from 'node:fs/promises';

const docA = '31000000000000000000000000000044';
const docB = '31000000000000000000000000000045';
const adminDocA = '31000000-0000-0000-0000-000000000044';
const uploadFixture = 'D:\\Vitorize\\Vitorize\\Vitorize.Api\\wwwroot\\uploads\\products\\947c2fd1b9a84f2ea86a683008e7fdc0.jpg';
const securePreview = 'img[src^="/media/verification-documents/"]';
const scenarios = {
  optionalDirect: { mobile: '09120000035', item: '38000000-0000-0000-0000-000000000075', name: 'FIX09 OPT-DIRECT' },
  optionalRedacted: { mobile: '09120000036', item: '38000000-0000-0000-0000-000000000076', name: 'FIX09-3AB-OPT-REDACT' },
  none: { mobile: '09120000037', item: '38000000-0000-0000-0000-000000000077' },
  v1: { mobile: '09120000038', item: '38000000-0000-0000-0000-000000000078' },
  v2: { mobile: '09120000039', item: '38000000-0000-0000-0000-000000000079' },
  replace: { mobile: '09120000040', item: '38000000-0000-0000-0000-000000000080', name: 'FIX09 REPLACE' },
  multi: { mobile: '09120000041', item: '38000000-0000-0000-0000-000000000081' }
} as const;

test.describe('FIX-09 Phase 3A-B fixture-isolated closure @fix09p3ab', () => {
  test.describe.configure({ timeout: 180_000 });

  test('Optional direct upload and its Admin preview use the normal path', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    const monitor = monitorBrowser(page);
    await loginCustomer(page, scenarios.optionalDirect.mobile);
    await verification(page, scenarios.optionalDirect.item);
    await expect(page.getByTestId(`redaction-direct-${docA}`)).toBeVisible();
    await expect(page.getByTestId(`redaction-open-${docA}`)).toBeVisible();
    await page.getByTestId(`redaction-direct-${docA}`).click();
    await expect(page.locator('input[type=file]')).toHaveCount(1);
    await page.locator('input[type=file]').setInputFiles(uploadFixture);
    await expect(page.locator('input[type=file]')).toHaveCount(0);
    await logoutCustomer(page);
    await loginAdmin(page);
    await openAdminPreview(page, scenarios.optionalDirect.name);
    await expect(page.locator(securePreview)).toBeVisible();
    await closeAdminPreview(page);
    await expectRtlAndNoOverflow(page);
    monitor.assertClean();
    await logoutAdmin(page);
  });

  test('Optional redacted upload is an explicit choice and preserves the normal alternative', async ({ page, consoleGuard }, testInfo) => {
    onceDesktop(testInfo);
    const monitor = monitorBrowser(page);
    await loginCustomer(page, scenarios.optionalRedacted.mobile);
    await verification(page, scenarios.optionalRedacted.item);
    await expect(page.getByTestId(`redaction-direct-${docA}`)).toBeVisible();
    await redact(page, docA);
    await expect(page.locator('input[type=file]')).toHaveCount(0);
    await expectAwaitingReview(page);
    await expectRtlAndNoOverflow(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('None mode retains the conventional upload control without a redaction editor', async ({ page, consoleGuard }, testInfo) => {
    onceDesktop(testInfo);
    const monitor = monitorBrowser(page);
    await loginCustomer(page, scenarios.none.mobile);
    await verification(page, scenarios.none.item);
    await expect(page.getByTestId(`redaction-open-${docA}`)).toHaveCount(0);
    await expect(page.locator('input[type=file]')).toHaveCount(1);
    await page.locator('input[type=file]').setInputFiles(uploadFixture);
    await expectAwaitingReview(page);
    monitor.assertClean(); consoleGuard.assertClean();
  });

  test('V1 and V2 orders retain their independently captured mode and instructions', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    let monitor = monitorBrowser(page);
    await loginCustomer(page, scenarios.v1.mobile);
    await verification(page, scenarios.v1.item);
    await expect(page.locator('main')).toContainText('FIX09-3AB V1 instruction');
    await expect(page.getByTestId(`redaction-direct-${docA}`)).toBeVisible();
    monitor.assertClean();
    await logoutCustomer(page);
    await loginCustomer(page, scenarios.v2.mobile);
    monitor = monitorBrowser(page);
    await verification(page, scenarios.v2.item);
    await expect(page.locator('main')).toContainText('FIX09-3AB V2 instruction');
    await expect(page.getByTestId(`redaction-direct-${docA}`)).toHaveCount(0);
    await expect(page.getByTestId(`redaction-open-${docA}`)).toBeVisible();
    monitor.assertClean();
  });

  test('per-slot modes remain isolated for a multi-document purchased policy', async ({ page, consoleGuard }, testInfo) => {
    onceDesktop(testInfo);
    await loginCustomer(page, scenarios.multi.mobile);
    await verification(page, scenarios.multi.item);
    await expect(page.getByTestId(`redaction-open-${docA}`)).toBeVisible();
    await expect(page.getByTestId(`redaction-open-${docB}`)).toHaveCount(0);
    const documentBUpload = page.getByTestId(`document-redaction-upload-${docB}`).locator('input[type=file]');
    await expect(documentBUpload).toHaveCount(1);
    await documentBUpload.setInputFiles(uploadFixture);
    await expect(page.getByTestId(`redaction-open-${docA}`)).toBeVisible();
    await redact(page, docA);
    await expectAwaitingReview(page);
    consoleGuard.assertClean();
  });

  test('redacted reject, replacement, resubmission, and Admin preview complete on one isolated item', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    let monitor = monitorBrowser(page);
    await loginCustomer(page, scenarios.replace.mobile);
    await verification(page, scenarios.replace.item);
    await redact(page, docA);
    await expectAwaitingReview(page);
    monitor.assertClean();
    await logoutCustomer(page);
    await loginAdmin(page);
    monitor = monitorBrowser(page);
    await page.goto('/admin/verifications', { waitUntil: 'networkidle' });
    const row = page.locator('tbody tr').filter({ hasText: scenarios.replace.name });
    await expect(row).toHaveCount(1);
    await row.locator('button.vz-btn--outline').click();
    await expect(page.locator(securePreview)).toBeVisible();
    await page.locator('textarea.vz-textarea').fill('FIX09 replacement rejection');
    await page.locator('button.vz-btn--danger').click();
    monitor.assertClean();
    await logoutAdmin(page);
    await loginCustomer(page, scenarios.replace.mobile);
    monitor = monitorBrowser(page);
    await verification(page, scenarios.replace.item);
    const remove = page.locator('button.st-btn--surface-danger');
    await expect(remove).toHaveCount(1);
    await remove.click();
    await redact(page, docA);
    await expectAwaitingReview(page);
    monitor.assertClean();
    await logoutCustomer(page);
    await loginAdmin(page);
    monitor = monitorBrowser(page);
    await openAdminPreview(page, scenarios.replace.name);
    await expect(page.locator(securePreview)).toHaveCount(1);
    await page.locator('button.vz-btn--success').click();
    monitor.assertClean();
    await logoutAdmin(page);
  });

  test('published policy is read-only while a newly created draft exposes redaction controls', async ({ page }, testInfo) => {
    onceDesktop(testInfo);
    const monitor = monitorBrowser(page);
    await loginAdmin(page);
    await page.goto('/admin/kyc-policies', { waitUntil: 'networkidle' });
    const publishedVersion = '36000000-0000-0000-0000-000000000078';
    const policy = '35000000-0000-0000-0000-000000000078';
    await expect(page.getByTestId(`kyc-version-edit-${publishedVersion}`)).toHaveCount(0);
    await page.getByTestId(`kyc-policy-create-version-${policy}`).click();
    // The native checkbox is intentionally hidden inside the visible custom-switch label.
    await page.getByTestId(`kyc-requirement-enabled-${adminDocA}`).locator('xpath=..').click();
    await expect(page.getByTestId(`kyc-redaction-mode-${adminDocA}`)).toBeVisible();
    await expect(page.getByTestId(`kyc-redaction-instructions-${adminDocA}`)).toBeVisible();
    await page.getByTestId(`kyc-redaction-mode-${adminDocA}`).selectOption('2');
    await page.getByTestId(`kyc-redaction-instructions-${adminDocA}`).fill('FIX09-3AB draft instruction');
    await page.getByTestId('kyc-draft-save').click();
    const policyRow = page.getByTestId(`kyc-policy-${policy}`);
    await policyRow.getByTestId(/kyc-version-publish-/).click();
    await expect(policyRow.getByTestId(/kyc-version-edit-/)).toHaveCount(0);
    monitor.assertClean();
    await logoutAdmin(page);
  });
});

function onceDesktop(testInfo: import('@playwright/test').TestInfo) {
  test.skip(testInfo.project.name !== 'desktop-light', 'This isolated business journey runs once; responsive Required coverage is in the dedicated matrix.');
}

async function loginCustomer(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function verification(page: import('@playwright/test').Page, item: string) {
  await page.goto(`/customer/verification?orderItem=${item}`, { waitUntil: 'networkidle' });
}

async function redact(page: import('@playwright/test').Page, documentType: string) {
  await expect(page.getByTestId(`redaction-source-${documentType}`)).toBeAttached();
  const chooser = page.waitForEvent('filechooser', { timeout: 10_000 });
  await page.getByTestId(`redaction-open-${documentType}`).click();
  await (await chooser).setFiles({ name: 'fix09-3ab-source.jpg', mimeType: 'image/jpeg', buffer: await readFile(uploadFixture) });
  const canvas = page.locator('.vz-redaction-modal__canvas');
  await expect(canvas).toBeVisible();
  const box = await canvas.boundingBox(); expect(box).not.toBeNull();
  await page.mouse.move(box!.x + box!.width * .25, box!.y + box!.height * .25);
  await page.mouse.down(); await page.mouse.move(box!.x + box!.width * .7, box!.y + box!.height * .7); await page.mouse.up();
  await page.getByRole('button', {
    name: 'تأیید و آپلود نسخه پوشانده‌شده',
    exact: true
  }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
}

async function expectAwaitingReview(page: import('@playwright/test').Page) {
  // Outcome, not layout: the ORDER-ITEM panel (several info alerts exist on the page) lists each
  // document with its upload state, so a completed upload reads «بارگذاری شده» there.
  await expect(page.locator('.st-alert--info').filter({ hasText: 'آیتم سفارش' }))
    .toContainText('بارگذاری شده');
}

async function openAdminPreview(page: import('@playwright/test').Page, name: string) {
  await page.goto('/admin/verifications', { waitUntil: 'networkidle' });
  const row = page.locator('tbody tr').filter({ hasText: name });
  await expect(row).toHaveCount(1);
  await row.locator('button.vz-btn--outline').click();
}

async function closeAdminPreview(page: import('@playwright/test').Page) {
  const panel = page.locator('.vz-slidepanel');
  await panel.locator('.vz-slidepanel__close').click();
  await expect(panel).toHaveCount(0);
}

import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, adminMobile, adminPassword } from './support/app';

const seededDraftVersion = '31000000-0000-0000-0000-000000000046';

type DocumentType = { id: string; code: string; title: string; description: string | null; isActive: boolean; allowedExtensions: string; maxFileSizeBytes: number; sortOrder: number };
type Requirement = { kycDocumentTypeId: string; documentTypeCode: string; documentTypeTitle: string; isRequired: boolean; sortOrder: number };
type Version = { id: string; version: number; status: number; customerTitle: string; customerInstructions: string | null; documentRequirements: Requirement[] };
type Policy = { id: string; code: string; name: string; versions: Version[] };

test.describe('FIX-09 Phase 1 Admin KYC policy/version/document workflow @fix09p1kyc', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop light creates, publishes and preserves V1/V2 KYC history', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'The full lifecycle is exercised once in desktop light.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');
    await page.goto('/admin/kyc-policies');
    await expect(page.getByTestId('kyc-document-save')).toBeVisible();

    const suffix = Date.now().toString(36);
    const documentACode = `national-card-e2e-${suffix}`;
    const documentBCode = `address-card-e2e-${suffix}`;
    const policyCode = `kyc-policy-e2e-${suffix}`;
    const v1Title = 'احراز هویت خرید V1';
    const v1Instructions = 'راهنمای فارسی نسخه اول برای احراز هویت.';
    const v2Instructions = 'راهنمای فارسی تغییرکرده در نسخه دوم.';

    await createDocument(page, documentACode, 'کارت ملی تست', 'مدرک هویتی اصلی', 10);
    let documentA = await getDocument(request, documentACode);
    expect(documentA.allowedExtensions).toBe('jpg,jpeg,png,webp');
    expect(documentA.allowedExtensions).not.toMatch(/pdf/i);
    await createDocument(page, documentACode, 'نباید ذخیره شود', 'duplicate', 11, false);
    await expect(page.locator('.vz-toast.error, .vz-toast--error').last()).toBeVisible();
    expect((await getDocument(request, documentACode)).title).toBe('کارت ملی تست');
    await createDocument(page, documentBCode, 'نشانی تست', 'مدرک اختیاری', 20);
    const documentB = await getDocument(request, documentBCode);

    await page.getByTestId(`kyc-document-edit-${documentA.id}`).click();
    await page.getByTestId('kyc-document-title').fill('کارت ملی ویرایش‌شده');
    await page.getByTestId('kyc-document-description').fill('توضیحات ویرایش‌شده');
    await page.getByTestId('kyc-document-sort').fill('15');
    await saveToast(page, 'kyc-document-save', '/admin/kyc/document-types/', 'PUT');
    documentA = await getDocument(request, documentACode);
    expect(documentA).toMatchObject({ title: 'کارت ملی ویرایش‌شده', description: 'توضیحات ویرایش‌شده', sortOrder: 15, isActive: true });

    await page.getByTestId('kyc-policy-code').fill(policyCode);
    await page.getByTestId('kyc-policy-name').fill(`سیاست احراز هویت ${suffix}`);
    await page.getByTestId('kyc-policy-title').fill(v1Title);
    await page.getByTestId('kyc-policy-instructions').fill(v1Instructions);
    await saveToast(page, 'kyc-policy-create', '/admin/kyc/policies', 'POST');
    let policy = await getPolicy(request, policyCode);
    const v1 = policy.versions.find(x => x.version === 1)!;
    expect(v1.status).toBe(1);

    await editVersion(page, v1.id);
    await setRequirement(page, documentA.id, true, true, 1);
    await setRequirement(page, documentB.id, true, false, 2);
    await saveToast(page, 'kyc-draft-save', '/document-requirements', 'PUT');
    expectRequirements((await getVersion(request, v1.id)).documentRequirements, documentA, documentB, false);

    // Draft mutability and removal/re-add lifecycle for the optional requirement.
    await editVersion(page, v1.id);
    await setChecked(page.getByTestId(`kyc-requirement-required-${documentB.id}`), true);
    await saveToast(page, 'kyc-draft-save', '/document-requirements', 'PUT');
    expect((await getVersion(request, v1.id)).documentRequirements.find(x => x.kycDocumentTypeId === documentB.id)?.isRequired).toBe(true);
    await editVersion(page, v1.id);
    await setChecked(page.getByTestId(`kyc-requirement-enabled-${documentB.id}`), false);
    await saveToast(page, 'kyc-draft-save', '/document-requirements', 'PUT');
    expect((await getVersion(request, v1.id)).documentRequirements).toHaveLength(1);
    await editVersion(page, v1.id);
    await setRequirement(page, documentB.id, true, false, 2);
    await saveToast(page, 'kyc-draft-save', '/document-requirements', 'PUT');
    const v1Snapshot = await getVersion(request, v1.id);
    expectRequirements(v1Snapshot.documentRequirements, documentA, documentB, false);

    await clickAndExpectApi(page, `kyc-version-publish-${v1.id}`, `/policy-versions/${v1.id}/publish`, 'POST');
    await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
    await page.reload();
    expect((await getVersion(request, v1.id)).status).toBe(2);
    await expect(page.getByTestId(`kyc-version-edit-${v1.id}`)).toHaveCount(0);
    await expect(page.getByTestId(`kyc-version-publish-${v1.id}`)).toHaveCount(0);

    policy = await getPolicy(request, policyCode);
    await clickAndExpectApi(page, `kyc-policy-create-version-${policy.id}`, `/policies/${policy.id}/versions`, 'POST');
    await expect(page.getByTestId('kyc-draft-title')).toBeVisible();
    policy = await getPolicy(request, policyCode);
    const v2 = policy.versions.find(x => x.version === 2)!;
    expect(v2.status).toBe(1);
    await page.getByTestId('kyc-draft-title').fill('احراز هویت خرید V2');
    await page.getByTestId('kyc-draft-instructions').fill(v2Instructions);
    await setRequirement(page, documentA.id, true, true, 1);
    await setRequirement(page, documentB.id, true, true, 2);
    await saveToast(page, 'kyc-draft-save', '/document-requirements', 'PUT');
    expectRequirements((await getVersion(request, v2.id)).documentRequirements, documentA, documentB, true);
    await clickAndExpectApi(page, `kyc-version-publish-${v2.id}`, `/policy-versions/${v2.id}/publish`, 'POST');
    await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();

    const publishedV1 = await getVersion(request, v1.id);
    const publishedV2 = await getVersion(request, v2.id);
    expect(publishedV1).toMatchObject({ status: 2, customerTitle: v1Title, customerInstructions: v1Instructions });
    expectRequirements(publishedV1.documentRequirements, documentA, documentB, false);
    expect(publishedV2).toMatchObject({ status: 2, version: 2, customerInstructions: v2Instructions });
    expectRequirements(publishedV2.documentRequirements, documentA, documentB, true);
    policy = await getPolicy(request, policyCode);
    expect(policy.versions).toHaveLength(2);
    expect(policy.versions.map(x => [x.version, x.status])).toEqual([[2, 2], [1, 2]]);
    consoleGuard.assertClean();
  });

  test('desktop dark renders KYC policy controls readably', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-dark', 'Dark representative check only.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');
    await page.goto('/admin/kyc-policies');
    await expect(page.getByTestId('kyc-policy-create')).toBeVisible();
    await expect(page.getByTestId('kyc-document-save')).toBeVisible();
    await expect(page.locator('.vz-card')).toHaveCount(3);
    consoleGuard.assertClean();
  });

  test('compact Admin keeps draft requirement controls and actions reachable', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'Compact representative check only.');
    await page.setViewportSize({ width: 393, height: 852 });
    await loginAs('SuperAdmin');
    await page.goto('/admin/kyc-policies');
    await page.getByTestId(`kyc-version-edit-${seededDraftVersion}`).click();
    await expect(page.getByTestId('kyc-draft-title')).toBeVisible();
    await expect(page.getByTestId('kyc-draft-save')).toBeVisible();
    const layout = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(layout).toBeLessThanOrEqual(1);
    consoleGuard.assertClean();
  });
});

async function createDocument(page: import('@playwright/test').Page, code: string, title: string, description: string, sort: number, expectSuccess = true) {
  await page.getByTestId('kyc-document-code').fill(code);
  await page.getByTestId('kyc-document-title').fill(title);
  await page.getByTestId('kyc-document-description').fill(description);
  await page.getByTestId('kyc-document-extensions').fill('jpg,jpeg,png,webp');
  await page.getByTestId('kyc-document-max-size').fill('5242880');
  await page.getByTestId('kyc-document-sort').fill(String(sort));
  await setChecked(page.getByTestId('kyc-document-active'), true);
  await clickAndExpectApi(page, 'kyc-document-save', '/admin/kyc/document-types', 'POST');
  await expect(page.locator(expectSuccess ? '.vz-toast.success, .vz-toast--success' : '.vz-toast.error, .vz-toast--error').last()).toBeVisible();
}

async function editVersion(page: import('@playwright/test').Page, versionId: string) {
  await page.getByTestId(`kyc-version-edit-${versionId}`).click();
  await expect(page.getByTestId('kyc-draft-title')).toBeVisible();
}

async function setRequirement(page: import('@playwright/test').Page, documentId: string, enabled: boolean, required: boolean, sort: number) {
  const toggle = page.getByTestId(`kyc-requirement-enabled-${documentId}`);
  await setChecked(toggle, enabled);
  if (enabled) {
    const requiredToggle = page.getByTestId(`kyc-requirement-required-${documentId}`);
    await setChecked(requiredToggle, required);
    await page.getByTestId(`kyc-requirement-sort-${documentId}`).fill(String(sort));
  }
}

async function setChecked(locator: import('@playwright/test').Locator, value: boolean) {
  await locator.evaluate((input, desired) => {
    const checkbox = input as HTMLInputElement;
    if (checkbox.checked !== desired) checkbox.click();
  }, value);
}

async function saveToast(page: import('@playwright/test').Page, testId: string, urlFragment: string, method: string) {
  await clickAndExpectApi(page, testId, urlFragment, method);
  await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
}

async function clickAndExpectApi(page: import('@playwright/test').Page, testId: string, urlFragment: string, method: string) {
  await page.getByTestId(testId).click();
  await page.waitForTimeout(750);
}

function expectRequirements(requirements: Requirement[], documentA: DocumentType, documentB: DocumentType, bRequired: boolean) {
  expect(requirements.map(x => [x.kycDocumentTypeId, x.isRequired, x.sortOrder])).toEqual([
    [documentA.id, true, 1], [documentB.id, bRequired, 2]
  ]);
}

async function token(request: import('@playwright/test').APIRequestContext) {
  const response = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: adminMobile, password: adminPassword } });
  expect(response.ok()).toBeTruthy();
  const data = ((await response.json()) as { data: { accessToken?: string } }).data;
  expect(data.accessToken).toBeTruthy();
  return data.accessToken!;
}

async function api<T>(request: import('@playwright/test').APIRequestContext, route: string): Promise<T> {
  const response = await request.get(`${apiBaseUrl}${route}`, { headers: { Authorization: `Bearer ${await token(request)}` } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: T }).data;
}

async function getDocument(request: import('@playwright/test').APIRequestContext, code: string) {
  const types = await api<DocumentType[]>(request, '/admin/kyc/document-types');
  const item = types.find(x => x.code === code);
  expect(item).toBeTruthy();
  return item!;
}

async function getPolicy(request: import('@playwright/test').APIRequestContext, code: string) {
  const policies = await api<Policy[]>(request, '/admin/kyc/policies');
  const item = policies.find(x => x.code === code);
  expect(item).toBeTruthy();
  return item!;
}

async function getVersion(request: import('@playwright/test').APIRequestContext, id: string) {
  return api<Version>(request, `/admin/kyc/policy-versions/${id}`);
}

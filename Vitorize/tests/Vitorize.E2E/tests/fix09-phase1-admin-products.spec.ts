import { expect, test, ProductBuilder, type AdminProductPage } from '../framework/fixtures';
import { apiBaseUrl, adminMobile, adminPassword } from './support/app';

const publishedPolicyV1 = '31000000-0000-0000-0000-000000000042';
const draftPolicy = '31000000-0000-0000-0000-000000000046';

type PersistedProduct = {
  id: string;
  requiresVerification: boolean;
  kycRequirementMode: number;
  kycThresholdAmount: number | null;
  kycPolicyVersionId: string | null;
};

test.describe('FIX-09 Phase 1 Admin Product KYC configuration @fix09p1admin', () => {
  test.describe.configure({ timeout: 180_000 });

  test('desktop Create/Edit persists canonical None, Always and AboveThreshold states', async ({
    page, request, loginAs, adminProduct, consoleGuard
  }, testInfo) => {
    test.skip(testInfo.project.name.startsWith('mobile'), 'Desktop matrix covers the complete create/edit flow.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');
    await assertPolicySelectorContract(page);
    await expect(page.getByTestId('product-save')).toHaveCount(1);
    await expect(page.locator('.vz-product-header-save:visible')).toHaveCount(0);
    await expect(page.locator('input[type="checkbox"]').filter({ has: page.getByText('نیازمند احراز هویت') })).toHaveCount(0);

    const suffix = `${Date.now().toString(36)}-${testInfo.project.name}`;
    const none = product(`fix09-admin-none-${suffix}`, `FIX09 None ${suffix}`);
    const always = product(`fix09-admin-always-${suffix}`, `FIX09 Always ${suffix}`);
    const above = product(`fix09-admin-above-${suffix}`, `FIX09 Above ${suffix}`);

    const noneId = await createWithMode(adminProduct, none, '0');
    await expectPersisted(await getProduct(request, noneId), { mode: 0, threshold: null, policy: null, legacy: false });
    await adminProduct.openEdit(noneId);
    await page.getByTestId('product-kyc-mode').selectOption('0');
    await expect(threshold(page)).toHaveCount(0);
    await expect(page.getByTestId('product-kyc-policy')).toHaveCount(0);

    await adminProduct.openCreate();
    await adminProduct.fill(always);
    await page.getByTestId('product-kyc-mode').selectOption('1');
    await adminProduct.saveExpectingError();
    await expect(page).not.toHaveURL(/error|exception/i);
    await page.getByTestId('product-kyc-policy').selectOption(publishedPolicyV1);
    await Promise.all([
      page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/i),
      page.getByTestId('product-save').click()
    ]);
    const alwaysId = idFromUrl(page.url());
    await expect(page.getByTestId('product-kyc-policy')).toHaveValue(publishedPolicyV1);
    await expect(threshold(page)).toHaveCount(0);
    await expectPersisted(await getProduct(request, alwaysId), { mode: 1, threshold: null, policy: publishedPolicyV1, legacy: true });

    await adminProduct.openCreate();
    await adminProduct.fill(above);
    await page.getByTestId('product-kyc-mode').selectOption('2');
    await threshold(page).fill('4000');
    await adminProduct.saveExpectingError();
    await expect(page).not.toHaveURL(/error|exception/i);
    await page.getByTestId('product-kyc-policy').selectOption(publishedPolicyV1);
    await expect(threshold(page)).toBeVisible();
    await expect(currencyContext(page)).toBeVisible();
    await expect(page.getByTestId('product-currency')).toBeVisible();
    for (const invalid of ['', '0', '-1']) {
      await threshold(page).fill(invalid);
      await adminProduct.saveExpectingError();
      await expect(page).not.toHaveURL(/error|exception/i);
    }
    await threshold(page).fill('4000');
    await Promise.all([
      page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/i),
      page.getByTestId('product-save').click()
    ]);
    const aboveId = idFromUrl(page.url());
    await page.reload();
    await expect(page.getByTestId('product-kyc-mode')).toHaveValue('2');
    await expect(threshold(page)).toHaveValue(/^4000(?:\.0+)?$/);
    await expect(page.getByTestId('product-kyc-policy')).toHaveValue(publishedPolicyV1);
    await expectPersisted(await getProduct(request, aboveId), { mode: 2, threshold: 4000, policy: publishedPolicyV1, legacy: true });

    // None -> Always: the selected published policy is canonical and threshold remains inactive.
    await adminProduct.openEdit(noneId);
    await page.getByTestId('product-kyc-mode').selectOption('1');
    await page.getByTestId('product-kyc-policy').selectOption(publishedPolicyV1);
    await adminProduct.saveEdit();
    await page.reload();
    await expectPersisted(await getProduct(request, noneId), { mode: 1, threshold: null, policy: publishedPolicyV1, legacy: true });

    // Always -> AboveThreshold, then AboveThreshold -> Always, then reset to None.
    await adminProduct.openEdit(alwaysId);
    await page.getByTestId('product-kyc-mode').selectOption('2');
    await threshold(page).fill('4000');
    await adminProduct.saveEdit();
    await page.reload();
    await expectPersisted(await getProduct(request, alwaysId), { mode: 2, threshold: 4000, policy: publishedPolicyV1, legacy: true });

    await adminProduct.openEdit(aboveId);
    await page.getByTestId('product-kyc-mode').selectOption('1');
    await adminProduct.saveEdit();
    await page.reload();
    await expectPersisted(await getProduct(request, aboveId), { mode: 1, threshold: null, policy: publishedPolicyV1, legacy: true });
    await page.getByTestId('product-kyc-mode').selectOption('2');
    await threshold(page).fill('4000');
    await adminProduct.saveEdit();
    await page.getByTestId('product-kyc-mode').selectOption('0');
    await adminProduct.saveEdit();
    await page.reload();
    await expect(threshold(page)).toHaveCount(0);
    await expect(page.getByTestId('product-kyc-policy')).toHaveCount(0);
    await expectPersisted(await getProduct(request, aboveId), { mode: 0, threshold: null, policy: null, legacy: false });

    consoleGuard.assertClean();
  });

  test('compact Admin keeps the AboveThreshold controls and Save reachable', async ({
    page, request, loginAs, adminProduct, consoleGuard
  }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'Compact smoke is exercised once in the mobile project.');
    await page.setViewportSize({ width: 393, height: 852 });
    await loginAs('SuperAdmin');
    const suffix = `${Date.now().toString(36)}-compact`;
    const input = product(`fix09-admin-compact-${suffix}`, `FIX09 Compact ${suffix}`);
    const productId = await createWithMode(adminProduct, input, '2', '4000');
    await adminProduct.openEdit(productId);
    await expect(page.getByTestId('product-kyc-mode')).toBeVisible();
    await expect(page.getByTestId('product-kyc-policy')).toBeVisible();
    await expect(threshold(page)).toBeVisible();
    await expect(page.getByTestId('product-save')).toBeVisible();
    const layout = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth }));
    expect(layout.overflow).toBeLessThanOrEqual(1);
    await threshold(page).fill('4500');
    await adminProduct.saveEdit();
    await page.reload();
    await expectPersisted(await getProduct(request, productId), { mode: 2, threshold: 4500, policy: publishedPolicyV1, legacy: true });
    consoleGuard.assertClean();
  });
});

function product(slug: string, title: string) {
  return new ProductBuilder(slug, title).ofType(99).deliveredBy(1).withoutBrand().priced(5_000).build();
}

async function createWithMode(adminProduct: AdminProductPage, input: ReturnType<typeof product>, mode: '0' | '1' | '2', value = '4000') {
  await adminProduct.openCreate();
  await adminProduct.fill(input);
  const page = (adminProduct as unknown as { page: import('@playwright/test').Page }).page;
  await page.getByTestId('product-kyc-mode').selectOption(mode);
  if (mode !== '0') await page.getByTestId('product-kyc-policy').selectOption(publishedPolicyV1);
  if (mode === '2') await threshold(page).fill(value);
  await Promise.all([
    page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/i),
    page.getByTestId('product-save').click()
  ]);
  return idFromUrl(page.url());
}

function threshold(page: import('@playwright/test').Page) {
  return page.locator('.vz-field').filter({ hasText: 'آستانه مبلغ' }).locator('input');
}

function currencyContext(page: import('@playwright/test').Page) {
  return threshold(page).locator('xpath=..');
}

function idFromUrl(url: string) {
  const id = /\/admin\/products\/([0-9a-f-]{36})$/i.exec(url)?.[1];
  if (!id) throw new Error(`Product id missing from ${url}`);
  return id;
}

async function assertPolicySelectorContract(page: import('@playwright/test').Page) {
  await page.goto('/admin/products/create');
  await page.getByTestId('product-kyc-mode').selectOption('1');
  const policy = page.getByTestId('product-kyc-policy');
  await expect(policy.locator(`option[value="${publishedPolicyV1}"]`)).toHaveCount(1);
  const values = await policy.locator('option').evaluateAll(options =>
    options.map(option => (option as HTMLOptionElement).value));
  expect(values).toContain(publishedPolicyV1);
  expect(values).not.toContain(draftPolicy);
}

async function getProduct(request: import('@playwright/test').APIRequestContext, id: string): Promise<PersistedProduct> {
  const login = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: adminMobile, password: adminPassword } });
  expect(login.ok()).toBeTruthy();
  const token = ((await login.json()) as { data: { accessToken?: string; token?: string; jwtToken?: string } }).data;
  const accessToken = token.accessToken ?? token.token ?? token.jwtToken;
  expect(accessToken).toBeTruthy();
  const response = await request.get(`${apiBaseUrl}/admin/products/${id}`, { headers: { Authorization: `Bearer ${accessToken}` } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { data: PersistedProduct }).data;
}

async function expectPersisted(product: PersistedProduct, expected: { mode: number; threshold: number | null; policy: string | null; legacy: boolean }) {
  expect(product.kycRequirementMode).toBe(expected.mode);
  expect(product.kycThresholdAmount).toBe(expected.threshold);
  expect(product.kycPolicyVersionId).toBe(expected.policy);
  expect(product.requiresVerification).toBe(expected.legacy);
}

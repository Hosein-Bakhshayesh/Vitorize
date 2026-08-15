import { expect, test, clearCustomerCart, expectRtlAndNoOverflow } from '../framework/fixtures';
import { loginAdmin, logoutAdmin, logoutCustomer, monitorBrowser } from './support/app';

// Phase-2G's paid allocation fixture belongs to the above-threshold instant
// product. The similarly named "always" product is deliberately manual and
// is covered by the Phase-1 policy tests instead.
const productSlug = 'e2e-fix09-above';
const uploadFixture = 'D:\\Vitorize\\Vitorize\\Vitorize.Api\\wwwroot\\uploads\\products\\947c2fd1b9a84f2ea86a683008e7fdc0.jpg';
const primaryCustomers = {
  'desktop-light': { mobile: '09120000015', name: 'P2G Browser Desktop Light' },
  'desktop-dark': { mobile: '09120000016', name: 'P2G Browser Desktop Dark' },
  'mobile-light': { mobile: '09120000017', name: 'P2G Browser Mobile Light' },
  'mobile-dark': { mobile: '09120000018', name: 'P2G Browser Mobile Dark' }
} as const;

test.describe('FIX-09 Phase 2G real post-payment KYC @fix09p2g', () => {
  test.describe.configure({ timeout: 180_000 });

  test('real Customer checkout, upload, Admin approval, and allocated code reveal', async ({ page, consoleGuard }, testInfo) => {
    const monitor = monitorBrowser(page);
    const customer = primaryCustomers[testInfo.project.name as keyof typeof primaryCustomers];
    await page.setViewportSize(testInfo.project.name.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
    await loginCustomer(page, customer.mobile);
    await clearCustomerCart(page);
    await addProduct(page);
    await page.goto('/cart', { waitUntil: 'networkidle' });
    await page.locator('.st-cart-sum button.st-btn--accent').click();
    await expect(page.getByTestId('checkout-kyc-information')).toBeVisible();
    await expect(page.getByTestId('checkout-kyc-post-payment-copy')).not.toBeEmpty();
    await expect(page.locator('.phase2g-kyc-information a')).not.toBeVisible();
    await expectRtlAndNoOverflow(page);

    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
    const orderId = new URL(page.url()).searchParams.get('orderId')!;
    const orderAction = page.locator(`a[href="/customer/orders/${orderId}"]`);
    await expect(orderAction).toBeVisible();
    await orderAction.click();
    await expect(page.locator('main')).toContainText('2F V2 Purchase Policy');
    await expect(page.locator('main')).not.toContainText(/(?:P2F|P2G)-CHECKOUT-/);
    const kycAction = page.locator('a[href^="/customer/verification?orderItem="]');
    await expect(kycAction).toBeVisible();
    await kycAction.click();
    await expect(page.locator('main')).toContainText('2F V2 Purchase Policy');
    await uploadAndSubmit(page, 'P2G', `${testInfo.project.name} Happy`);
    await expect(page.locator('button.st-btn--primary')).toHaveCount(0);
    await expect(page.locator('main')).not.toContainText(/(?:P2F|P2G)-CHECKOUT-/);

    await logoutCustomer(page);
    await loginAdmin(page);
    await reviewInAdmin(page, `P2G ${testInfo.project.name} Happy`, true);
    await logoutAdmin(page);
    await loginCustomer(page, customer.mobile);
    await page.goto(`/customer/orders/${orderId}`, { waitUntil: 'networkidle' });
    await expect(page.locator('a[href^="/customer/verification?orderItem="]')).toHaveCount(0);
    await expect(page.locator('main')).toContainText(/(?:P2F|P2G)-CHECKOUT-/);
    await expectRtlAndNoOverflow(page);
    monitor.assertClean();
    consoleGuard.assertClean();
  });

  test('real paid order is rejected and resubmitted through the same purchase-time context', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Executed once through the real desktop Customer/Admin UI.');
    const monitor = monitorBrowser(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginCustomer(page, '09120000019');
    await clearCustomerCart(page);
    await addProduct(page);
    await page.goto('/cart', { waitUntil: 'networkidle' });
    await page.locator('.st-cart-sum button.st-btn--accent').click();
    await page.locator('button.st-btn--accent').last().click();
    await expect(page).toHaveURL(/\/payment\/result\?orderId=.*paid=1/);
    const orderId = new URL(page.url()).searchParams.get('orderId')!;
    await page.locator(`a[href="/customer/orders/${orderId}"]`).click();
    await page.locator('a[href^="/customer/verification?orderItem="]').click();
    await uploadAndSubmit(page, 'P2G', 'Reject');
    await logoutCustomer(page);
    await loginAdmin(page);
    await reviewInAdmin(page, 'P2G Reject', false);
    await logoutAdmin(page);
    await loginCustomer(page, '09120000019');
    await page.goto(`/customer/orders/${orderId}`, { waitUntil: 'networkidle' });
    await expect(page.locator('main')).not.toContainText(/(?:P2F|P2G)-CHECKOUT-/);
    const resubmit = page.locator('a[href^="/customer/verification?orderItem="]');
    await expect(resubmit).toBeVisible();
    await resubmit.click();
    await expect(page.locator('main')).toContainText('2F V2 Purchase Policy');
    await replaceRejectedDocuments(page);
    await expect(page.locator('button.st-btn--primary')).toHaveCount(0);
    monitor.assertClean();
    consoleGuard.assertClean();
  });

  test('FinalRejected and mixed/Manual item cards retain item-level truth', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Fixture state smoke runs once.');
    await loginCustomer(page, '09120000013');
    await page.goto('/customer/orders/32000000-0000-0000-0000-000000000001', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Final Rejected' }).locator('a[href*="verification"]')).toHaveCount(0);
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Final Rejected' })).not.toContainText(/(?:P2F|P2G)-CHECKOUT-/);
    await page.goto('/customer/orders/32000000-0000-0000-0000-000000000003', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Awaiting Submission' }).locator('a[href*="verification"]')).toBeVisible();
    await expect(page.locator('.st-card').filter({ hasText: 'P2F Mixed Manual Pending' }).locator('a[href*="verification"]')).toHaveCount(0);
    await expectRtlAndNoOverflow(page);
    consoleGuard.assertClean();
  });
});

async function loginCustomer(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function addProduct(page: import('@playwright/test').Page) {
  await page.goto(`/product/${productSlug}`, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success, .vz-toast--success').last()).toBeVisible();
}

async function uploadAndSubmit(page: import('@playwright/test').Page, firstName: string, lastName: string) {
  const inputs = page.locator('input.st-input');
  await inputs.nth(0).fill(firstName); await inputs.nth(1).fill(lastName); await inputs.nth(2).fill('1234567890');
  await page.locator('button.st-btn--primary').click();
  await expect(page.locator('input[type="file"]')).toHaveCount(2);
  await page.locator('input[type="file"]').nth(0).setInputFiles(uploadFixture);
  await expect(page.locator('input[type="file"]')).toHaveCount(1);
  await page.locator('input[type="file"]').nth(0).setInputFiles(uploadFixture);
  await expect(page.locator('input[type="file"]')).toHaveCount(0);
}

async function replaceRejectedDocuments(page: import('@playwright/test').Page) {
  const deleteButtons = page.locator('button.st-btn--surface-danger');
  await expect(deleteButtons).toHaveCount(2);
  await deleteButtons.first().click();
  await expect(deleteButtons).toHaveCount(1);
  await deleteButtons.first().click();
  await expect(deleteButtons).toHaveCount(0);
  await expect(page.locator('input[type="file"]')).toHaveCount(2);
  await page.locator('input[type="file"]').nth(0).setInputFiles(uploadFixture);
  await expect(page.locator('input[type="file"]')).toHaveCount(1);
  await page.locator('input[type="file"]').nth(0).setInputFiles(uploadFixture);
  await expect(page.locator('input[type="file"]')).toHaveCount(0);
}

async function reviewInAdmin(page: import('@playwright/test').Page, verificationName: string, approve: boolean) {
  await page.goto('/admin/verifications', { waitUntil: 'networkidle' });
  const row = page.locator('tbody tr').filter({ hasText: verificationName });
  await expect(row).toHaveCount(1);
  await row.locator('button.vz-btn--outline').click();
  if (approve) await page.locator('button.vz-btn--success').click();
  else { await page.locator('textarea.vz-textarea').fill('E2E rejection'); await page.locator('button.vz-btn--danger').click(); }
}

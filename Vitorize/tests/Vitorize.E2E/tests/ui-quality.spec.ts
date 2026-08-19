import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow } from './support/app';

test('theme selection persists across reload and preserves RTL layout', async ({ page }, testInfo) => {
  await page.goto('/', { waitUntil: 'networkidle' });
  const expectedInitial = testInfo.project.name === 'desktop-light' ? 'light' : 'dark';
  await expect(page.locator('html')).toHaveAttribute('data-theme', expectedInitial);

  const expectedNext = expectedInitial === 'dark' ? 'light' : 'dark';
  await page.waitForFunction(() => typeof (window as any).vzTheme !== 'undefined');
  await page.waitForTimeout(300);
  await page.locator('.st-theme-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', expectedNext);
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', expectedNext);
  expect(await page.evaluate(() => localStorage.getItem('vitorize-theme'))).toBe(expectedNext);
  await expectRtlAndNoOverflow(page);
});

test('responsive catalog controls remain operable at the active viewport', async ({ page }) => {
  await page.goto('/shop?q=E2E%20Dynamic', { waitUntil: 'networkidle' });
  const mobile = (page.viewportSize()?.width ?? 1280) < 768;
  if (mobile) {
    await expect(page.locator('.st-fab')).toBeVisible();
    await page.locator('.st-fab').click();
    await expect(page.locator('.st-filter-sheet__panel')).toBeVisible();
    await page.locator('.st-filter-sheet__backdrop').click({ position: { x: 10, y: 10 } });
    await expect(page.locator('.st-filter-sheet')).toBeHidden();
  } else {
    await expect(page.locator('.st-fsidebar')).toBeVisible();
    await page.locator('.st-sort__btn').click();
    await expect(page.locator('.st-sort__menu')).toBeVisible();
    await page.locator('.st-sort__opt').nth(1).click();
    await expect(page.locator('.st-sort__menu')).toBeHidden();
  }
  await expectRtlAndNoOverflow(page);
});

// Product information moved from a product-page dialog to an inline checkout section, so the
// accessible surface to audit is that section: labelled controls, and an invalid field that
// announces itself and points at its own message.
test('checkout product-input fields are labelled and announce their validation state', async ({ page }) => {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill(password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);

  await page.goto('/cart', { waitUntil: 'networkidle' });
  const clear = page.locator('button', { hasText: 'خالی کردن سبد خرید' });
  if (await clear.count()) { await clear.first().click(); await page.waitForTimeout(1200); }

  await page.goto('/product/e2e-seo-product', { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('.vz-toast.success, .vz-toast--success').first()).toBeVisible();

  await page.goto('/checkout', { waitUntil: 'networkidle' });
  const card = page.getByTestId('checkout-input-card').first();
  await expect(card).toBeVisible();

  const field = card.locator('input.st-input, textarea, select').first();
  const id = await field.getAttribute('id');
  expect(id, 'every control needs an id its label can point at').toBeTruthy();
  await expect(card.locator(`label[for="${id}"]`)).toHaveCount(1);
  await expect(field).toHaveAttribute('aria-invalid', 'false');

  // Trying to pay with it empty must mark it invalid and wire it to its own message.
  await page.locator('button.st-btn--accent').last().click();
  await expect(field).toHaveAttribute('aria-invalid', 'true');
  const describedBy = await field.getAttribute('aria-describedby');
  expect(describedBy).toBeTruthy();
  await expect(page.locator(`#${describedBy}`)).toHaveAttribute('role', 'alert');
});

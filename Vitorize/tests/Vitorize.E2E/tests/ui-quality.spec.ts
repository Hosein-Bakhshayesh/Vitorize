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
    await expect(page.locator('.st-sheet__panel')).toBeVisible();
    await page.locator('.st-sheet__bd').click({ position: { x: 10, y: 10 } });
    await expect(page.locator('.st-sheet')).toBeHidden();
  } else {
    await expect(page.locator('.st-fsidebar')).toBeVisible();
    await page.locator('.st-sort__btn').click();
    await expect(page.locator('.st-sort__menu')).toBeVisible();
    await page.locator('.st-sort__opt').nth(1).click();
    await expect(page.locator('.st-sort__menu')).toBeHidden();
  }
  await expectRtlAndNoOverflow(page);
});

test('dynamic-input dialog exposes dialog semantics, receives focus and closes with Escape', async ({ page }) => {
  await page.goto('/product/e2e-seo-product', { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toHaveAttribute('aria-modal', 'true');
  await expect(dialog).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
});

import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { loginAdmin, registerCustomer, uniqueCustomer } from './support/app';

const publicRoutes = ['/', '/shop', '/categories', '/faq', '/product/e2e-seo-product'];

for (const route of publicRoutes) {
  test(`no serious or critical axe violations on ${route}`, async ({ page }) => {
    await page.goto(route, { waitUntil: 'networkidle' });
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    const blocking = results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious');
    expect(blocking, blocking.map(v =>
      `${v.id}: ${v.help} (${v.nodes.length})\n${v.nodes.map(n =>
        `  target=${n.target.join(' > ')}; ${n.failureSummary ?? n.html}`).join('\n')}`
    ).join('\n')).toEqual([]);
  });
}

test('skip link and keyboard focus are available', async ({ page }) => {
  await page.goto('/', { waitUntil: 'networkidle' });
  await page.waitForFunction(() => typeof (window as any).vzTheme !== 'undefined');
  await page.waitForTimeout(300);
  const skip = page.locator('.st-skip-link');
  await skip.focus();
  await expect(skip).toBeFocused();
  await expect(skip).toHaveAttribute('href', '#main-content');
  await page.keyboard.press('Enter');
  await expect(page.locator('#main-content')).toBeFocused();
});

test('customer profile has no serious or critical axe violations', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Accessibility Customer'));
  await page.goto('/customer/profile', { waitUntil: 'networkidle' });
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  const blocking = results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious');
  expect(blocking, blocking.map(v => `${v.id}: ${v.help} (${v.nodes.length})`).join('\n')).toEqual([]);
});

test('admin settings has no serious or critical axe violations', async ({ page }) => {
  await loginAdmin(page);
  await page.goto('/admin/settings', { waitUntil: 'networkidle' });
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  const blocking = results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious');
  expect(blocking, blocking.map(v => `${v.id}: ${v.help} (${v.nodes.length})`).join('\n')).toEqual([]);
});

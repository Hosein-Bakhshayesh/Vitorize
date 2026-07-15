import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const publicRoutes = ['/', '/shop', '/categories', '/faq', '/product/e2e-seo-product'];

for (const route of publicRoutes) {
  test(`no serious or critical axe violations on ${route}`, async ({ page }) => {
    await page.goto(route, { waitUntil: 'networkidle' });
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    const blocking = results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious');
    expect(blocking, blocking.map(v => `${v.id}: ${v.help} (${v.nodes.length})`).join('\n')).toEqual([]);
  });
}

test('skip link and keyboard focus are available', async ({ page }) => {
  await page.goto('/');
  const skip = page.locator('.st-skip-link');
  await skip.focus();
  await expect(skip).toBeFocused();
  await expect(skip).toHaveAttribute('href', '#main-content');
  await page.keyboard.press('Enter');
  await expect(page.locator('#main-content')).toBeFocused();
});

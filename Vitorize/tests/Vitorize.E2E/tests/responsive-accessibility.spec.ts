import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '../framework/fixtures';

const axeProjects = new Set(['phone-320-light', 'phone-390-dark', 'tablet-768-light', 'tablet-820-dark']);
const tags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function expectNoBlockingAxe(page: import('@playwright/test').Page, label: string) {
  const results = await new AxeBuilder({ page }).withTags(tags).analyze();
  const blocking = results.violations.filter(violation =>
    violation.impact === 'critical' || violation.impact === 'serious');
  expect(blocking, blocking.map(violation =>
    `${label}: ${violation.id}: ${violation.help} (${violation.nodes.length})\n${violation.nodes.map(node =>
      `  target=${node.target.join(' > ')}; ${node.failureSummary ?? node.html}`).join('\n')}`
  ).join('\n')).toEqual([]);
}

test.describe('@responsive @mobile @tablet @rtl responsive accessibility', () => {
  test.beforeEach(({}, testInfo) => test.skip(!axeProjects.has(testInfo.project.name), 'Representative mobile/tablet Axe matrix only.'));

  test('public storefront and authentication retain accessibility @release @regression', async ({ page }) => {
    for (const route of ['/', '/product/e2e-seo-product', '/login']) {
      await page.goto(route, { waitUntil: 'networkidle' });
      await expectNoBlockingAxe(page, route);
    }
  });

  test('customer account retains accessibility @release @regression', async ({ page, loginAs }) => {
    await loginAs('Customer');
    for (const route of ['/customer/profile', '/customer/orders', '/customer/verification']) {
      await page.goto(route, { waitUntil: 'networkidle' });
      await expectNoBlockingAxe(page, route);
    }
  });

  test('admin settings and product editor retain accessibility @release @regression', async ({ page, loginAs }) => {
    await loginAs('SuperAdmin');
    for (const route of ['/admin/settings', '/admin/products/create']) {
      await page.goto(route, { waitUntil: 'networkidle' });
      await expect(page.locator('.vz-spinner:visible')).toHaveCount(0, { timeout: 30_000 });
      await expectNoBlockingAxe(page, route);
    }
  });
});

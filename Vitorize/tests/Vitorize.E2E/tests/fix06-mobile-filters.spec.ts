import { expect, test, type Page } from '@playwright/test';
import { expectRtlAndNoOverflow, monitorBrowser } from './support/app';

const listingRoutes = ['/shop', '/search', '/category/e2e-category', '/brand/e2e-brand'];

async function openFilterSheet(page: Page): Promise<void> {
  const trigger = page.locator('.st-fab');
  await expect(trigger).toBeVisible();
  await trigger.click();
  await expect(page.locator('.st-filter-sheet__panel')).toBeVisible();
  await page.waitForTimeout(350);
}

test.describe('FIX-06 mobile listing filters', () => {
  for (const route of listingRoutes) {
    test(`${route} keeps the mobile filter dialog reachable and stateful`, async ({ page }, testInfo) => {
      test.skip(!testInfo.project.name.includes('fix06-') || testInfo.project.name.includes('desktop'), 'mobile-only coverage');
      const browser = monitorBrowser(page);
      await page.goto(route, { waitUntil: 'networkidle' });
      await expectRtlAndNoOverflow(page);

      await page.evaluate(() => window.scrollTo(0, 180));
      const scrollBefore = await page.evaluate(() => window.scrollY);
      await openFilterSheet(page);

      const dialog = page.getByRole('dialog', { name: 'فیلترها' });
      const body = page.locator('.st-filter-sheet__body');
      const footer = page.locator('.st-filter-sheet__footer');
      const result = footer.getByRole('button', { name: /نمایش.*محصول/ });
      await expect(dialog).toHaveAttribute('aria-modal', 'true');
      await expect(page.locator('.st-filter-sheet__close')).toBeFocused();
      await expect(page.locator('html')).toHaveClass(/st-filter-scroll-locked/);

      const geometry = await page.evaluate(() => {
        const rect = (selector: string) => {
          const node = document.querySelector(selector) as HTMLElement | null;
          if (!node) return null;
          const value = node.getBoundingClientRect();
          return { top: value.top, bottom: value.bottom, left: value.left, right: value.right, height: value.height };
        };
        return {
          innerWidth: window.innerWidth,
          innerHeight: window.innerHeight,
          scrollWidth: document.documentElement.scrollWidth,
          overlay: rect('.st-filter-sheet'), panel: rect('.st-filter-sheet__panel'),
          header: rect('.st-filter-sheet__header'), body: rect('.st-filter-sheet__body'), footer: rect('.st-filter-sheet__footer'),
          result: rect('.st-filter-sheet__footer button:last-child'),
          transform: getComputedStyle(document.querySelector('.st-filter-sheet__panel')!).transform
        };
      });
      expect(geometry.overlay!.top, JSON.stringify(geometry)).toBeLessThanOrEqual(1);
      expect(geometry.overlay!.bottom, JSON.stringify(geometry)).toBeGreaterThanOrEqual(geometry.innerHeight - 1);
      expect(geometry.panel!.top, JSON.stringify(geometry)).toBeGreaterThanOrEqual(-1);
      expect(geometry.footer!.bottom, JSON.stringify(geometry)).toBeLessThanOrEqual(geometry.innerHeight + 1);
      expect(geometry.result!.top, JSON.stringify(geometry)).toBeGreaterThanOrEqual(0);
      expect(geometry.transform, JSON.stringify(geometry)).not.toMatch(/matrix\([^,]+,[^,]+,[^,]+,[^,]+,[^,]+,[1-9]/);
      expect(geometry.scrollWidth, JSON.stringify(geometry)).toBeLessThanOrEqual(geometry.innerWidth + 1);

      await page.mouse.wheel(0, 400);
      expect(await page.evaluate(() => window.scrollY)).toBe(scrollBefore);
      await body.locator('.st-fg__head').nth(2).click();
      const bodyScroll = await body.evaluate(element => {
        element.scrollTop = element.scrollHeight;
        return { top: element.scrollTop, height: element.scrollHeight, client: element.clientHeight };
      });
      expect(bodyScroll.height).toBeGreaterThan(bodyScroll.client);
      expect(bodyScroll.top).toBeGreaterThan(0);
      await expect(result).toBeVisible();
      await expect(body.locator('.st-fg').last()).toBeVisible();

      await page.locator('.st-filter-sheet__close').click();
      await expect(page.locator('.st-filter-sheet')).toBeHidden();
      await expect(page.locator('.st-fab')).toBeFocused();

      await openFilterSheet(page);
      await page.locator('.st-filter-sheet__backdrop').click({ position: { x: 8, y: 8 } });
      await expect(page.locator('.st-filter-sheet')).toBeHidden();

      await openFilterSheet(page);
      await page.locator('.st-filter-sheet__body .st-fg').nth(3).locator('.st-fopt').first().click();
      await expect.poll(() => page.locator('.st-fchip').count()).toBeGreaterThan(0);
      await result.click();
      await expect(page.locator('.st-filter-sheet')).toBeHidden();

      await openFilterSheet(page);
      await expect.poll(() => page.locator('.st-fchip').count()).toBeGreaterThan(0);
      await page.keyboard.press('Escape');
      await expect(page.locator('.st-filter-sheet')).toBeHidden();
      await expect(page.locator('.st-fab')).toBeFocused();
      await expect(page.locator('html')).not.toHaveClass(/st-filter-scroll-locked/);
      await page.mouse.wheel(0, 400);
      expect(await page.evaluate(() => window.scrollY)).toBeGreaterThanOrEqual(scrollBefore);
      await expectRtlAndNoOverflow(page);
      browser.assertClean();
    });
  }

  test('desktop keeps its sidebar and never renders the mobile filter dialog', async ({ page }, testInfo) => {
    test.skip(!testInfo.project.name.includes('desktop'), 'desktop-only coverage');
    await page.goto('/shop', { waitUntil: 'networkidle' });
    await expect(page.locator('.st-fsidebar')).toBeVisible();
    await expect(page.locator('.st-fab')).toBeHidden();
    await expect(page.locator('.st-filter-sheet')).toHaveCount(0);
    await expectRtlAndNoOverflow(page);
  });
});

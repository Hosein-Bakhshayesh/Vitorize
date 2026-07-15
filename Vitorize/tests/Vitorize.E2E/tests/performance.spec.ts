import { expect, test } from '@playwright/test';

test('public product has a bounded initial document and stable rendered layout', async ({ page, request }) => {
  const raw = await request.get('/product/e2e-seo-product');
  const bytes = Buffer.byteLength(await raw.body());
  expect(bytes).toBeLessThan(350_000);

  await page.goto('/product/e2e-seo-product', { waitUntil: 'networkidle' });
  const timing = await page.evaluate(() => {
    const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
    return {
      ttfb: navigation.responseStart - navigation.requestStart,
      domContentLoaded: navigation.domContentLoadedEventEnd - navigation.startTime,
      transferSize: navigation.transferSize
    };
  });
  expect(timing.ttfb).toBeLessThan(2_000);
  expect(timing.domContentLoaded).toBeLessThan(8_000);

  const overflowing = await page.evaluate(() => {
    const viewport = document.documentElement.clientWidth;
    if (document.documentElement.scrollWidth <= viewport + 1) return [];
    return [...document.querySelectorAll<HTMLElement>('body *')]
      .map(element => ({
        tag: element.tagName.toLowerCase(),
        className: element.className?.toString().slice(0, 120) ?? '',
        left: Math.round(element.getBoundingClientRect().left),
        right: Math.round(element.getBoundingClientRect().right),
        width: Math.round(element.getBoundingClientRect().width)
      }))
      .filter(item => item.right > viewport + 1 || item.left < -1)
      .slice(0, 20);
  });
  expect(overflowing, JSON.stringify(overflowing, null, 2)).toEqual([]);
});

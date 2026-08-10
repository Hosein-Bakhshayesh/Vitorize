import { expect, test, type Page } from '@playwright/test';

const logoSelector = '.st-card .st-logo__img';

async function installFrameProbe(page: Page): Promise<void> {
  await page.addInitScript(() => {
    (window as any).__fix07Frames = [];
    const until = performance.now() + 2_000;
    const probe = () => {
      const logo = document.querySelector('.st-card .st-logo__img') as HTMLImageElement | null;
      if (logo) {
        const rect = logo.getBoundingClientRect();
        (window as any).__fix07Frames.push({ time: performance.now(), width: rect.width, height: rect.height, readyState: document.readyState });
      }
      if (performance.now() < until) requestAnimationFrame(probe);
    };
    requestAnimationFrame(probe);
  });
}

async function logoSize(page: Page): Promise<{ width: number; height: number }> {
  return page.locator(logoSelector).evaluate((logo: HTMLImageElement) => {
    const rect = logo.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
  });
}

test('FIX-07 reserves StoreLogo dimensions through the real failed-login redirect', async ({ page }) => {
  await installFrameProbe(page);
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await expect(page.locator(logoSelector)).toHaveAttribute('width', '42');
  await expect(page.locator(logoSelector)).toHaveAttribute('height', '42');
  await expect(page.locator('.st-header .st-logo__img')).toHaveAttribute('width', '42');
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill('wrong-password');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();
  await expect(page).toHaveURL(/\/login\?error=/);
  await expect(page.locator('.st-alert--danger')).toBeVisible();
  const stable = await logoSize(page);
  expect(stable.width).toBeLessThanOrEqual(43);
  expect(stable.height).toBeLessThanOrEqual(43);
  const frames = await page.evaluate(() => (window as any).__fix07Frames as Array<{ width: number; height: number }>);
  expect(Math.max(...frames.map(frame => frame.width), stable.width)).toBeLessThanOrEqual(50);
  expect(Math.max(...frames.map(frame => frame.height), stable.height)).toBeLessThanOrEqual(50);
});

test('FIX-07 keeps Login logo intrinsically sized while storefront CSS is blocked', async ({ page }) => {
  await page.route(/\/css\/storefront\.css(?:\?|$)/, route => route.abort());
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await expect(page.locator(logoSelector)).toHaveAttribute('width', '42');
  await expect(page.locator(logoSelector)).toHaveAttribute('height', '42');
  const size = await logoSize(page);
  expect(size.width).toBeLessThanOrEqual(43);
  expect(size.height).toBeLessThanOrEqual(43);
});

test('FIX-07 delayed stylesheet never permits an intrinsic-size logo frame', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'fix07-desktop-light', 'single controlled timing diagnostic');
  for (const delay of [500, 1000, 1500]) {
    await installFrameProbe(page);
    await page.route(/\/css\/storefront\.css(?:\?|$)/, async route => {
      await new Promise(resolve => setTimeout(resolve, delay));
      await route.continue();
    });
    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    const frames = await page.evaluate(() => (window as any).__fix07Frames as Array<{ width: number; height: number }>);
    const stable = await logoSize(page);
    expect(Math.max(...frames.map(frame => frame.width), stable.width), `${delay}ms`).toBeLessThanOrEqual(50);
    expect(Math.max(...frames.map(frame => frame.height), stable.height), `${delay}ms`).toBeLessThanOrEqual(50);
    await page.unroute(/\/css\/storefront\.css(?:\?|$)/);
  }
});

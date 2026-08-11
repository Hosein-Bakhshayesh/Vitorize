import { expect, test } from '../framework/fixtures';

const unbrokenToken = 'پیامآزمایشیبدونفاصلهبرایبررسیمقاومتنمایشمتن'.repeat(24);
const longPersianProse = 'این یک متن فارسی آزمایشی برای بررسی شکست خطوط و خوانایی صفحه در زمان نگهداری سامانه است. '.repeat(18);

async function geometry(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const pageBox = document.querySelector<HTMLElement>('.st-errpage')!;
    const message = document.querySelector<HTMLElement>('.st-errpage__msg')!;
    const action = document.querySelector<HTMLElement>('.st-errpage__actions button')!;
    const style = getComputedStyle(pageBox);
    const pageRect = pageBox.getBoundingClientRect();
    const messageRect = message.getBoundingClientRect();
    const actionRect = action.getBoundingClientRect();
    const availableWidth = pageRect.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    return {
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth,
      availableWidth,
      messageWidth: messageRect.width,
      messageScrollWidth: message.scrollWidth,
      messageBottom: messageRect.bottom,
      actionTop: actionRect.top,
      hasMain: document.querySelectorAll('main').length === 1,
      h1Count: document.querySelectorAll('h1').length,
      mascotRole: document.querySelector('.st-mascot')?.getAttribute('role'),
      mascotLabel: document.querySelector('.st-mascot')?.getAttribute('aria-label')
    };
  });
}

async function expectConstrained(page: import('@playwright/test').Page) {
  const result = await geometry(page);
  expect(result.documentWidth).toBeLessThanOrEqual(result.viewportWidth);
  expect(result.messageWidth).toBeLessThanOrEqual(result.availableWidth + 1);
  expect(result.messageScrollWidth).toBeLessThanOrEqual(result.messageWidth + 1);
  expect(result.messageBottom).toBeLessThanOrEqual(result.actionTop);
  expect(result.hasMain).toBe(true);
  expect(result.h1Count).toBe(1);
  expect(result.mascotRole).toBe('img');
  expect(result.mascotLabel).toBeTruthy();
}

test('FIX-08 direct 503 keeps HTTP semantics and constrains configured-message shapes', async ({ page, consoleGuard }, testInfo) => {
  const response = await page.goto('/error/503', { waitUntil: 'networkidle' });
  expect(response?.status()).toBe(503);
  await expect(page.locator('.st-errpage')).toBeVisible();
  await expect(page.locator('.st-errpage__code')).toContainText('۵۰۳');
  await expect(page.locator('html')).toHaveAttribute('data-theme', testInfo.project.use.colorScheme as string);
  await expectConstrained(page);

  await page.locator('.st-errpage__msg').evaluate((element, text) => { element.textContent = text; }, longPersianProse);
  await expectConstrained(page);

  await page.locator('.st-errpage__msg').evaluate((element, text) => { element.textContent = text; }, unbrokenToken);
  await expectConstrained(page);
  await page.locator('.st-errpage__actions button').scrollIntoViewIfNeeded();
  await expect(page.locator('.st-errpage__actions button')).toBeVisible();
  // Chromium reports the intentionally navigated top-level 503 as a console
  // resource failure. Keep every other browser/circuit failure actionable.
  expect(consoleGuard.errors.filter(error => !error.includes('status of 503 (Service Unavailable)'))).toEqual([]);
});

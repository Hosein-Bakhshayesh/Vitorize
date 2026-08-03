import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow } from './support/app';

test('storefront loads the configured Persian and Latin font families @smoke', async ({ page }, testInfo) => {
  await page.goto('/', { waitUntil: 'networkidle' });

  const typography = await page.evaluate(async () => {
    const shell = document.querySelector('.st-shell');
    if (!shell) throw new Error('Storefront shell was not rendered.');

    await document.fonts.load('400 16px Peyda', 'نمونه');
    await document.fonts.load('400 16px "Funnel Display"', 'Steam Wallet');

    const styles = getComputedStyle(shell);
    const loadedFamilies = Array.from(document.fonts).map(font => font.family.replaceAll('"', ''));
    return {
      persian: styles.getPropertyValue('--font-fa').trim(),
      english: styles.getPropertyValue('--font-en').trim(),
      family: styles.fontFamily,
      loadedFamilies
    };
  });

  expect(typography.persian).toContain('Peyda');
  expect(typography.english).toContain('Funnel Display');
  expect(typography.english).toContain('Manrope');
  expect(typography.family).toContain('Peyda');
  expect(typography.loadedFamilies).toContain('Peyda');
  expect(typography.loadedFamilies).toContain('Funnel Display');
  await expectRtlAndNoOverflow(page);
  await page.screenshot({ path: testInfo.outputPath('storefront-typography-after.png'), fullPage: true });
});

import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow } from './support/app';

test('storefront loads the configured Persian and Latin font families @smoke', async ({ page }, testInfo) => {
  await page.goto('/', { waitUntil: 'networkidle' });

  const typography = await page.evaluate(async () => {
    const shell = document.querySelector('.st-shell');
    if (!shell) throw new Error('Storefront shell was not rendered.');

    const styles = getComputedStyle(shell);
    // Whatever family the deployment has configured is the one that must actually load. Asserting
    // the configured value rather than a hard-coded name keeps this test meaningful when an
    // administrator changes the storefront font in Admin - Settings.
    const configuredPersian = styles.getPropertyValue('--font-fa').trim().replaceAll(/["']/g, '');
    await document.fonts.load(`400 16px "${configuredPersian}"`, 'نمونه');
    await document.fonts.load('400 16px "Funnel Display"', 'Steam Wallet');

    const loadedFamilies = Array.from(document.fonts).map(font => font.family.replaceAll('"', ''));
    return {
      persian: configuredPersian,
      english: styles.getPropertyValue('--font-en').trim(),
      family: styles.fontFamily,
      loadedFamilies
    };
  });

  // Vazirmatn is the application default (client batch 2). Peyda remains selectable, so the test
  // pins the mechanism - the configured face resolves and loads - not one particular family name.
  expect(typography.persian).toBe('Vazirmatn');
  expect(typography.english).toContain('Funnel Display');
  expect(typography.english).toContain('Manrope');
  expect(typography.family).toContain(typography.persian);
  expect(typography.loadedFamilies).toContain(typography.persian);
  expect(typography.loadedFamilies).toContain('Funnel Display');
  // Nothing may fall through to a browser/system default.
  expect(typography.family).not.toMatch(/^\s*(Arial|Tahoma|Segoe UI|system-ui|Times)/i);
  await expectRtlAndNoOverflow(page);
  await page.screenshot({ path: testInfo.outputPath('storefront-typography-after.png'), fullPage: true });
});

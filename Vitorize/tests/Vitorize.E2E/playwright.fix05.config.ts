import { defineConfig, devices } from '@playwright/test';
import baseConfig from './playwright.config';

const desktop = devices['Desktop Chrome'];
const matrix = [
  ['fix05-desktop-light', 1440, 900, 'light', false],
  ['fix05-desktop-dark', 1440, 900, 'dark', false],
  ['fix05-iphone-light', 390, 844, 'light', true],
  ['fix05-iphone-dark', 390, 844, 'dark', true],
  ['fix05-android-light', 393, 852, 'light', true],
  ['fix05-android-dark', 393, 852, 'dark', true]
] as const;

export default defineConfig({
  ...baseConfig,
  testMatch: /fix05-visual-fixtures\.spec\.ts/,
  timeout: 90_000,
  globalTimeout: 1_800_000,
  workers: 1,
  projects: matrix.map(([name, width, height, colorScheme, mobile]) => ({
    name,
    use: { ...desktop, channel: 'chrome', viewport: { width, height }, screen: { width, height }, colorScheme, isMobile: mobile, hasTouch: mobile }
  }))
});

import { defineConfig, devices } from '@playwright/test';
import baseConfig from './playwright.config';

const desktop = devices['Desktop Chrome'];
const matrix = [
  ['fix06-360-light', 360, 800, 'light', true],
  ['fix06-360-dark', 360, 800, 'dark', true],
  ['fix06-390-light', 390, 844, 'light', true],
  ['fix06-390-dark', 390, 844, 'dark', true],
  ['fix06-393-light', 393, 852, 'light', true],
  ['fix06-393-dark', 393, 852, 'dark', true],
  ['fix06-desktop-light', 1440, 900, 'light', false],
  ['fix06-desktop-dark', 1440, 900, 'dark', false]
] as const;

export default defineConfig({
  ...baseConfig,
  testMatch: /fix06-mobile-filters\.spec\.ts/,
  timeout: 90_000,
  globalTimeout: 1_800_000,
  workers: 1,
  projects: matrix.map(([name, width, height, colorScheme, mobile]) => ({
    name,
    use: { ...desktop, channel: 'chrome', viewport: { width, height }, screen: { width, height }, colorScheme, isMobile: mobile, hasTouch: mobile }
  }))
});

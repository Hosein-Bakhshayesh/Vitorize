import { defineConfig, devices } from '@playwright/test';
import baseConfig from './playwright.config';

const desktop = devices['Desktop Chrome'];
const matrix = [
  ['phone-320-light', 320, 568, 'light', 1, true],
  ['phone-360-dark-hidpi', 360, 640, 'dark', 3, true],
  ['phone-375-light', 375, 667, 'light', 2, true],
  ['phone-390-dark', 390, 844, 'dark', 3, true],
  ['phone-412-light', 412, 915, 'light', 2.625, true],
  ['phone-430-dark', 430, 932, 'dark', 3, true],
  ['phone-landscape-667-light', 667, 375, 'light', 2, true],
  ['tablet-768-light', 768, 1024, 'light', 1, true],
  ['tablet-820-dark', 820, 1180, 'dark', 2, true],
  ['tablet-landscape-1024-light', 1024, 768, 'light', 1, true],
  ['laptop-1280-dark', 1280, 800, 'dark', 1, false],
  ['desktop-1366-light', 1366, 768, 'light', 1, false],
  ['desktop-1440-light', 1440, 900, 'light', 1, false],
  ['desktop-1440-dark', 1440, 900, 'dark', 1, false],
  ['desktop-1920-light', 1920, 1080, 'light', 1, false]
] as const;

export default defineConfig({
  ...baseConfig,
  testMatch: /responsive-.*\.spec\.ts/,
  timeout: 240_000,
  globalTimeout: 3_600_000,
  retries: 0,
  workers: 1,
  projects: matrix.map(([name, width, height, colorScheme, deviceScaleFactor, mobile]) => ({
    name,
    use: {
      ...desktop,
      channel: 'chrome',
      viewport: { width, height },
      screen: { width, height },
      colorScheme,
      deviceScaleFactor,
      isMobile: mobile,
      hasTouch: mobile
    }
  }))
});

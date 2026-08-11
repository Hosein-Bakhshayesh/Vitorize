import { defineConfig, devices } from '@playwright/test';
import baseConfig from './playwright.config';

const desktop = devices['Desktop Chrome'];
const matrix = [
  ['fix08-1440-light', 1440, 900, 'light'],
  ['fix08-1440-dark', 1440, 900, 'dark'],
  ['fix08-1366-light', 1366, 768, 'light'],
  ['fix08-1366-dark', 1366, 768, 'dark'],
  ['fix08-390-light', 390, 844, 'light'],
  ['fix08-390-dark', 390, 844, 'dark'],
  ['fix08-393-light', 393, 852, 'light'],
  ['fix08-393-dark', 393, 852, 'dark'],
  ['fix08-360-light', 360, 800, 'light'],
  ['fix08-360-dark', 360, 800, 'dark']
] as const;

export default defineConfig({
  ...baseConfig,
  testMatch: /fix08-maintenance\.spec\.ts/,
  timeout: 90_000,
  globalTimeout: 1_800_000,
  workers: 1,
  projects: matrix.map(([name, width, height, colorScheme]) => ({
    name,
    use: { ...desktop, channel: 'chrome', viewport: { width, height }, screen: { width, height }, colorScheme }
  }))
});

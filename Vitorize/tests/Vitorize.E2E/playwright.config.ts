import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5077';
const manageStack = process.env.E2E_MANAGE_STACK === 'true';

export default defineConfig({
  testDir: './tests',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'artifacts/report' }]],
  outputDir: 'artifacts/results',
  use: {
    baseURL,
    locale: 'fa-IR',
    timezoneId: 'Asia/Tehran',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: process.env.E2E_VIDEO === 'true' ? 'retain-on-failure' : 'off',
    ignoreHTTPSErrors: true
  },
  projects: [
    { name: 'desktop-light', use: { ...devices['Desktop Chrome'], channel: 'chrome', colorScheme: 'light' } },
    { name: 'desktop-dark', use: { ...devices['Desktop Chrome'], channel: 'chrome', colorScheme: 'dark' } },
    { name: 'mobile-dark', use: { ...devices['Pixel 7'], channel: 'chrome', colorScheme: 'dark' } }
  ],
  webServer: manageStack ? {
    command: 'powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Start-E2EStack.ps1',
    url: baseURL,
    timeout: 120_000,
    reuseExistingServer: !process.env.CI
  } : undefined
});

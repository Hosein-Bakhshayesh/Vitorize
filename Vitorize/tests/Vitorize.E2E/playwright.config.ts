import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5077';
const manageStack = process.env.E2E_MANAGE_STACK === 'true';
if (manageStack) {
  process.env.E2E_ADMIN_MOBILE ??= '09120000011';
  // Testing-only credential for the isolated browser database. Production never
  // reads this Playwright configuration or enables the bootstrap flag.
  process.env.E2E_ADMIN_PASSWORD ??= 'E2E-Admin-Only-aA1!';
}

export default defineConfig({
  testDir: './tests',
  // Full-stack Blazor Server admin pages (interactive circuit + API data) are heavy; 60s gives
  // headroom on slower CI machines without masking real hangs (steps still fail fast on assertions).
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 1,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'artifacts/report' }],
    ['junit', { outputFile: 'artifacts/results/junit.xml' }],
    ['json', { outputFile: 'artifacts/results/results.json' }]
  ],
  outputDir: 'artifacts/results',
  use: {
    baseURL,
    locale: 'fa-IR',
    timezoneId: 'Asia/Tehran',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    // Videos are captured for every failure by default; set E2E_VIDEO=off to disable on slow machines.
    video: process.env.E2E_VIDEO === 'off' ? 'off' : 'retain-on-failure',
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

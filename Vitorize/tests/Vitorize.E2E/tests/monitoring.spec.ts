import { expect, test } from '@playwright/test';

const adminMobile = process.env.E2E_ADMIN_MOBILE!;
const adminPassword = process.env.E2E_ADMIN_PASSWORD!;

test('monitoring route enforces admin authentication', async ({ page }) => {
  await page.goto('/admin/monitoring');
  await expect(page).toHaveURL(/\/admin\/login/i);
});

test('authorized diagnostics admin sees safe responsive monitoring summary', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });

  await page.goto('/admin/login');
  await page.locator('input[name="mobile"]').fill(adminMobile);
  await page.locator('input[name="password"]').fill(adminPassword);
  await Promise.all([
    page.waitForURL(/\/admin\/(dashboard|monitoring)/i),
    page.locator('button[type="submit"]').click()
  ]);

  await page.goto('/admin/monitoring', { waitUntil: 'networkidle' });
  await expect(page).toHaveURL(/\/admin\/monitoring/i);
  await expect(page.locator('.vz-stats .vz-stat')).toHaveCount(7);
  await expect(page.locator('.monitoring-grid')).toBeVisible();

  const seqLink = page.getByRole('link', { name: /Seq/i });
  await expect(seqLink).toBeVisible();
  await expect(seqLink).toHaveAttribute('href', 'https://seq.e2e.invalid');
  await expect(seqLink).toHaveAttribute('rel', /noopener/);
  await expect(seqLink).toHaveAttribute('rel', /noreferrer/);

  const monitoringLinks = page.locator('.monitoring-actions');
  await expect(monitoringLinks.getByRole('link', { name: /لاگ خطاها/ })).toBeVisible();
  await expect(monitoringLinks.getByRole('link', { name: /لاگ امنیتی/ })).toBeVisible();
  await expect(monitoringLinks.getByRole('link', { name: /لاگ عملیات/ })).toBeVisible();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  expect(consoleErrors).toEqual([]);
});

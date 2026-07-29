import { test, expect, TAG } from '../framework/fixtures';

async function exportFirstSelectedRow(page: import('@playwright/test').Page, route: string, filePrefix: string): Promise<string> {
  await page.goto(route, { waitUntil: 'networkidle' });
  const rowSelector = 'tbody tr .vz-check';
  await expect(page.locator(rowSelector).first()).toBeVisible();
  await page.locator(rowSelector).first().click();
  const exportButton = page.locator('.vz-bulkbar button.vz-btn--outline');
  await expect(exportButton).toBeVisible();
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    exportButton.click()
  ]);
  expect(download.suggestedFilename()).toMatch(new RegExp(`^${filePrefix}-`));
  const stream = await download.createReadStream();
  expect(stream).not.toBeNull();
  let csv = '';
  for await (const chunk of stream!) csv += chunk.toString();
  return csv;
}

test('selected-row exports are server-approved and only expose approved CSV columns', {
  tag: [TAG.admin, TAG.release, TAG.regression]
}, async ({ page, loginAs }) => {
  await loginAs('SuperAdmin');

  const productsCsv = await exportFirstSelectedRow(page, '/admin/products', 'products');
  expect(productsCsv).toContain('Title');
  expect(productsCsv).not.toContain('FullDescription');

  const ordersCsv = await exportFirstSelectedRow(page, '/admin/orders', 'orders');
  expect(ordersCsv).toContain('OrderNumber');
  expect(ordersCsv).not.toContain('Items');
});

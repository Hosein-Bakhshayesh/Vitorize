import { expect, test } from '@playwright/test';
import { expectRtlAndNoOverflow, loginCustomer, logoutCustomer, registerCustomer, uniqueCustomer } from './support/app';

test('customer can update profile, change password and authenticate with the replacement password', async ({ page }) => {
  const customer = await registerCustomer(page, uniqueCustomer('Profile Customer'));
  await page.goto('/customer/profile', { waitUntil: 'networkidle' });
  const profileInputs = page.locator('.st-card').first().locator('input.st-input');
  await profileInputs.nth(0).fill('Updated Browser Customer');
  await profileInputs.nth(1).fill(`updated-${Date.now()}@example.test`);
  await page.locator('.st-card').first().getByRole('button').filter({ hasText: /ذخیره/ }).click();
  await expect(page.locator('.vz-toast.success')).toBeVisible();
  await expect(page.locator('.vz-toast.success')).toBeHidden();

  const passwordCard = page.locator('.st-card').nth(1);
  const passwords = passwordCard.locator('input[type="password"]');
  const replacement = `Replacement-${Date.now()}-aA1!`;
  await passwords.nth(0).fill(customer.password);
  await passwords.nth(1).fill(replacement);
  await passwords.nth(2).fill(replacement);
  await passwordCard.getByRole('button').click();
  await expect(passwords.nth(0)).toHaveValue('');
  await expect(passwords.nth(1)).toHaveValue('');
  await expect(passwords.nth(2)).toHaveValue('');

  customer.password = replacement;
  await logoutCustomer(page);
  await loginCustomer(page, customer);
  await expect(page.locator('main')).toContainText('Updated Browser Customer');
});

test('customer account navigation is authorized, responsive and exposes safe empty states', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Account Navigation'));
  const routes = [
    '/customer/dashboard', '/customer/orders', '/customer/wallet', '/customer/tickets',
    '/customer/notifications', '/customer/gift-codes', '/customer/reviews',
    '/customer/wishlist', '/customer/verification'
  ];

  for (const route of routes) {
    await page.goto(route, { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(new RegExp(route.replaceAll('/', '\\/')));
    await expect(page.locator('main')).toBeVisible();
    await expectRtlAndNoOverflow(page);
  }

  await page.goto('/customer/verification');
  await page.locator('button.st-btn--primary').click();
  await expect(page.locator('.vz-toast.error')).toBeVisible();
});

test('customer can create a ticket and view the resulting conversation', async ({ page }) => {
  await registerCustomer(page, uniqueCustomer('Ticket Customer'));
  await page.goto('/customer/tickets/new');
  const subject = `E2E ticket ${Date.now()}`;
  await page.locator('input.st-input').fill(subject);
  await page.locator('select.st-select').first().selectOption('5');
  await page.locator('select.st-select').nth(1).selectOption('3');
  await page.locator('textarea.st-textarea').fill('A deterministic browser support request.');
  await page.locator('button.st-btn--primary').click();
  await expect(page).toHaveURL(/\/customer\/tickets\/[0-9a-f-]+/i);
  await expect(page.locator('main')).toContainText(subject);
  await expect(page.locator('main')).toContainText('A deterministic browser support request.');
});

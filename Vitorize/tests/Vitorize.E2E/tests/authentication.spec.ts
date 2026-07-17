import { expect, test } from '@playwright/test';
import {
  expireOtp,
  latestOtp,
  loginCustomer,
  logoutCustomer,
  monitorBrowser,
  registerCustomer,
  uniqueCustomer
} from './support/app';

test('customer can register, persist a session, log out and log back in with password', async ({ page }) => {
  const browser = monitorBrowser(page);
  const customer = await registerCustomer(page);

  await expect(page).toHaveURL(/\/customer\/dashboard/);
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page).toHaveURL(/\/customer\/dashboard/);
  await expect(page.locator('main')).toContainText(customer.fullName);

  await logoutCustomer(page);
  await page.goto('/customer/profile');
  await expect(page).toHaveURL(/\/login\?returnUrl=/i);

  await loginCustomer(page, customer, '/customer/profile');
  await expect(page).toHaveURL(/\/customer\/profile/);
  browser.assertClean();
});

test('invalid password is rejected without creating an authenticated session', async ({ page }) => {
  const customer = uniqueCustomer('Invalid Login');
  await registerCustomer(page, customer);
  await logoutCustomer(page);

  await page.goto('/login');
  await page.locator('#pw-mobile').fill(customer.mobile);
  await page.locator('#pw-pass').fill('Definitely-Wrong-aA1!');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();

  await expect(page).toHaveURL(/\/login\?error=/);
  await expect(page.locator('.st-alert--danger')).toBeVisible();
  await page.goto('/customer/dashboard');
  await expect(page).toHaveURL(/\/login\?returnUrl=/i);
});

test('customer can request, retry and complete OTP login through the fake SMS provider', async ({ page, request }) => {
  const customer = await registerCustomer(page, uniqueCustomer('OTP Login'));
  await logoutCustomer(page);
  await page.goto('/login?otp=1');

  await page.locator('#otp-mobile').fill(customer.mobile);
  await page.locator('#otp-mobile').locator('xpath=following::button[1]').click();
  await expect(page.locator('#otp-code')).toBeVisible();
  const firstCode = await latestOtp(request, customer.mobile);

  const resend = page.locator('button').filter({ hasText: /ارسال مجدد/ });
  await expect(resend).toBeVisible();
  await resend.click();
  const retriedCode = await latestOtp(request, customer.mobile, firstCode);

  await page.locator('#otp-code').fill(retriedCode);
  await page.locator('main button.st-btn--primary.st-btn--block').click();
  await expect(page).toHaveURL(/\/customer\/dashboard/);
});

test('expired OTP is rejected and forgot-password reset accepts the newly issued OTP', async ({ page, request }) => {
  const customer = await registerCustomer(page, uniqueCustomer('Password Reset'));
  await logoutCustomer(page);

  await page.goto('/login?otp=1');
  await page.locator('#otp-mobile').fill(customer.mobile);
  await page.locator('#otp-mobile').locator('xpath=following::button[1]').click();
  const expiredCode = await latestOtp(request, customer.mobile);
  await expireOtp(request, customer.mobile);
  await page.locator('#otp-code').fill(expiredCode);
  await page.locator('main button.st-btn--primary.st-btn--block').click();
  await expect(page.locator('.st-alert--danger')).toBeVisible();
  await expect(page).toHaveURL(/\/login/);

  await page.goto('/forgot-password');
  await page.locator('input[inputmode="tel"]').fill(customer.mobile);
  await page.locator('button.st-btn--primary').click();
  await expect(page).toHaveURL(/\/reset-password\?mobile=/);
  const resetCode = await latestOtp(request, customer.mobile);
  const fields = page.locator('input.st-input');
  await fields.nth(1).fill(resetCode);
  const newPassword = `Changed-${Date.now()}-aA1!`;
  await fields.nth(2).fill(newPassword);
  await fields.nth(3).fill(newPassword);
  await page.locator('button.st-btn--primary').click();
  await expect(page).toHaveURL(/\/login$/);

  customer.password = newPassword;
  await loginCustomer(page, customer);
  await expect(page).toHaveURL(/\/customer\/dashboard/);
});

import { expect, test } from '../framework/fixtures';
import { apiBaseUrl } from './support/app';

type ApiResult<T> = { data: T };
type Notification = { id: string; title: string; message: string; type: number; isRead: boolean; actionUrl: string | null };
type Broadcast = { id: string; title: string; recipientCount: number };

const ANNOUNCEMENT_TYPE = 91;
const customerA = '09120000013';
const customerB = '09120000014';
const staffAdmin = '09120000012';

test.describe('FIX-15 broadcast announcements @fix15', () => {
  test.describe.configure({ timeout: 240_000 });

  test('SuperAdmin broadcasts to all customers; both customers receive it once and staff does not', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin journey runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const title = unique('اطلاعیه همگانی');
    await loginAs('SuperAdmin');

    await page.goto('/admin/notifications', { waitUntil: 'networkidle' });
    await page.getByTestId('broadcast-open').click();

    // AllCustomers is the default audience; the preview count must appear before sending.
    await expect(page.getByTestId('broadcast-audience-all')).toBeChecked();
    await page.getByTestId('broadcast-title').fill(title);
    await page.getByTestId('broadcast-message').fill('متن اطلاعیه همگانی برای آزمون.');
    await page.getByTestId('broadcast-action-url').fill('/shop');
    await page.getByTestId('broadcast-preview-refresh').click();

    const preview = page.getByTestId('broadcast-preview');
    await expect(preview).toBeVisible();
    await expect(preview).toContainText('تعداد گیرندگان');
    const previewCount = digitsFrom(await preview.innerText());
    expect(previewCount).toBeGreaterThan(0);

    // A high-impact action must go through the confirmation dialog, not a single click.
    const send = page.getByTestId('broadcast-send');
    await expect(send).toBeEnabled();
    await send.click();
    const dialog = page.getByRole('dialog').filter({ hasText: 'ارسال گروهی اعلان' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('ادامه می‌دهید؟');
    await dialog.getByRole('button', { name: 'ارسال' }).click();

    // The success toast reports the server's actual delivered count.
    const toast = page.locator('.vz-toast').last();
    await expect(toast).toContainText('کاربر ارسال شد');
    expect(digitsFrom(await toast.innerText())).toBe(previewCount);

    // History records the send truthfully.
    const historyRow = page.getByTestId('broadcast-history').locator('tr').filter({ hasText: title });
    await expect(historyRow).toHaveCount(1);
    await expect(historyRow).toContainText('همه مشتریان');
    await expect(historyRow).toContainText('ارسال شد');

    // Each customer received it exactly once; the staff account did not.
    for (const mobile of [customerA, customerB]) {
      const received = (await notificationsFor(request, mobile)).filter(x => x.title === title);
      expect(received, `${mobile} must receive the announcement exactly once`).toHaveLength(1);
      expect(received[0].type).toBe(ANNOUNCEMENT_TYPE);
      expect(received[0].isRead).toBe(false);
      expect(received[0].actionUrl).toBe('/shop');
    }
    expect((await notificationsFor(request, staffAdmin)).filter(x => x.title === title)).toHaveLength(0);

    consoleGuard.assertClean();
  });

  test('a customer sees the announcement as plain text with a working internal CTA, and can mark it read', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Customer journey runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const title = unique('اطلاعیه مشتری');
    // Deliberately include markup in the body: it must be displayed, never interpreted.
    await sendBroadcast(request, {
      audience: 1, title, message: 'سلام <b>کاربر</b> عزیز', actionUrl: '/shop'
    });

    await loginCustomer(page, customerA);
    await page.goto('/customer/notifications', { waitUntil: 'networkidle' });

    const card = page.getByTestId('customer-announcement').filter({ hasText: title });
    await expect(card).toHaveCount(1);
    await expect(card).toContainText('سلام <b>کاربر</b> عزیز');
    await expect(card.locator('b')).toHaveCount(0, 'notification text is never rendered as HTML');

    const cta = card.getByTestId('notification-action');
    await expect(cta).toBeVisible();
    await expect(cta).toHaveAttribute('href', '/shop');
    await expect(cta).not.toHaveAttribute('target', '_blank');

    // Unread until the customer acts on it.
    const unreadBefore = await unreadCount(request, customerA);
    await card.getByRole('button').last().click();
    await expect.poll(() => unreadCount(request, customerA)).toBe(unreadBefore - 1);

    await page.goto('/customer/notifications', { waitUntil: 'networkidle' });
    await page.getByRole('button', { name: 'خواندن همه' }).click();
    await expect.poll(() => unreadCount(request, customerA)).toBe(0);

    // The CTA navigates internally.
    await page.goto('/customer/notifications', { waitUntil: 'networkidle' });
    await page.getByTestId('customer-announcement').filter({ hasText: title })
      .getByTestId('notification-action').click();
    await expect(page).toHaveURL(/\/shop$/);

    consoleGuard.assertClean();
  });

  test('a selected-customers broadcast reaches only the chosen customer', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin journey runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const title = unique('اطلاعیه انتخابی');
    const targetName = await fullNameOf(request, customerA);
    await loginAs('SuperAdmin');

    await page.goto('/admin/notifications', { waitUntil: 'networkidle' });
    await page.getByTestId('broadcast-open').click();

    // Fill the text fields before the audience controls: picking a recipient triggers an async
    // preview whose re-render would otherwise race the pending @bind change events.
    await page.getByTestId('broadcast-title').fill(title);
    await page.getByTestId('broadcast-message').fill('متن اطلاعیه انتخابی.');
    await page.getByTestId('broadcast-audience-selected').check();

    // The picker searches server-side rather than loading every user.
    await page.getByTestId('broadcast-user-search').fill(customerA);
    await expect(page.getByTestId('broadcast-search-results')).toBeVisible();
    await page.getByTestId('broadcast-search-results').getByRole('button').first().click();
    await expect(page.getByTestId('broadcast-selected-chips')).toContainText(targetName);
    await expect(page.getByTestId('broadcast-preview')).toContainText('۱');

    const send = page.getByTestId('broadcast-send');
    await expect(send).toBeEnabled();
    await send.click();
    await page.getByRole('dialog').filter({ hasText: 'ارسال گروهی اعلان' })
      .getByRole('button', { name: 'ارسال' }).click();
    await expect(page.locator('.vz-toast').last()).toContainText('کاربر ارسال شد');

    expect((await notificationsFor(request, customerA)).filter(x => x.title === title)).toHaveLength(1);
    expect((await notificationsFor(request, customerB)).filter(x => x.title === title)).toHaveLength(0);

    consoleGuard.assertClean();
  });

  test('mobile 390x844 keeps the announcement readable with a usable CTA', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile smoke is sufficient.');
    await page.setViewportSize({ width: 390, height: 844 });
    const title = unique('اطلاعیه موبایل');
    await sendBroadcast(request, {
      audience: 1, title, message: 'متن اطلاعیه برای بررسی چیدمان موبایل در صفحه اعلان‌ها.', actionUrl: '/shop'
    });

    await loginCustomer(page, customerA);
    await page.goto('/customer/notifications', { waitUntil: 'networkidle' });

    const card = page.getByTestId('customer-announcement').filter({ hasText: title });
    await expect(card).toHaveCount(1);
    await expect(card.getByTestId('notification-action')).toBeVisible();
    await expectNoOverflow(page);

    consoleGuard.assertClean();
  });
});

function unique(prefix: string) {
  return `${prefix} ${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`;
}

/** Reads a Persian-digit number out of UI text. */
function digitsFrom(text: string): number {
  const latin = text.replace(/[۰-۹]/g, d => String(d.charCodeAt(0) - 0x06f0)).replace(/[^\d]/g, ' ');
  const first = latin.trim().split(/\s+/).filter(Boolean)[0];
  return first ? Number(first) : 0;
}

async function sendBroadcast(
  request: import('@playwright/test').APIRequestContext,
  data: { audience: number; title: string; message: string; actionUrl?: string; selectedCustomerIds?: string[] }
): Promise<Broadcast> {
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const response = await request.post(`${apiBaseUrl}/admin/notification-broadcasts`, {
    headers: { ...bearer(admin), 'Idempotency-Key': `fix15-${Date.now()}-${Math.random()}` },
    data: {
      audience: data.audience,
      selectedCustomerIds: data.selectedCustomerIds ?? [],
      title: data.title,
      message: data.message,
      actionUrl: data.actionUrl ?? null
    }
  });
  await expectOk(response);
  return (await response.json() as ApiResult<Broadcast>).data;
}

async function notificationsFor(
  request: import('@playwright/test').APIRequestContext, mobile: string
): Promise<Notification[]> {
  const token = await tokenFor(request, mobile);
  const response = await request.get(`${apiBaseUrl}/notifications`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<Notification[]>).data;
}

async function unreadCount(
  request: import('@playwright/test').APIRequestContext, mobile: string
): Promise<number> {
  const token = await tokenFor(request, mobile);
  const response = await request.get(`${apiBaseUrl}/notifications/unread-count`, { headers: bearer(token) });
  await expectOk(response);
  return (await response.json() as ApiResult<number>).data;
}

async function fullNameOf(
  request: import('@playwright/test').APIRequestContext, mobile: string
): Promise<string> {
  const admin = await tokenFor(request, process.env.E2E_ADMIN_MOBILE ?? '09120000011');
  const response = await request.get(
    `${apiBaseUrl}/admin/users?page=1&pageSize=5&search=${encodeURIComponent(mobile)}`,
    { headers: bearer(admin) });
  await expectOk(response);
  const items = (await response.json() as ApiResult<{ items: Array<{ mobile: string; fullName: string }> }>).data.items;
  const match = items.find(x => x.mobile === mobile);
  expect(match, `user ${mobile} must exist`).toBeTruthy();
  return match!.fullName;
}

async function tokenFor(request: import('@playwright/test').APIRequestContext, mobile: string): Promise<string> {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  const response = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile, password } });
  await expectOk(response);
  return (await response.json() as ApiResult<{ accessToken: string }>).data.accessToken;
}

function bearer(token: string) { return { Authorization: `Bearer ${token}` }; }

async function expectOk(response: import('@playwright/test').APIResponse) {
  expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy();
}

async function loginCustomer(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function expectNoOverflow(page: import('@playwright/test').Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
}

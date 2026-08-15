import { expect, test, type BrowserContext, type Page } from '@playwright/test';

const productUrl = '/product/e2e-seo-product';
const guestCookie = 'Vitorize.GuestCart';

async function addGuestItem(page: Page, context: BrowserContext): Promise<void> {
  await page.goto(productUrl, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect(page.locator('#product-input-account_email')).toBeVisible();
  await page.locator('#product-input-account_email').fill('fix03-guest@example.test');
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-dialog')).toBeHidden();
  const cookie = (await context.cookies()).find(x => x.name === guestCookie);
  expect(cookie).toBeTruthy();
  await expect.poll(async () => {
    const apiCart = await page.request.get('http://127.0.0.1:5177/api/cart', {
      headers: { 'X-Vitorize-Guest-Cart': cookie!.value }
    });
    if (apiCart.status() !== 200) return -1;
    return (await apiCart.json()).data.totalQuantity;
  }).toBe(1);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();
}

async function expectGuestCookie(context: BrowserContext): Promise<void> {
  const cookie = (await context.cookies()).find(x => x.name === guestCookie);
  expect(cookie).toBeTruthy();
  expect(cookie!.httpOnly).toBe(true);
  expect(cookie!.sameSite).toBe('Lax');
  expect(cookie!.path).toBe('/');
  expect(cookie!.expires).toBeGreaterThan(Date.now() / 1000);
}

async function readGuestCart(page: Page, context: BrowserContext) {
  const cookie = (await context.cookies()).find(x => x.name === guestCookie);
  expect(cookie).toBeTruthy();
  const response = await page.request.get('http://127.0.0.1:5177/api/cart', {
    headers: { 'X-Vitorize-Guest-Cart': cookie!.value }
  });
  expect(response.status()).toBe(200);
  return (await response.json()).data as { id: string; totalQuantity: number; items: Array<{ id: string; quantity: number }> };
}

async function addStagedGuestItem(page: Page, context: BrowserContext): Promise<void> {
  await page.goto('/product/e2e-staged-cart-product', { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await expect.poll(async () => (await readGuestCart(page, context)).totalQuantity).toBe(1);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Staged Cart Product' })).toBeVisible();
}

async function openStagedEditor(page: Page) {
  const item = page.locator('.st-cart-item').filter({ hasText: 'E2E Staged Cart Product' });
  await item.locator('button.st-btn--ghost').first().click();
  await expect(page.locator('.vz-dialog')).toBeVisible();
  return item;
}

async function saveStagedValues(page: Page, values = { text: 'guest-stage2-value', region: 'south', terms: true }) {
  await page.locator('input[id$="-stage2_reference"]').fill(values.text);
  await page.locator('select[id$="-stage2_region"]').selectOption(values.region);
  const terms = page.locator('input[id$="-stage2_terms"]');
  if (values.terms) await terms.check(); else await terms.uncheck();
  await page.locator('.vz-dialog button.st-btn--accent').click();
}

async function loginCustomer(page: Page) {
  await page.goto('/login', { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill('E2E-Admin-Only-aA1!');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();
  await expect(page).not.toHaveURL(/\/login/);
}

async function addPrimaryItem(page: Page, email: string) {
  await page.goto(productUrl, { waitUntil: 'networkidle' });
  await page.locator('.st-buy__card button.st-btn--accent').click();
  await page.locator('#product-input-account_email').fill(email);
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-dialog')).toBeHidden();
}

test('FIX-03 browser merge uses a seeded existing cart, persists after refresh, and isolates logout', async ({ page, context }) => {
  await addPrimaryItem(page, 'merge-match@example.test');
  await addPrimaryItem(page, 'merge-guest-x@example.test');
  const oldCookie = (await context.cookies()).find(x => x.name === guestCookie)!;
  const guestBefore = await readGuestCart(page, context);
  expect(guestBefore.items).toHaveLength(2);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcheckout/);
  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill('E2E-Admin-Only-aA1!');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();
  await expect(page).toHaveURL(/\/checkout/);
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');
  expect((await context.cookies()).some(x => x.name === guestCookie)).toBe(false);
  const oldCart = await page.request.get('http://127.0.0.1:5177/api/cart', { headers: { 'X-Vitorize-Guest-Cart': oldCookie.value } });
  expect((await oldCart.json()).data.totalQuantity).toBe(0);

  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(3);
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Related Product' })).toBeVisible();

  await page.locator('button.st-avatar').click();
  await page.locator('form[action="/auth/customer/logout"] button').click();
  await expect(page).not.toHaveURL(/\/cart$/);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item')).toHaveCount(0);
  await loginCustomer(page);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(3);
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Related Product' })).toBeVisible();
});

test('FIX-03 expired access token refreshes and preserves the authenticated cart', async ({ page }) => {
  test.setTimeout(130_000);
  const routes: Array<{ route: string; status: number }> = [];
  page.on('response', response => {
    const url = new URL(response.url());
    if (url.pathname === '/cart' || url.pathname.startsWith('/auth/')) routes.push({ route: url.pathname, status: response.status() });
  });
  await loginCustomer(page);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(2);
  await page.waitForTimeout(65_000);
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(2);
  await page.goto('/shop', { waitUntil: 'networkidle' });
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Related Product' })).toBeVisible();
  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item')).toHaveCount(3);
  expect(routes.filter(x => x.route === '/auth/customer/login' && x.status === 302)).toHaveLength(1);
});

test('FIX-03 guest Stage-2 edit persists across mobile reload, navigation, and second tab', async ({ page, context }) => {
  await page.setViewportSize({ width: 393, height: 852 });
  await addStagedGuestItem(page, context);
  const item = await openStagedEditor(page);
  await saveStagedValues(page);
  await expect(page.locator('.vz-dialog')).toBeHidden();
  await item.locator('.st-qty button').last().click();
  await expect(item.locator('.st-qty span')).toHaveText('۲');
  const expected = await readGuestCart(page, context);
  expect(expected.totalQuantity).toBe(2);
  expect(expected.items).toHaveLength(1);

  for (const action of [
    async () => page.reload({ waitUntil: 'networkidle' }),
    async () => { await page.goto('/shop', { waitUntil: 'networkidle' }); await page.goto('/cart', { waitUntil: 'networkidle' }); }
  ]) {
    await action();
    const actual = await readGuestCart(page, context);
    expect(actual.id).toBe(expected.id);
    expect(actual.totalQuantity).toBe(2);
    expect(actual.items).toHaveLength(1);
    await expect(page.locator('.st-cart-item').filter({ hasText: 'guest-stage2-value' })).toBeVisible();
    await expect(page.locator('.st-cart-item').filter({ hasText: 'south' })).toBeVisible();
  }
  const secondTab = await context.newPage();
  await secondTab.goto('/cart', { waitUntil: 'networkidle' });
  const same = await readGuestCart(secondTab, context);
  expect(same.id).toBe(expected.id);
  expect(same.totalQuantity).toBe(2);
  await expect(secondTab.locator('.st-cart-item').filter({ hasText: 'guest-stage2-value' })).toBeVisible();
});

test('FIX-03 guest Stage-2 validation blocks authentication before redirect', async ({ page, context }) => {
  await page.setViewportSize({ width: 393, height: 852 });
  await addStagedGuestItem(page, context);
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/cart$/);
  await expect(page.locator('.vz-dialog')).toBeVisible();
  const requiredText = page.locator('input[id$="-stage2_reference"]');
  await expect(requiredText).toHaveAttribute('aria-invalid', 'true');
  await expect(requiredText).toBeFocused();
  await expect(page.locator('.st-field__error[role="alert"]').first()).toBeVisible();
  await requiredText.fill('corrected-stage2-value');
  await page.locator('select[id$="-stage2_region"]').selectOption('north');
  await page.locator('input[id$="-stage2_terms"]').check();
  await page.locator('.vz-dialog button.st-btn--accent').click();
  await expect(page.locator('.vz-dialog')).toBeHidden();
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcheckout/);
  await expectGuestCookie(context);
});

test('FIX-03 cart load failure renders error then retry restores guest cart', async ({ page, context }) => {
  await addGuestItem(page, context);
  const expected = await readGuestCart(page, context);
  const arm = await page.request.post('http://127.0.0.1:5177/api/testing/cart/fail-next-read');
  expect(arm.status()).toBe(204);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-alert[role="alert"]')).toBeVisible();
  await expect(page.locator('.st-empty, .st-state').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(0);
  const untouched = await readGuestCart(page, context);
  expect(untouched.id).toBe(expected.id);
  await page.locator('.st-alert button').click();
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();
  expect((await readGuestCart(page, context)).id).toBe(expected.id);
});

test('FIX-03 tampered guest cookie creates isolated identity without data disclosure', async ({ page, context }) => {
  await addGuestItem(page, context);
  const original = await readGuestCart(page, context);
  const originalCookie = (await context.cookies()).find(x => x.name === guestCookie)!;
  await context.addCookies([{ ...originalCookie, value: 'invalid-guest-capability' }]);
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-cart-item').filter({ hasText: 'E2E Dynamic Product' })).toHaveCount(0);
  await expectGuestCookie(context);
  const replacement = await readGuestCart(page, context);
  expect(replacement.id).not.toBe(original.id);
  expect(replacement.totalQuantity).toBe(0);
  const protectedOriginal = await page.request.get('http://127.0.0.1:5177/api/cart', {
    headers: { 'X-Vitorize-Guest-Cart': originalCookie.value }
  });
  const protectedData = (await protectedOriginal.json()).data;
  expect(protectedData.id).toBe(original.id);
  expect(protectedData.totalQuantity).toBe(1);
});

test('FIX-03 guest cart survives refresh, navigation, second tab, and persisted-browser reopen', async ({ page, context, browser }) => {
  await addGuestItem(page, context);
  await expectGuestCookie(context);
  const item = page.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' });
  await expect(item.locator('.st-qty')).toContainText('۱');

  await page.reload({ waitUntil: 'networkidle' });
  await expect(page.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();
  await expectGuestCookie(context);

  await page.goto('/shop', { waitUntil: 'networkidle' });
  await page.goto('/cart', { waitUntil: 'networkidle' });
  await expect(page.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();

  const secondTab = await context.newPage();
  await secondTab.goto('/cart', { waitUntil: 'networkidle' });
  await expect(secondTab.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();

  const storage = await context.storageState();
  await context.close();
  const reopened = await browser.newContext({ baseURL: 'http://localhost:5077', storageState: storage });
  const reopenedPage = await reopened.newPage();
  await reopenedPage.goto('/cart', { waitUntil: 'networkidle' });
  await expect(reopenedPage.locator('.st-stack > .st-card').filter({ hasText: 'E2E Dynamic Product' })).toBeVisible();
  await expectGuestCookie(reopened);
  await reopened.close();
});

test('FIX-03 guest checkout requires login and merges into a cartless customer before checkout', async ({ page, context }) => {
  await addGuestItem(page, context);
  const originalCookie = (await context.cookies()).find(x => x.name === guestCookie)!;
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcheckout/);
  await expectGuestCookie(context);

  await page.locator('#pw-mobile').fill('09120000013');
  await page.locator('#pw-pass').fill('E2E-Admin-Only-aA1!');
  await page.locator('form[action="/auth/customer/login"] button[type="submit"]').click();
  await expect(page).toHaveURL(/\/checkout/);
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');
  expect((await context.cookies()).some(x => x.name === guestCookie)).toBe(false);

  const oldCapability = await page.request.get('http://127.0.0.1:5177/api/cart', {
    headers: { 'X-Vitorize-Guest-Cart': originalCookie.value }
  });
  expect(oldCapability.status()).toBe(200);
  expect((await oldCapability.json()).data.totalQuantity).toBe(0);
});

test('FIX-03 guest checkout registration merges before checkout', async ({ page, context }) => {
  await addGuestItem(page, context);
  const originalCookie = (await context.cookies()).find(x => x.name === guestCookie)!;
  await page.locator('.st-cart-sum button.st-btn--accent').click();
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcheckout/);
  await page.locator('a[href="/register?returnUrl=%2Fcheckout"]').click();
  await expect(page).toHaveURL(/\/register\?returnUrl=%2Fcheckout/);
  const stamp = Date.now();
  await page.locator('input[name="fullName"]').fill('FIX03 Registration');
  await page.locator('input[name="mobile"]').fill(`0919${String(stamp).slice(-7)}`);
  await page.locator('input[name="email"]').fill(`fix03-${stamp}@example.test`);
  await page.locator('input[name="password"]').fill('Fix03-Register-aA1!');
  await page.locator('form[action="/auth/customer/register"] button[type="submit"]').click();
  await expect(page).toHaveURL(/\/checkout/);
  await expect(page.locator('main')).toContainText('E2E Dynamic Product');
  expect((await context.cookies()).some(x => x.name === guestCookie)).toBe(false);
  const oldCapability = await page.request.get('http://127.0.0.1:5177/api/cart', {
    headers: { 'X-Vitorize-Guest-Cart': originalCookie.value }
  });
  expect((await oldCapability.json()).data.totalQuantity).toBe(0);
});

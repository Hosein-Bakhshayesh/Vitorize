import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, monitorBrowser } from './support/app';

type ApiResult<T> = { data: T };
type AdminPage = { id: string; title: string; slug: string; isSystem: boolean; isPublished: boolean };
type AdminFaq = { id: string; question: string };

const UNSAFE_HTML =
  '<h2>عنوان آزمون</h2><p onclick="window.__vzPwned=1">متن <strong>پررنگ</strong></p>' +
  '<script>window.__vzPwned=1</script><a href="javascript:window.__vzPwned=1">لینک بد</a>' +
  '<ul><li>مورد فهرست</li></ul>';

test.describe('FIX-14 CMS pages, system routes, contact and FAQ @fix14', () => {
  test.describe.configure({ timeout: 240_000 });

  test('Admin creates and publishes a custom page; unpublishing returns the customer a 404', async ({ page, request, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin journey runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const slug = uniqueSlug('journey');
    await loginAs('SuperAdmin');

    await page.goto('/admin/pages', { waitUntil: 'networkidle' });
    await page.getByTestId('page-create').click();
    await page.getByTestId('page-title').fill('راهنمای خرید آزمون');
    await page.getByTestId('page-slug').fill(slug);
    await page.getByTestId('page-seo-title').fill('راهنمای خرید | آزمون');
    await page.getByTestId('page-seo-description').fill('توضیح سئوی صفحه راهنمای خرید.');

    // The shared CKEditor component is reused as-is; type into its editable region.
    const editor = page.getByTestId('page-content-editor').locator('.ck-editor__editable');
    await expect(editor).toBeVisible();
    await editor.click();
    await page.keyboard.type('محتوای آزمایشی صفحه سفارشی');
    // CKEditor pushes into its Blazor-bound value on a fixed 220ms change debounce. Saving inside
    // that window stores the page with empty content, which is exactly how this intermittently
    // failed. Settle the debounce, as the admin product editor already does; the persisted value is
    // still asserted from the customer-facing page below, so this is a settle, not a crutch.
    await page.waitForTimeout(400);

    // The published control is a .vz-switch: its checkbox is visually hidden, so toggle the label.
    const published = page.getByTestId('page-published');
    if (!(await published.isChecked())) await published.locator('xpath=ancestor::label[1]').click();
    await expect(published).toBeChecked();

    await page.getByTestId('page-save').click();
    await expect(page.locator('.vz-toast').last()).toBeVisible();
    await expect(page.getByTestId(`page-row-${slug}`)).toBeVisible();

    // Customer view.
    await page.goto(`/page/${slug}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cms-page-title')).toHaveText('راهنمای خرید آزمون');
    await expect(page.getByTestId('cms-page-content')).toContainText('محتوای آزمایشی صفحه سفارشی');
    await expect(page).toHaveTitle(/راهنمای خرید/);
    await expect(page.locator('meta[name="description"]')).toHaveAttribute('content', /توضیح سئوی صفحه راهنمای خرید/);
    await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', new RegExp(`/page/${slug}$`));

    // Everything up to here must be console-clean. The step below deliberately requests an
    // unpublished page, which correctly answers 404 and therefore logs a failed resource load.
    consoleGuard.assertClean();

    // Unpublish through the API and confirm the public route stops serving it.
    const admin = await tokenFor(request, adminMobile());
    const created = await findPage(request, admin, slug);
    await expectOk(await request.post(`${apiBaseUrl}/admin/pages/${created.id}/unpublish`, { headers: bearer(admin) }));

    const notFound = await page.goto(`/page/${slug}`, { waitUntil: 'networkidle' });
    expect(notFound?.status()).toBe(404);
    await expect(page.locator('main')).not.toContainText('محتوای آزمایشی صفحه سفارشی');
  });

  test('dangerous page HTML is stripped and never executes in the customer browser', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Security coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const admin = await tokenFor(request, adminMobile());
    const slug = uniqueSlug('xss');
    await createPage(request, admin, { title: 'صفحه ناامن', slug, contentHtml: UNSAFE_HTML, isPublished: true });

    const monitor = monitorBrowser(page);
    await page.goto(`/page/${slug}`, { waitUntil: 'networkidle' });

    // No injected global, no script/handler survived, but safe formatting is intact.
    expect(await page.evaluate(() => (window as unknown as Record<string, unknown>).__vzPwned)).toBeUndefined();
    const content = page.getByTestId('cms-page-content');
    await expect(content.locator('script')).toHaveCount(0);
    await expect(content.locator('[onclick]')).toHaveCount(0);
    await expect(content.locator('a[href^="javascript:"]')).toHaveCount(0);
    await expect(content.locator('h2')).toHaveText('عنوان آزمون');
    await expect(content.locator('strong')).toHaveText('پررنگ');
    await expect(content.locator('li')).toHaveText('مورد فهرست');

    monitor.assertClean();
    consoleGuard.assertClean();
  });

  test('About, Terms and Privacy publish onto their canonical system routes', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'System page coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const admin = await tokenFor(request, adminMobile());

    for (const [slug, title] of [['about', 'درباره ما'], ['terms', 'قوانین و مقررات'], ['privacy', 'حریم خصوصی']] as const) {
      await publishSystemPage(request, admin, slug, title, `<p>محتوای آزمون ${slug}</p>`);

      await page.goto(`/${slug}`, { waitUntil: 'networkidle' });
      await expect(page.getByTestId('cms-page-title')).toHaveText(title);
      await expect(page.getByTestId('cms-page-content')).toContainText(`محتوای آزمون ${slug}`);
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', new RegExp(`/${slug}$`));
      // No consent UI belongs on Terms in FIX-14.
      await expect(page.locator('main input[type="checkbox"]')).toHaveCount(0);
    }

    // The generic route canonicalizes to the short system route rather than duplicating content.
    await page.goto('/page/about', { waitUntil: 'networkidle' });
    await expect(page).toHaveURL(/\/about$/);
    await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', /\/about$/);

    // Footer navigation points at the canonical routes.
    const footer = page.locator('footer').first();
    await expect(footer.locator('a[href="/about"]')).toBeVisible();
    await expect(footer.locator('a[href="/terms"]')).toBeVisible();
    await expect(footer.locator('a[href="/privacy"]')).toBeVisible();
    await expect(footer.locator('a[href="/page/about"]')).toHaveCount(0);

    consoleGuard.assertClean();
  });

  test('Contact combines the CMS intro with existing contact settings and offers no form', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Contact coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const admin = await tokenFor(request, adminMobile());
    await publishSystemPage(request, admin, 'contact', 'تماس با ما', '<p>برای ارتباط با ما از راه‌های زیر استفاده کنید.</p>');

    // Assert against the effective settings rather than writing new ones: StoreBrandingService
    // caches branding for two minutes, so the page must be checked against what the server serves.
    const settings = await publicSettings(request);
    expect(settings['SupportPhone'], 'a deterministic support phone must be configured').toBeTruthy();

    await page.goto('/contact', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cms-page-title')).toHaveText('تماس با ما');
    await expect(page.getByTestId('cms-page-content')).toContainText('برای ارتباط با ما');

    const details = page.getByTestId('contact-details');
    await expect(details).toBeVisible();
    for (const [key, testId] of [
      ['SupportPhone', 'contact-phone'], ['SupportEmail', 'contact-email'],
      ['ContactAddress', 'contact-address'], ['WorkingHours', 'contact-hours']
    ] as const) {
      const value = settings[key];
      if (value) await expect(page.getByTestId(testId)).toContainText(value);
      else await expect(page.getByTestId(testId)).toHaveCount(0, `${key} is unset so its row must be omitted`);
    }
    await expect(details.locator('a[href^="tel:"]')).toBeVisible();

    // Information only: no submission surface anywhere on the page.
    await expect(page.locator('main form')).toHaveCount(0);
    await expect(page.locator('main textarea')).toHaveCount(0);
    await expect(page.locator('main input[type="email"]')).toHaveCount(0);

    await page.locator('footer').first().locator('a[href="/contact"]').click();
    await expect(page).toHaveURL(/\/contact$/);

    consoleGuard.assertClean();
  });

  test('FAQ shows only active items in order, as text, with FAQPage JSON-LD', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'FAQ coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    const admin = await tokenFor(request, adminMobile());
    await clearFaqs(request, admin);

    await createFaq(request, admin, { question: 'پرسش دوم آزمون', answer: 'پاسخ دوم', sortOrder: 20, isActive: true });
    await createFaq(request, admin, { question: 'پرسش اول آزمون', answer: 'پاسخ اول <b>ساده</b>', sortOrder: 10, isActive: true });
    await createFaq(request, admin, { question: 'پرسش غیرفعال آزمون', answer: 'پاسخ پنهان', sortOrder: 1, isActive: false });

    await page.goto('/faq', { waitUntil: 'networkidle' });
    const questions = page.locator('.st-stack .st-card button span').first().locator('xpath=ancestor::div[contains(@class,"st-stack")]');
    await expect(page.locator('main')).not.toContainText('پرسش غیرفعال آزمون');
    await expect(page.locator('main')).toContainText('پرسش اول آزمون');
    await expect(page.locator('main')).toContainText('پرسش دوم آزمون');

    const ordered = await page.locator('.st-card button span').allInnerTexts();
    expect(ordered.filter(x => x.includes('آزمون'))).toEqual(['پرسش اول آزمون', 'پرسش دوم آزمون']);

    // Accordion: the answer appears only after activating its question.
    const firstToggle = page.locator('.st-card button').filter({ hasText: 'پرسش اول آزمون' });
    await expect(page.locator('main')).not.toContainText('پاسخ اول');
    await firstToggle.click();
    await expect(page.locator('main')).toContainText('پاسخ اول');
    // Answers are plain text: the markup in the answer is displayed, never interpreted.
    await expect(page.locator('main b').filter({ hasText: 'ساده' })).toHaveCount(0);
    await expect(page.locator('main')).toContainText('<b>ساده</b>');

    const jsonLd = await page.locator('script[type="application/ld+json"]').allTextContents();
    expect(jsonLd.some(x => x.includes('FAQPage'))).toBeTruthy();

    consoleGuard.assertClean();
  });

  test('mobile 390x844 renders a custom page, FAQ and Contact without overflow', async ({ page, request, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile smoke is sufficient.');
    await page.setViewportSize({ width: 390, height: 844 });
    const admin = await tokenFor(request, adminMobile());
    const slug = uniqueSlug('mobile');
    await createPage(request, admin, {
      title: 'صفحه موبایل', slug, isPublished: true,
      contentHtml: '<h2>عنوان</h2><p>متن طولانی برای بررسی چیدمان موبایل.</p><ul><li>مورد</li></ul>'
    });
    await publishSystemPage(request, admin, 'contact', 'تماس با ما', '<p>راه‌های ارتباطی</p>');

    await page.goto(`/page/${slug}`, { waitUntil: 'networkidle' });
    await expect(page.getByTestId('cms-page-title')).toBeVisible();
    await expectNoOverflow(page);

    await page.goto('/faq', { waitUntil: 'networkidle' });
    await page.locator('.st-card button').first().click();
    await expectNoOverflow(page);

    await page.goto('/contact', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('contact-details')).toBeVisible();
    await expectNoOverflow(page);

    consoleGuard.assertClean();
  });
});

function adminMobile() { return process.env.E2E_ADMIN_MOBILE ?? '09120000011'; }

function uniqueSlug(prefix: string) {
  return `fix14-${prefix}-${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`;
}

async function createPage(
  request: import('@playwright/test').APIRequestContext, token: string,
  data: { title: string; slug: string; contentHtml: string; isPublished: boolean }
) {
  const response = await request.post(`${apiBaseUrl}/admin/pages`, { headers: bearer(token), data });
  await expectOk(response);
  return (await response.json() as ApiResult<AdminPage>).data;
}

async function findPage(request: import('@playwright/test').APIRequestContext, token: string, slug: string): Promise<AdminPage> {
  const response = await request.get(`${apiBaseUrl}/admin/pages`, { headers: bearer(token) });
  await expectOk(response);
  const match = (await response.json() as ApiResult<AdminPage[]>).data.find(x => x.slug === slug);
  expect(match, `page '${slug}' must exist`).toBeTruthy();
  return match!;
}

/** Edits and publishes one of the four seeded system pages, leaving its slug identity intact. */
async function publishSystemPage(
  request: import('@playwright/test').APIRequestContext, token: string,
  slug: string, title: string, contentHtml: string
) {
  const page = await findPage(request, token, slug);
  expect(page.isSystem, `'${slug}' must be a system page`).toBeTruthy();
  await expectOk(await request.put(`${apiBaseUrl}/admin/pages/${page.id}`, {
    headers: bearer(token),
    data: { title, slug, contentHtml, seoTitle: title, seoDescription: `${title} — ویتورایز`, isPublished: true }
  }));
}

/** Effective public settings, which is exactly the source the storefront branding reads. */
async function publicSettings(request: import('@playwright/test').APIRequestContext): Promise<Record<string, string>> {
  const response = await request.get(`${apiBaseUrl}/settings/public`);
  await expectOk(response);
  const items = (await response.json() as ApiResult<Array<{ key: string; value: string | null }>>).data;
  return Object.fromEntries(items.map(x => [x.key, (x.value ?? '').trim()]));
}

async function clearFaqs(request: import('@playwright/test').APIRequestContext, token: string) {
  const response = await request.get(`${apiBaseUrl}/admin/faqs`, { headers: bearer(token) });
  await expectOk(response);
  for (const faq of (await response.json() as ApiResult<AdminFaq[]>).data) {
    await expectOk(await request.delete(`${apiBaseUrl}/admin/faqs/${faq.id}`, { headers: bearer(token) }));
  }
}

async function createFaq(
  request: import('@playwright/test').APIRequestContext, token: string,
  data: { question: string; answer: string; sortOrder: number; isActive: boolean }
) {
  await expectOk(await request.post(`${apiBaseUrl}/admin/faqs`, { headers: bearer(token), data }));
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

async function expectNoOverflow(page: import('@playwright/test').Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
}

/*
 * Captures the implemented UI route inventory at a phone viewport. It is intentionally
 * standalone: it is useful when the full end-to-end data-creation suite is unavailable,
 * while still exercising the actual rendered pages and authenticated admin shell.
 *
 * Usage:
 *   node scripts/CaptureMobileRouteAudit.cjs http://127.0.0.1:5087 artifacts/mobile-audit
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const baseUrl = process.argv[2] || 'http://127.0.0.1:5087';
const output = path.resolve(process.argv[3] || 'artifacts/mobile-audit');
const adminMobile = process.env.E2E_ADMIN_MOBILE || '09120000011';
const password = process.env.E2E_QA_PASSWORD || process.env.E2E_ADMIN_PASSWORD || 'E2E-Admin-Only-aA1!';

const publicRoutes = [
  '/', '/access-denied', '/blog', '/blog/responsive-missing', '/brand/e2e-brand',
  '/categories', '/category/e2e-category?sort=newest', '/error', '/error/500', '/faq',
  '/forgot-password', '/login', '/page/about', '/payment/result?orderId=31000000-0000-0000-0000-000000000099&paid=0',
  '/product/e2e-seo-product', '/register', '/reset-password?mobile=09123456789',
  '/search?q=E2E%20Dynamic%20Product%20with%20long%20English%20query', '/shop?q=E2E%20Dynamic&sort=price-desc'
];

const customerRoutes = [
  '/customer/dashboard', '/customer/gift-codes', '/customer/notifications', '/customer/orders',
  '/customer/orders/31000000-0000-0000-0000-000000000099', '/customer/profile', '/customer/reviews',
  '/customer/tickets', '/customer/tickets/31000000-0000-0000-0000-000000000099',
  '/customer/tickets/new?orderId=31000000-0000-0000-0000-000000000099', '/customer/verification',
  '/customer/wallet', '/customer/wishlist'
];

const adminRoutes = [
  '/admin/dashboard', '/admin/audit-logs', '/admin/banners', '/admin/brands', '/admin/categories',
  '/admin/coupons', '/admin/error-logs', '/admin/gift-codes', '/admin/monitoring', '/admin/notifications',
  '/admin/orders', '/admin/payments', '/admin/products', '/admin/products/create',
  '/admin/products/31000000-0000-0000-0000-000000000002',
  '/admin/products/31000000-0000-0000-0000-000000000002/details',
  '/admin/products/31000000-0000-0000-0000-000000000002/images', '/admin/product-tags',
  '/admin/reports', '/admin/reviews', '/admin/roles', '/admin/security-logs', '/admin/settings',
  '/admin/sms', '/admin/tickets', '/admin/tools', '/admin/users', '/admin/verifications',
  '/admin/kyc-policies', '/admin/wallets'
];

const fileName = (index, route) => `${String(index + 1).padStart(2, '0')}-${route
  .replace(/^\//, '').replace(/[/?&=]+/g, '-').replace(/[^a-zA-Z0-9\-]/g, '').slice(0, 72) || 'home'}.png`;

async function settle(page) {
  await page.locator('#vz-initial-loader').waitFor({ state: 'hidden', timeout: 8_000 }).catch(() => {});
  await page.waitForTimeout(1_500);
}

async function scan(page) {
  return page.evaluate(() => {
    const root = document.documentElement;
    const visible = el => {
      const r = el.getBoundingClientRect();
      const s = getComputedStyle(el);
      return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
    };
    const allowed = '.vz-table-wrap,.st-table-wrap,.vz-tabs,.vz-settabs,.ck-toolbar,.st-hslider,.st-trustchips,.st-catrail,.st-marquee';
    const offenders = [...document.querySelectorAll('body *')]
      .filter(visible)
      .filter(el => !el.matches('.st-news__aurora') && !el.closest(allowed) && !el.closest('.vz-sidebar:not(.open),[aria-hidden="true"]'))
      .filter(el => {
        const r = el.getBoundingClientRect();
        return r.left < -1 || r.right > innerWidth + 1;
      })
      .slice(0, 8)
      .map(el => ({ tag: el.tagName.toLowerCase(), className: String(el.className || '').slice(0, 120) }));
    return { viewport: innerWidth, documentOverflow: root.scrollWidth - root.clientWidth, offenders };
  });
}

async function signIn(page, url, mobile, action) {
  await page.goto(`${url}/admin/login`, { waitUntil: 'domcontentloaded', timeout: 20_000 });
  await page.locator('input[name="mobile"]').fill(mobile);
  await page.locator('input[name="password"]').fill(password);
  await Promise.all([
    page.waitForURL(/\/admin(\/dashboard)?$/i, { timeout: 15_000 }),
    page.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
  ]);
}

async function signInCustomer(page, url) {
  await page.goto(`${url}/login`, { waitUntil: 'domcontentloaded', timeout: 20_000 });
  await page.locator('#pw-mobile').fill(process.env.E2E_CUSTOMER_MOBILE || '09120000013');
  await page.locator('#pw-pass').fill(password);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/i, { timeout: 15_000 }),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

async function captureGroup(page, group, routes, report) {
  for (let index = 0; index < routes.length; index += 1) {
    const route = routes[index];
    const entry = { group, route, url: `${baseUrl}${route}` };
    try {
      const response = await page.goto(entry.url, { waitUntil: 'domcontentloaded', timeout: 20_000 });
      await settle(page);
      entry.status = response?.status() || 200;
      entry.title = await page.title();
      Object.assign(entry, await scan(page));
      entry.screenshot = path.join(group, fileName(index, route));
      await page.screenshot({ path: path.join(output, entry.screenshot), fullPage: true, animations: 'disabled' });
    } catch (error) {
      entry.error = String(error.message || error);
      entry.screenshot = path.join(group, `failed-${fileName(index, route)}`);
      await page.screenshot({ path: path.join(output, entry.screenshot), fullPage: true }).catch(() => {});
    }
    report.push(entry);
    process.stdout.write(`${group} ${index + 1}/${routes.length}: ${route}${entry.error ? ' (failed)' : ''}\n`);
  }
}

async function captureOverlay(page, name, open, close, report) {
  const entry = { group: 'overlays', route: name, url: page.url() };
  try {
    await open();
    await settle(page);
    Object.assign(entry, await scan(page));
    entry.screenshot = path.join('overlays', `${name}.png`);
    await page.screenshot({ path: path.join(output, entry.screenshot), fullPage: true, animations: 'disabled' });
    await close().catch(() => {});
  } catch (error) {
    entry.error = String(error.message || error);
  }
  report.push(entry);
  process.stdout.write(`overlays: ${name}${entry.error ? ' (failed)' : ''}\n`);
}

(async () => {
  fs.rmSync(output, { recursive: true, force: true });
  for (const group of ['public', 'customer', 'admin', 'overlays']) fs.mkdirSync(path.join(output, group), { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, isMobile: true, hasTouch: true, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
  const page = await context.newPage();
  const report = [];
  await captureGroup(page, 'public', publicRoutes, report);
  await signInCustomer(page, baseUrl).catch(error => {
    console.warn(`Customer sign-in unavailable: ${String(error.message || error)}`);
  });
  await captureGroup(page, 'customer', customerRoutes, report);
  await signIn(page, baseUrl, adminMobile);
  await captureGroup(page, 'admin', adminRoutes, report);
  await page.goto(`${baseUrl}/admin/orders`, { waitUntil: 'domcontentloaded' });
  await settle(page);
  await captureOverlay(page, 'admin-order-detail', async () => {
    await page.locator('.orders-mobile-card').first().click();
    await page.getByRole('dialog').waitFor({ state: 'visible', timeout: 10_000 });
  }, () => page.locator('[role="dialog"] .vz-dialog__close').click(), report);
  await page.goto(`${baseUrl}/admin/payments`, { waitUntil: 'domcontentloaded' });
  await settle(page);
  await captureOverlay(page, 'admin-payment-detail', async () => {
    await page.locator('.vz-table-wrap .vz-iconaction').first().click();
    await page.locator('.vz-slidepanel').waitFor({ state: 'visible', timeout: 10_000 });
  }, () => page.locator('.vz-slidepanel .vz-slidepanel__close').click(), report);
  await page.goto(`${baseUrl}/admin/users`, { waitUntil: 'domcontentloaded' });
  await settle(page);
  await captureOverlay(page, 'admin-user-detail', async () => {
    await page.locator('.users-mobile-card__action').first().click();
    await page.locator('.vz-slidepanel').waitFor({ state: 'visible', timeout: 10_000 });
  }, () => page.locator('.vz-slidepanel .vz-slidepanel__close').click(), report);
  fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify({ baseUrl, viewport: '412x915', routes: report }, null, 2));
  await browser.close();
  const blocked = report.filter(item => item.error || item.documentOverflow > 1 || item.offenders?.length);
  console.log(`Captured ${report.length} routes. Responsive findings: ${blocked.length}.`);
  process.exitCode = blocked.length ? 2 : 0;
})().catch(error => { console.error(error); process.exitCode = 1; });

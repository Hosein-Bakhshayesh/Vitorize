// TEMPORARY documentation screenshot generator for the Vitorize manuals.
// Not part of the application or the test suite. Deleted once the guides are rebuilt.
//
// Usage:  node shotgen.mjs [filter]
// Captures the current UI at 1440x900 (desktop, light theme) and 390x844 (mobile) into
// manuals/build/shots/. Every capture uses the deterministic QA seed data.

// Resolved by absolute path so this generator can live outside the test project.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
import { mkdirSync, existsSync } from 'node:fs';
import path from 'node:path';

const ORIGIN = process.env.DOC_ORIGIN ?? 'http://localhost:5077';
const PASSWORD = process.env.E2E_QA_PASSWORD ?? 'E2E-Admin-Only-aA1!';
const ADMIN = process.env.E2E_ADMIN_MOBILE ?? '09120000011';
const CUSTOMER = process.env.E2E_CUSTOMER_MOBILE ?? '09120000013';
const OUT = path.resolve('D:/Vitorize/manuals/build/shots');
const DESKTOP = { width: 1440, height: 900 };
const MOBILE = { width: 390, height: 844 };

const filter = process.argv[2] ?? '';
const done = [];
const failed = [];
if (!existsSync(OUT)) mkdirSync(OUT, { recursive: true });

/** Light theme before first paint, and disable animations so captures are deterministic. */
async function newContext(browser, viewport) {
  const context = await browser.newContext({
    viewport,
    deviceScaleFactor: 2,          // crisp text in the PDF
    locale: 'fa-IR',
    colorScheme: 'light',
    ignoreHTTPSErrors: true
  });
  await context.addInitScript(() => {
    try { localStorage.setItem('vitorize-theme', 'light'); } catch { /* first-party only */ }
  });
  await context.addStyleTag?.({}).catch?.(() => {});
  return context;
}

async function settle(page) {
  await page.addStyleTag({
    content: `*,*::before,*::after{animation-duration:0s!important;animation-delay:0s!important;
              transition-duration:0s!important;transition-delay:0s!important}
              #vz-initial-loader{display:none!important}`
  }).catch(() => {});
  await page.waitForTimeout(450);
}

async function shot(page, name, opts = {}) {
  if (filter && !name.includes(filter)) return;
  await settle(page);
  const file = path.join(OUT, `${name}.png`);
  try {
    if (opts.clip) await page.screenshot({ path: file, clip: opts.clip });
    else await page.screenshot({ path: file, fullPage: !!opts.full });
    done.push(name);
    console.log(`  ok   ${name}`);
  } catch (error) {
    failed.push(`${name}: ${error.message}`);
    console.log(`  FAIL ${name}: ${error.message}`);
  }
}

async function go(page, url, waitFor) {
  await page.goto(`${ORIGIN}${url}`, { waitUntil: 'networkidle', timeout: 60_000 });
  if (waitFor) await page.locator(waitFor).first().waitFor({ timeout: 20_000 }).catch(() => {});
  await page.waitForTimeout(300);
}

async function loginAdmin(page) {
  await page.goto(`${ORIGIN}/admin/login`, { waitUntil: 'networkidle' });
  await page.locator('input[name="mobile"]').fill(ADMIN);
  await page.locator('input[name="password"]').fill(PASSWORD);
  await Promise.all([
    page.waitForURL(/\/admin(\/dashboard)?$/i, { timeout: 40_000 }),
    page.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
  ]);
}

async function loginCustomer(page) {
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  await page.locator('#pw-mobile').fill(CUSTOMER);
  await page.locator('#pw-pass').fill(PASSWORD);
  await Promise.all([
    page.waitForURL(/\/customer\/dashboard/i, { timeout: 40_000 }),
    page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()
  ]);
}

/** Masks any element that could leak a usable secret (gift codes, tokens). */
async function maskSecrets(page) {
  await page.evaluate(() => {
    const looksLikeCode = /^[A-Z0-9]{4}-?[A-Z0-9]{4}-?[A-Z0-9]{4}/;
    document.querySelectorAll('code, .st-code, .vz-code, [data-testid*="code"]').forEach(el => {
      const text = (el.textContent || '').trim();
      if (looksLikeCode.test(text)) el.textContent = 'XXXX-XXXX-XXXX';
    });
  }).catch(() => {});
}

const ADMIN_PAGES = [
  ['adm-dashboard',        '/admin/dashboard'],
  ['adm-products',         '/admin/products'],
  ['adm-product-create',   '/admin/products/new'],
  ['adm-categories',       '/admin/categories'],
  ['adm-brands',           '/admin/brands'],
  ['adm-product-tags',     '/admin/product-tags'],
  ['adm-gift-codes',       '/admin/gift-codes'],
  ['adm-banners',          '/admin/banners'],
  ['adm-pages',            '/admin/pages'],
  ['adm-faqs',             '/admin/faqs'],
  ['adm-orders',           '/admin/orders'],
  ['adm-payments',         '/admin/payments'],
  ['adm-wallets',          '/admin/wallets'],
  ['adm-coupons',          '/admin/coupons'],
  ['adm-users',            '/admin/users'],
  ['adm-roles',            '/admin/roles'],
  ['adm-verifications',    '/admin/verifications'],
  ['adm-kyc-policies',     '/admin/kyc-policies'],
  ['adm-tickets',          '/admin/tickets'],
  ['adm-reviews',          '/admin/reviews'],
  ['adm-notifications',    '/admin/notifications'],
  ['adm-sms',              '/admin/sms'],
  ['adm-monitoring',       '/admin/monitoring'],
  ['adm-reports',          '/admin/reports'],
  ['adm-settings',         '/admin/settings'],
  ['adm-audit-logs',       '/admin/audit-logs'],
  ['adm-error-logs',       '/admin/error-logs'],
  ['adm-security-logs',    '/admin/security-logs'],
  ['adm-tools',            '/admin/tools']
];

const PUBLIC_PAGES = [
  ['pub-home',            '/'],
  ['pub-shop',            '/shop'],
  ['pub-categories',      '/categories'],
  ['pub-product-detail',  '/product/e2e-seo-product'],
  ['pub-faq',             '/faq'],
  ['pub-page-about',      '/about'],
  ['pub-page-terms',      '/terms'],
  ['pub-page-privacy',    '/privacy'],
  ['pub-page-contact',    '/contact'],
  ['pub-login',           '/login'],
  ['pub-register',        '/register'],
  ['pub-forgot-password', '/forgot-password']
];

const CUSTOMER_PAGES = [
  ['cust-dashboard',     '/customer/dashboard'],
  ['cust-orders',        '/customer/orders'],
  ['cust-gift-codes',    '/customer/gift-codes'],
  ['cust-wishlist',      '/customer/wishlist'],
  ['cust-wallet',        '/customer/wallet'],
  ['cust-verification',  '/customer/verification'],
  ['cust-tickets',       '/customer/tickets'],
  ['cust-notifications', '/customer/notifications'],
  ['cust-reviews',       '/customer/reviews'],
  ['cust-profile',       '/customer/profile']
];

const MOBILE_PAGES = [
  ['m-home',        '/',                      false],
  ['m-shop',        '/shop',                  false],
  ['m-product',     '/product/e2e-seo-product', false],
  ['m-cart',        '/cart',                  false],
  ['m-login',       '/login',                 false],
  ['m-dashboard',   '/customer/dashboard',    true],
  ['m-orders',      '/customer/orders',       true],
  ['m-notifications', '/customer/notifications', true]
];

// The QA suite drives the installed Chrome channel rather than a bundled build; match it.
const browser = await chromium.launch({ channel: 'chrome' });

// ---------- Admin (desktop) ----------
{
  const context = await newContext(browser, DESKTOP);
  const page = await context.newPage();
  await loginAdmin(page);
  for (const [name, url] of ADMIN_PAGES) {
    if (filter && !name.includes(filter)) continue;
    try { await go(page, url); await maskSecrets(page); await shot(page, name); }
    catch (e) { failed.push(`${name}: ${e.message}`); console.log(`  FAIL ${name}: ${e.message}`); }
  }

  // Settings: one capture per tab, in the order the UI renders them.
  if (!filter || filter.includes('settings')) {
    await go(page, '/admin/settings');
    const tabs = page.locator('.vz-settab');
    const count = await tabs.count();
    for (let i = 0; i < count; i++) {
      await tabs.nth(i).click();
      await page.waitForTimeout(600);
      await shot(page, `adm-settings-${String(i + 1).padStart(2, '0')}`);
    }
    // The loading-media field (custom loader) lives on the logos tab.
    const logosTab = page.locator('.vz-settab', { hasText: 'لوگو و تصاویر' }).first();
    if (await logosTab.count()) {
      await logosTab.click();
      const field = page.getByTestId('setting-LoadingMediaPath');
      await field.scrollIntoViewIfNeeded().catch(() => {});
      await page.waitForTimeout(400);
      const box = await field.boundingBox().catch(() => null);
      if (box) {
        await shot(page, 'adm-setting-loading-media', {
          clip: { x: Math.max(0, box.x - 16), y: Math.max(0, box.y - 16), width: Math.min(1440, box.width + 32), height: box.height + 32 }
        });
      }
    }
  }

  // Broadcast panel.
  if (!filter || filter.includes('broadcast')) {
    await go(page, '/admin/notifications');
    const open = page.getByTestId('broadcast-open');
    if (await open.count()) {
      await open.click();
      await page.waitForTimeout(700);
      await shot(page, 'adm-broadcast');
    }
  }

  // Order details (first row).
  if (!filter || filter.includes('order')) {
    await go(page, '/admin/orders');
    const row = page.locator('table tbody tr').first();
    if (await row.count()) {
      await row.locator('button, a').first().click().catch(() => {});
      await page.waitForTimeout(900);
      await maskSecrets(page);
      await shot(page, 'adm-order-detail');
    }
  }

  // KYC review detail.
  if (!filter || filter.includes('verification')) {
    await go(page, '/admin/verifications');
    const row = page.locator('table tbody tr').first();
    if (await row.count()) {
      await row.locator('button, a').first().click().catch(() => {});
      await page.waitForTimeout(900);
      await shot(page, 'adm-verification-detail');
    }
  }

  await context.close();
}

// ---------- Public storefront (desktop) ----------
{
  const context = await newContext(browser, DESKTOP);
  const page = await context.newPage();
  for (const [name, url] of PUBLIC_PAGES) {
    if (filter && !name.includes(filter)) continue;
    try { await go(page, url); await maskSecrets(page); await shot(page, name); }
    catch (e) { failed.push(`${name}: ${e.message}`); console.log(`  FAIL ${name}: ${e.message}`); }
  }
  await context.close();
}

// ---------- Customer account + cart/checkout (desktop) ----------
{
  const context = await newContext(browser, DESKTOP);
  const page = await context.newPage();
  await loginCustomer(page);
  for (const [name, url] of CUSTOMER_PAGES) {
    if (filter && !name.includes(filter)) continue;
    try { await go(page, url); await maskSecrets(page); await shot(page, name); }
    catch (e) { failed.push(`${name}: ${e.message}`); console.log(`  FAIL ${name}: ${e.message}`); }
  }
  if (!filter || filter.includes('flow')) {
    await go(page, '/cart');       await maskSecrets(page); await shot(page, 'flow-cart');
    await go(page, '/checkout');   await maskSecrets(page); await shot(page, 'flow-checkout');
  }
  await context.close();
}

// ---------- Mobile ----------
{
  const context = await newContext(browser, MOBILE);
  const page = await context.newPage();
  let authed = false;
  for (const [name, url, needsAuth] of MOBILE_PAGES) {
    if (filter && !name.includes(filter)) continue;
    try {
      if (needsAuth && !authed) { await loginCustomer(page); authed = true; }
      await go(page, url); await maskSecrets(page); await shot(page, name);
    } catch (e) { failed.push(`${name}: ${e.message}`); console.log(`  FAIL ${name}: ${e.message}`); }
  }
  // Mobile filter sheet on the shop page.
  if (!filter || filter.includes('m-filters')) {
    await go(page, '/shop');
    const fab = page.locator('.st-fab');
    if (await fab.count()) { await fab.click(); await page.waitForTimeout(700); await shot(page, 'm-filters'); }
  }
  await context.close();
}

await browser.close();
console.log(`\ncaptured=${done.length} failed=${failed.length}`);
if (failed.length) { console.log('FAILURES:'); failed.forEach(f => console.log('  ' + f)); }

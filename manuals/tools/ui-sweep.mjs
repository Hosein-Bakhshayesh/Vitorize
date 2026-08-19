// Vitorize full UI/UX route sweep — every reachable route, desktop + mobile, per-route findings.
// usage: node ui-sweep.mjs <origin> <desktop|mobile> [evidenceDir]
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = process.argv[2] || 'http://localhost:5077';
const MODE = process.argv[3] || 'desktop';
const VP = MODE === 'mobile' ? { width: 390, height: 844 } : { width: 1440, height: 900 };
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';

const E = {
  productSlug: 'e2e-seo-product', productId: '31000000-0000-0000-0000-000000000002',
  categorySlug: 'e2e-category', brandSlug: 'e2e-brand',
  orderId: '32000000-0000-0000-0000-000000000001', ticketId: '32000000-0000-0000-0000-000000000006',
  pageSlug: 'about'
};

const ROUTES = [
  ['PUBLIC', '/', 'anon'], ['PUBLIC', '/shop', 'anon'], ['PUBLIC', '/categories', 'anon'],
  ['PUBLIC', `/category/${E.categorySlug}`, 'anon'], ['PUBLIC', `/brand/${E.brandSlug}`, 'anon'],
  ['PUBLIC', `/product/${E.productSlug}`, 'anon'], ['PUBLIC', '/search?q=e2e', 'anon'],
  ['PUBLIC', '/cart', 'anon'], ['PUBLIC', '/checkout', 'customer'], ['PUBLIC', '/payment/result', 'customer'],
  ['AUTH', '/login', 'anon'], ['AUTH', '/register', 'anon'],
  ['AUTH', '/forgot-password', 'anon'], ['AUTH', '/reset-password', 'anon'],
  ['CMS', '/about', 'anon'], ['CMS', '/terms', 'anon'], ['CMS', '/privacy', 'anon'],
  ['CMS', '/contact', 'anon'], ['CMS', '/faq', 'anon'], ['CMS', `/page/${E.pageSlug}`, 'anon'],
  ['CMS', '/blog', 'anon'], ['CMS', '/blog/sweep-blog-post', 'anon'],
  ['ADMIN', '/admin/blog', 'admin'],
  ['CUSTOMER', '/customer/dashboard', 'customer'], ['CUSTOMER', '/customer/orders', 'customer'],
  ['CUSTOMER', `/customer/orders/${E.orderId}`, 'customer'], ['CUSTOMER', '/customer/wishlist', 'customer'],
  ['CUSTOMER', '/customer/wallet', 'customer'], ['CUSTOMER', '/customer/gift-codes', 'customer'],
  ['CUSTOMER', '/customer/notifications', 'customer'], ['CUSTOMER', '/customer/verification', 'customer'],
  ['CUSTOMER', '/customer/profile', 'customer'], ['CUSTOMER', '/customer/reviews', 'customer'],
  ['CUSTOMER', '/customer/tickets', 'customer'], ['CUSTOMER', '/customer/tickets/new', 'customer'],
  ['CUSTOMER', `/customer/tickets/${E.ticketId}`, 'customer'],
  ['ADMIN', '/admin/dashboard', 'admin'], ['ADMIN', '/admin/products', 'admin'],
  ['ADMIN', '/admin/products/create', 'admin'], ['ADMIN', `/admin/products/${E.productId}`, 'admin'],
  ['ADMIN', `/admin/products/${E.productId}/details`, 'admin'], ['ADMIN', `/admin/products/${E.productId}/images`, 'admin'],
  ['ADMIN', '/admin/categories', 'admin'], ['ADMIN', '/admin/brands', 'admin'],
  ['ADMIN', '/admin/product-tags', 'admin'], ['ADMIN', '/admin/gift-codes', 'admin'],
  ['ADMIN', '/admin/banners', 'admin'], ['ADMIN', '/admin/pages', 'admin'], ['ADMIN', '/admin/faqs', 'admin'],
  ['ADMIN', '/admin/orders', 'admin'], ['ADMIN', '/admin/payments', 'admin'],
  ['ADMIN', '/admin/wallets', 'admin'], ['ADMIN', '/admin/coupons', 'admin'],
  ['ADMIN', '/admin/users', 'admin'], ['ADMIN', '/admin/roles', 'admin'],
  ['ADMIN', '/admin/verifications', 'admin'], ['ADMIN', '/admin/kyc-policies', 'admin'],
  ['ADMIN', '/admin/tickets', 'admin'], ['ADMIN', '/admin/reviews', 'admin'],
  ['ADMIN', '/admin/notifications', 'admin'], ['ADMIN', '/admin/sms', 'admin'],
  ['ADMIN', '/admin/monitoring', 'admin'], ['ADMIN', '/admin/reports', 'admin'],
  ['ADMIN', '/admin/settings', 'admin'], ['ADMIN', '/admin/audit-logs', 'admin'],
  ['ADMIN', '/admin/error-logs', 'admin'], ['ADMIN', '/admin/security-logs', 'admin'],
  ['ADMIN', '/admin/tools', 'admin'],
  ['ERROR', '/access-denied', 'anon'], ['ERROR', '/error', 'anon'],
  ['ERROR', '/error/404', 'anon'], ['ERROR', '/definitely-not-a-real-route', 'anon']
];

const browser = await chromium.launch({ channel: 'chrome' });
async function ctxFor(role) {
  const c = await browser.newContext({ viewport: VP, colorScheme: 'light', locale: 'fa-IR' });
  const p = await c.newPage();
  if (role === 'customer') {
    await p.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
    await p.locator('#pw-mobile').fill('09120000013');
    await p.locator('#pw-pass').fill(PW);
    await Promise.all([p.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 40000 }),
                       p.locator('form[action="/auth/customer/login"] button[type=submit]').click()]);
  } else if (role === 'admin') {
    await p.goto(`${ORIGIN}/admin/login`, { waitUntil: 'networkidle' });
    await p.locator('input[name="mobile"]').fill('09120000011');
    await p.locator('input[name="password"]').fill(PW);
    await Promise.all([p.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login'), { timeout: 45000 }),
                       p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);
  }
  await p.close();
  return c;
}
const ctxs = { anon: await ctxFor('anon'), customer: await ctxFor('customer'), admin: await ctxFor('admin') };

// Routes whose own document is deliberately a 404 (error-page probes). The 404 status and the
// console entry the browser emits for it are the expected outcome, not a defect.
const EXPECTED_404 = /definitely-not-a-real-route|\/error\/404/;
const isExpected404Route = r => EXPECTED_404.test(r);
const findings = [];
let passed = 0;

for (const [area, route, role] of ROUTES) {
  const page = await ctxs[role].newPage();
  const errs = [], pageErrs = [], bad = [];
  const expected404 = isExpected404Route(route);
  page.on('console', m => {
    if (m.type() !== 'error') return;
    if (expected404 && /404/.test(m.text())) return;   // the probe's own 404
    errs.push(m.text().slice(0, 120));
  });
  page.on('pageerror', e => pageErrs.push(String(e).slice(0, 120)));
  page.on('response', r => { if (r.status() >= 400 && !EXPECTED_404.test(r.url())) bad.push(`${r.status()} ${r.url().split('/').slice(3).join('/').slice(0, 60)}`); });

  let status = 0;
  try { const resp = await page.goto(`${ORIGIN}${route}`, { waitUntil: 'networkidle', timeout: 60000 }); status = resp ? resp.status() : 0; }
  catch (e) { findings.push({ area, route, kind: 'navigation', detail: String(e).split('\n')[0].slice(0, 110) }); await page.close(); continue; }

  await page.waitForTimeout(500);
  // scroll the whole page so lazy content and sticky behaviour engage
  await page.evaluate(async () => {
    const h = document.body.scrollHeight;
    for (let y = 0; y <= h; y += Math.max(400, window.innerHeight - 100)) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 90)); }
    window.scrollTo(0, 0);
  });
  await page.waitForTimeout(250);

  const d = await page.evaluate(() => {
    const de = document.documentElement;
    const vw = de.clientWidth;
    const brokenImgs = [...document.images]
      .filter(i => { const r = i.getBoundingClientRect(); return r.width > 0 && r.height > 0 && !(i.complete && i.naturalWidth > 0); })
      .map(i => i.getAttribute('src') || '(no src)');
    // product media must be square
    const nonSquare = [...document.querySelectorAll('.st-pcard__media, .st-gal__main, .st-cart-item__image')]
      .map(e => { const r = e.getBoundingClientRect(); return { w: Math.round(r.width), h: Math.round(r.height) }; })
      .filter(m => m.w > 4 && Math.abs(m.w - m.h) > 2);
    // any element sticking out past the right/left edge of the viewport
    const wide = [...document.querySelectorAll('body *')].filter(el => {
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0) return false;
      const cs = getComputedStyle(el);
      if (cs.position === 'fixed' || cs.visibility === 'hidden' || cs.opacity === '0') return false;
      return r.right > vw + 2 || r.left < -2;
    }).slice(0, 3).map(el => `${el.tagName.toLowerCase()}.${(el.className || '').toString().split(' ')[0]}`);
    return {
      overflow: de.scrollWidth - de.clientWidth,
      brokenImgs, nonSquare, wide,
      title: (document.querySelector('h1') || {}).textContent?.trim().slice(0, 40) || '(no h1)',
      bodyText: (document.body.innerText || '').length
    };
  });

  const issues = [];
  if (status >= 500) issues.push({ kind: 'http', detail: `status ${status}` });
  if (d.overflow > 1) issues.push({ kind: 'h-overflow', detail: `${d.overflow}px (${d.wide.join(', ') || 'n/a'})` });
  if (d.brokenImgs.length) issues.push({ kind: 'broken-image', detail: d.brokenImgs.slice(0, 2).join(', ') });
  if (d.nonSquare.length) issues.push({ kind: 'product-media-not-square', detail: JSON.stringify(d.nonSquare[0]) });
  if (pageErrs.length) issues.push({ kind: 'page-error', detail: pageErrs[0] });
  if (errs.length) issues.push({ kind: 'console-error', detail: errs[0] });
  if (bad.length) issues.push({ kind: 'failed-request', detail: bad.slice(0, 2).join(' | ') });
  if (d.bodyText < 40) issues.push({ kind: 'empty-render', detail: `only ${d.bodyText} chars` });

  if (issues.length) { issues.forEach(i => findings.push({ area, route, ...i })); console.log(`  FAIL ${area.padEnd(8)} ${route}`); issues.forEach(i => console.log(`         ${i.kind}: ${i.detail}`)); }
  else { passed++; console.log(`  ok   ${area.padEnd(8)} ${route}`); }
  await page.close();
}

for (const c of Object.values(ctxs)) await c.close();
await browser.close();

console.log(`\n===== ${MODE} ${VP.width}x${VP.height} =====`);
console.log(`routes=${ROUTES.length} passed=${passed} failed=${ROUTES.length - passed}`);
const byKind = {};
findings.forEach(f => { byKind[f.kind] = (byKind[f.kind] || 0) + 1; });
console.log('findings by kind: ' + (Object.keys(byKind).length ? JSON.stringify(byKind) : 'none'));
process.exit(findings.length ? 1 : 0);

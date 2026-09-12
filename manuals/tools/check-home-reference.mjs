// Isolated browser QA: only an in-memory public API fixture is used.
// No production API, DB, account, persistent settings or customer data are touched.
import { chromium } from '../../Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(root, 'manuals/artifacts/home-reference');
await mkdir(output, { recursive: true });
const guid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
const categories = ['محصولات پریمیوم', 'هوش مصنوعی', 'بازی پلی‌استیشن', 'بازی‌های شخصی', 'گیفت کارت‌های مجازی', 'خدمات مجازی']
  .map((title, i) => ({ id: guid(i + 1), slug: 'category-' + i, title }));
const products = Array.from({ length: 8 }, (_, i) => ({
  id: guid(i + 20), slug: 'product-' + i, title: 'اشتراک پریمیوم دیجیتال',
  categoryTitle: 'محصولات دیجیتال', basePrice: 1000000, discountPrice: 980000,
  currencyType: 2, deliveryType: 2, availableStock: 10, isUnlimitedStock: true,
  reviewCount: i < 4 ? 1 : 0, averageRating: 4
}));
const settings = Object.entries({
  HeroTitle: 'زندگی دیجیتالتو پیشرفت بده حمال!', HeroSubtitle: 'لورم ایپسوم دولور سیت امت.',
  HomeFeaturesJson: JSON.stringify(['پشتیبانی‌های', 'قیمت مناسب', 'پرایمر و تنوع', 'سرعت خرید بالا'].map(title => ({ title, text: 'لورم ایپسوم متن ساختگی برای تست', icon: 'circle' }))),
  InstagramUrl: 'https://www.instagram.com/', TelegramUrl: 'https://t.me/',
  SupportEmail: 'support@example.test', SupportPhone: '+98 900 0000000',
  CopyrightText: '© 2026 vitorize - All rights reserved.', SplashEnabled: 'false',
}).map(([key, value]) => ({ key, value }));
const data = {
  categories, featuredProducts: products, banners: [],
  brands: ['Claude', 'XBOX', 'VISA', 'Spotify', 'PlayStation', 'OpenAI'].map((title, i) => ({ id: guid(i + 40), slug: 'brand-' + i, title })),
  latestBlogPosts: Array.from({ length: 3 }, (_, i) => ({ id: guid(i + 60), slug: 'post-' + i, title: 'مطلب آزمایشی ' + i })),
  faqs: Array.from({ length: 4 }, (_, i) => ({
    id: guid(i + 80), question: 'متن سوال پرتکرار شما چیست؟',
    answer: 'این متن صرفاً دادهٔ آزمایشی مرورگر است و در فروشگاه نمایش داده نمی‌شود. پاسخ سوال دربارهٔ محصولات دیجیتال و نحوهٔ خرید در این قسمت قرار می‌گیرد. '.repeat(3)
  }))
};
let mode = 'normal';
const calls = [];
const api = createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1:5188');
  calls.push(url.pathname + url.search);
  if (url.pathname === '/media/test.svg') {
    res.writeHead(200, { 'content-type': 'image/svg+xml' });
    res.end('<svg xmlns="http://www.w3.org/2000/svg" width="400" height="300"><rect width="400" height="300" fill="#2cc3b3"/><text x="30" y="150" font-size="32">API MEDIA FIXTURE</text></svg>');
    return;
  }
  const mediaProducts = products.map((product, i) => ({ ...product, thumbnailImagePath: '/media/test.svg?product=' + i, forceOutOfStock: i === 0, redirectUrl: i === 1 ? '/product/redirect-target' : null }));
  let result = [];
  if (url.pathname === '/api/settings/public') result = settings;
  else if (url.pathname === '/api/storefront/home') result = mode === 'empty' ? {} : mode === 'media' ? {
    ...data, featuredProducts: mediaProducts,
    banners: Array.from({ length: 6 }, (_, i) => ({ id: guid(100 + i), title: 'بنر آزمایشی ' + i, imagePath: '/media/test.svg?desktop=' + i, mobileImagePath: '/media/test.svg?mobile=' + i, position: i < 4 ? 'home-hero' : 'home-secondary', sortOrder: i, linkUrl: '/shop?banner=' + i }))
  } : data;
  else if (url.pathname === '/api/products/categories') result = categories;
  else if (url.pathname === '/api/products') result = { items: mode === 'empty' ? [] : products, page: 1, pageSize: 8, totalCount: 8 };
  else if (url.pathname === '/api/cart') result = { items: [], totalQuantity: 0 };
  else if (url.pathname.startsWith('/api/product-reviews/product/')) {
    const id = url.pathname.split('/').at(-1);
    result = { reviews: { items: [
      { id, productId: id, userDisplayName: 'کاربر آزمایشی', comment: 'این نظر فقط در تست محلی استفاده می‌شود و محتوای فروشگاه نیست.', rating: 4, isBuyer: true, isApproved: true, isRejected: false, createdAt: '2026-09-01T00:00:00Z' },
      { id: guid(999), comment: 'MUST_NOT_RENDER_PENDING_REVIEW', isApproved: false, rating: 1 }
    ], totalCount: 2 } };
  }
  const failed = mode === 'failure' && ['/api/storefront/home', '/api/products'].includes(url.pathname);
  res.writeHead(failed ? 503 : 200, { 'content-type': 'application/json' });
  res.end(JSON.stringify({ isSuccess: !failed, data: result, message: failed ? 'Fixture unavailable' : '' }));
});
await new Promise(resolve => api.listen(5188, '127.0.0.1', resolve));
const app = spawn('dotnet', [
  'bin/Debug/net8.0/Vitorize.Web.dll', '--urls=http://127.0.0.1:5088',
  '--ApiSettings:BaseUrl=http://127.0.0.1:5188/api/', '--ApiSettings:MediaBaseUrl=http://127.0.0.1:5188/',
], { cwd: resolve(root, 'Vitorize/Vitorize.Web'), env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
let logs = '';
app.stdout.on('data', b => { logs += b; });
app.stderr.on('data', b => { logs += b; });
let browser;
try {
  for (let i = 0; i < 120; i++) {
    try { if ((await fetch('http://127.0.0.1:5088/css/home.css')).ok) break; } catch {}
    if (app.exitCode !== null) throw Error('Web process exited before startup');
    await new Promise(r => setTimeout(r, 250));
  }
  browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 1728, height: 1000 }, colorScheme: 'light' });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  const report = { source: 'Isolated public API fixture; not a live-database verification or pixel-identical content comparison.', viewports: [], errors, apiCalls: calls };
  for (const width of [1728, 402, 390, 360, 768, 1280]) {
    await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
    await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
    await page.locator('.hp-product').first().waitFor();
    await page.waitForFunction(() => !document.querySelector('.vz-splash:not(.is-done)'));
    assert.equal(await page.locator('text=MUST_NOT_RENDER_PENDING_REVIEW').count(), 0);
    assert.equal(await page.locator('.hp-product:visible').count(), width > 767 ? 8 : 4);
    assert.equal(await page.locator('.hp-faq details[open]').count(), 1);
    const geometry = await page.evaluate(() => ({
      width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
      height: document.documentElement.scrollHeight,
      sections: [...document.querySelectorAll('.hp-section, .hp-footer')].map(el => ({
        name: el.className, top: Math.round(el.getBoundingClientRect().top + scrollY), height: Math.round(el.getBoundingClientRect().height)
      }))
    }));
    assert.equal(geometry.overflow, false, 'horizontal overflow at ' + width);
    report.viewports.push(geometry);
    if (width === 1728) {
      assert.deepEqual(geometry.sections.map(s => s.top), [1000,1486,2733,3844,4388,4605,5284,5942,6621,7687]);
      assert.equal(geometry.height, 8573);
    }
    if (width === 402) {
      assert.deepEqual(geometry.sections.map(s => s.top), [682,1052,1684,1966,2398,2462,2732,2914,3150,3628]);
      assert.equal(geometry.height, 4265);
    }
    if ([1728, 402].includes(width)) {
      await page.screenshot({ path: resolve(output, width === 1728 ? 'desktop.png' : 'mobile.png'), fullPage: true });
      const toggle = width === 1728 ? page.locator('.hp-nav button') : page.locator('.hp-menu-toggle');
      await toggle.click();
      await page.locator('.hp-focus-panel').waitFor();
      await page.locator('.hp-focus-panel a').last().focus();
      await page.keyboard.press('Tab');
      assert.equal(await page.locator('.hp-header').evaluate(el => el.contains(document.activeElement)), true);
      if (width === 1728) await page.screenshot({ path: resolve(output, 'desktop-focus.png'), fullPage: true });
      await page.keyboard.press('Escape');
      await page.waitForFunction(() => !document.querySelector('.hp-focus-backdrop'));
      await page.locator('.hp-faq details').first().locator('summary').click();
      assert.equal(await page.locator('.hp-faq details[open]').count(), 2);
    }
  }
  // Data failure and empty data are explicit and do not create fake commercial content.
  mode = 'failure';
  await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
  await page.locator('.hp-data-notice').waitFor();
  mode = 'normal';
  await page.locator('.hp-data-notice button').click();
  await page.waitForFunction(() => !document.querySelector('.hp-data-notice'));
  assert.equal(await page.locator('.hp-product').count(), 8);
  mode = 'empty';
  await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
  assert.equal(await page.locator('.hp-product').count(), 0);
  assert.equal(await page.locator('.hp-review').count(), 0);
  assert.equal(await page.locator('.hp-data-notice').count(), 0);
  mode = 'normal';
  await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
  await page.locator('.hp-nav a[href="/about"]').click();
  await page.waitForURL('**/about');
  await page.locator('.home-shell').waitFor({ state: 'detached' });
  assert.equal(await page.locator('.home-shell, .hp-header, .hp-footer').count(), 0);
  await page.goBack({ waitUntil: 'networkidle' });
  await page.locator('.home-shell').waitFor();
  mode = 'media';
  await page.setViewportSize({ width: 402, height: 874 });
  await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
  const heroImage = page.locator('.hp-hero__mosaic img').first();
  assert.match(await heroImage.evaluate(el => el.currentSrc), /mobile=0/);
  assert.equal(await heroImage.evaluate(el => el.complete && el.naturalWidth > 0), true);
  assert.equal(await page.locator('.hp-product').first().locator('.hp-product__stock').textContent(), 'ناموجود');
  assert.equal(await page.locator('.hp-product').nth(1).getAttribute('href'), '/product/redirect-target');
  await page.locator('.hp-banner button').nth(1).click();
  await page.waitForFunction(() => document.querySelector('.hp-banner > a')?.getAttribute('href') === '/shop?banner=5');
  assert.equal(errors.length, 0, errors.join('\n'));
  await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ viewports: report.viewports, errors, requestCount: calls.length }, null, 2));
} catch (error) {
  await writeFile(resolve(output, 'failure.log'), String(error) + '\n' + logs);
  throw error;
} finally {
  await browser?.close();
  app.kill();
  api.close();
}

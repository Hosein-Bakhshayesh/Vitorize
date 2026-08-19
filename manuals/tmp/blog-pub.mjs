import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O = 'http://localhost:5077', PW = 'E2E-Admin-Only-aA1!';
const b = await chromium.launch({ channel: 'chrome' });
const c = await b.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
const p = await c.newPage();
p.on('console', m => { if (m.type() === 'error') console.log('  console error:', m.text().slice(0, 140)); });
await p.goto(`${O}/admin/login`, { waitUntil: 'networkidle' });
await p.locator('input[name="mobile"]').fill('09120000011');
await p.locator('input[name="password"]').fill(PW);
await Promise.all([p.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login')),
                   p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);

await p.goto(`${O}/admin/blog`, { waitUntil: 'networkidle' });
const btn = p.getByTestId('blog-publish-probe-plain');
console.log('publish button present:', await btn.count());
await btn.click();
await p.waitForTimeout(3500);
const toast = await p.locator('.vz-toast').last().innerText().catch(() => '(none)');
console.log('toast:', toast.replace(/\s+/g, ' ').slice(0, 160));
await b.close();

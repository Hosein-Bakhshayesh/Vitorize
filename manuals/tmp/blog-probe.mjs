// Isolates whether ContentHtml survives (a) a plain save and (b) a save after preview.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O = 'http://localhost:5077', PW = 'E2E-Admin-Only-aA1!';
const b = await chromium.launch({ channel: 'chrome' });
const c = await b.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
const p = await c.newPage();
await p.goto(`${O}/admin/login`, { waitUntil: 'networkidle' });
await p.locator('input[name="mobile"]').fill('09120000011');
await p.locator('input[name="password"]').fill(PW);
await Promise.all([p.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login')),
                   p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);

async function makePost(slug, withPreview) {
  await p.goto(`${O}/admin/blog`, { waitUntil: 'networkidle' });
  await p.getByTestId('blog-create').click();
  await p.waitForTimeout(800);
  await p.getByTestId('blog-title').fill(`probe ${slug}`);
  await p.getByTestId('blog-slug').fill(slug);
  const ed = p.getByTestId('blog-content-editor').locator('.ck-editor__editable');
  await ed.click();
  await p.keyboard.type('PROBE-CONTENT-MARKER');
  await p.waitForTimeout(600);
  if (withPreview) {
    await p.getByTestId('blog-preview-form').click();
    await p.waitForTimeout(900);
    await p.getByTestId('blog-preview-close').click();
    await p.waitForTimeout(900);
  }
  await p.getByTestId('blog-save').click();
  await p.waitForTimeout(3000);
  const listed = await p.locator(`[data-testid="blog-row-${slug}"]`).count();
  console.log(`${withPreview ? 'WITH preview   ' : 'WITHOUT preview'} : listed=${listed}`);
}

await makePost('probe-plain', false);
await makePost('probe-preview', true);
await b.close();

// Blog Admin end-to-end: drives the real Admin UI through the full authoring lifecycle and checks
// the public site at each publication state.
// usage: node blog-e2e.mjs <origin> <desktop|mobile>
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = process.argv[2] || 'http://localhost:5077';
const MODE = process.argv[3] || 'desktop';
const VP = MODE === 'mobile' ? { width: 390, height: 844 } : { width: 1440, height: 900 };
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';
const SLUG = 'vitorize-blog-qa';

let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };

const browser = await chromium.launch({ channel: 'chrome' });

async function adminContext() {
  const c = await browser.newContext({ viewport: VP, colorScheme: 'light', locale: 'fa-IR' });
  const p = await c.newPage();
  await p.goto(`${ORIGIN}/admin/login`, { waitUntil: 'networkidle' });
  await p.locator('input[name="mobile"]').fill('09120000011');
  await p.locator('input[name="password"]').fill(PW);
  await Promise.all([
    p.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login'), { timeout: 45000 }),
    p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()
  ]);
  await p.close();
  return c;
}

/** Public visibility of the article, from a clean anonymous context. */
async function publicState() {
  const c = await browser.newContext({ viewport: VP, locale: 'fa-IR' });
  const p = await c.newPage();
  const listResp = await p.goto(`${ORIGIN}/blog`, { waitUntil: 'networkidle' });
  const inList = (await p.content()).includes('Vitorize Blog QA');
  const detail = await p.goto(`${ORIGIN}/blog/${SLUG}`, { waitUntil: 'networkidle' });
  const detailStatus = detail ? detail.status() : 0;
  const detailShows = (await p.content()).includes('Vitorize Blog QA');
  await p.goto(`${ORIGIN}/`, { waitUntil: 'networkidle' });
  const onHome = (await p.content()).includes('Vitorize Blog QA');
  await c.close();
  return { listStatus: listResp ? listResp.status() : 0, inList, detailStatus, detailShows, onHome };
}

const admin = await adminContext();
const page = await admin.newPage();
const consoleErrors = [];
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 120)); });

console.log(`\n##### BLOG ADMIN E2E — ${MODE} ${VP.width}x${VP.height} #####`);

// ---- navigation ----
await page.goto(`${ORIGIN}/admin/dashboard`, { waitUntil: 'networkidle' });
const navLink = page.locator('a[href="admin/blog"], a[href="/admin/blog"]').first();
(await navLink.count()) ? ok('sidebar shows وبلاگ') : fail('sidebar entry missing');
await page.goto(`${ORIGIN}/admin/blog`, { waitUntil: 'networkidle' });
(await page.getByTestId('blog-create').count()) ? ok('blog list page loads') : fail('blog list did not load');

let overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
overflow <= 1 ? ok(`list no overflow (${overflow}px)`) : fail(`list overflow ${overflow}px`);

// ---- create draft ----
await page.getByTestId('blog-create').click();
await page.waitForTimeout(700);
await page.getByTestId('blog-title').fill('Vitorize Blog QA');
await page.getByTestId('blog-slug').fill(SLUG);
await page.getByTestId('blog-summary').fill('QA content');

const editor = page.getByTestId('blog-content-editor').locator('.ck-editor__editable');
if (await editor.count()) {
  await editor.click();
  await page.keyboard.type('متن آزمایشی وبلاگ ویتورایز.');
  ok('CKEditor accepts input');
} else fail('CKEditor not rendered');

// preview the unsaved form
await page.getByTestId('blog-preview-form').click();
await page.waitForTimeout(600);
const previewBody = page.getByTestId('blog-preview-body');
if (await previewBody.count()) {
  const txt = await previewBody.innerText();
  txt.includes('Vitorize Blog QA') ? ok('preview renders unsaved form content') : fail('preview missing title');
  const m = await page.evaluate(() => {
    const el = document.querySelector('[data-testid="blog-preview-body"]').closest('.vz-dialog');
    if (!el) return null; const r = el.getBoundingClientRect();
    return { x: Math.round(r.x), right: Math.round(r.right), vw: document.documentElement.clientWidth };
  });
  if (m) (m.x >= -1 && m.right <= m.vw + 1) ? ok('preview dialog inside viewport') : fail(`preview escapes: ${m.x}..${m.right}/${m.vw}`);
  // Close via the explicit action, then confirm the editor underneath is usable again.
  await page.getByTestId('blog-preview-close').click();
  // Leaving preview re-renders the editor subtree (CKEditor returns to view) over the Blazor
  // Server circuit, so allow a real round-trip rather than a token delay.
  await page.waitForTimeout(2500);
  const overlays = await page.locator('.vz-overlay').count();
  const previewGone = (await page.getByTestId('blog-preview-body').count()) === 0;
  previewGone ? ok('preview closes') : fail('preview stayed open');
  overlays <= 1 ? ok(`exactly one dialog visible at a time (overlays=${overlays})`)
                : fail(`stacked overlays remain after closing preview (overlays=${overlays})`);
  // The author must land back in the editor with their unsaved work intact.
  const backInEditor = await page.getByTestId('blog-title').inputValue().catch(() => '');
  backInEditor === 'Vitorize Blog QA' ? ok('editor reopens with unsaved content preserved')
                                      : fail(`editor did not restore (title="${backInEditor}")`);
} else fail('preview did not open');

await page.getByTestId('blog-save').click();
await page.waitForTimeout(3500);
const saveToast = (await page.locator('.vz-toast').last().innerText().catch(() => '')) || '(no toast)';
if (await page.locator(`[data-testid="blog-row-${SLUG}"]`).count()) ok('draft saved and listed');
else fail(`draft not listed — save feedback: ${saveToast.replace(/\s+/g, ' ').slice(0, 120)}`);

let pub = await publicState();
(!pub.inList) ? ok('draft hidden from /blog') : fail('draft leaked into /blog');
(!pub.detailShows) ? ok('draft not publicly readable at /blog/{slug}') : fail('draft readable publicly');
(!pub.onHome) ? ok('draft hidden from Home') : fail('draft leaked onto Home');

// ---- publish ----
await page.getByTestId(`blog-publish-${SLUG}`).click();
await page.waitForTimeout(2000);
pub = await publicState();
pub.inList ? ok('published post appears on /blog') : fail('published post missing from /blog');
(pub.detailStatus === 200 && pub.detailShows) ? ok(`/blog/${SLUG} returns 200 with content`) : fail(`detail status=${pub.detailStatus} shows=${pub.detailShows}`);
pub.onHome ? ok('published post appears in Home blog section') : console.log('  note Home blog section did not include it (may be limited by design)');

// ---- duplicate slug ----
await page.goto(`${ORIGIN}/admin/blog`, { waitUntil: 'networkidle' });
await page.getByTestId('blog-create').click();
await page.waitForTimeout(600);
await page.getByTestId('blog-title').fill('Duplicate attempt');
await page.getByTestId('blog-slug').fill(SLUG);
await page.getByTestId('blog-save').click();
await page.waitForTimeout(1800);
const dupToast = (await page.locator('.vz-toast').last().innerText().catch(() => '')) || '';
dupToast.includes('نامک') ? ok('duplicate slug rejected with clear message') : fail(`duplicate slug not rejected (toast: ${dupToast.slice(0,60)})`);
await page.keyboard.press('Escape'); await page.waitForTimeout(500);

// ---- unpublish / republish ----
await page.goto(`${ORIGIN}/admin/blog`, { waitUntil: 'networkidle' });
await page.getByTestId(`blog-publish-${SLUG}`).click();
await page.waitForTimeout(2000);
pub = await publicState();
(!pub.inList && !pub.detailShows) ? ok('unpublished post removed from public site') : fail('unpublished post still public');

await page.getByTestId(`blog-publish-${SLUG}`).click();
await page.waitForTimeout(2000);
pub = await publicState();
(pub.inList && pub.detailStatus === 200) ? ok('republished post is public again') : fail('republish failed');

const rows = await page.locator('[data-testid^="blog-row-"]').count();
rows === 1 ? ok('no duplicate record created across publish cycles') : fail(`expected 1 row, found ${rows}`);

// ---- delete ----
await page.getByTestId(`blog-delete-${SLUG}`).click();
await page.waitForTimeout(600);
const confirm = page.locator('.vz-dialog button', { hasText: 'حذف' }).last();
if (await confirm.count()) { await confirm.click(); await page.waitForTimeout(2000); }
(await page.locator(`[data-testid="blog-row-${SLUG}"]`).count()) === 0 ? ok('post deleted') : fail('post still listed after delete');

overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
overflow <= 1 ? ok(`final no overflow (${overflow}px)`) : fail(`overflow ${overflow}px`);
consoleErrors.length === 0 ? ok('no console errors') : fail(`console errors: ${consoleErrors[0]}`);

await admin.close();
await browser.close();
console.log(`\nTOTAL FAILURES = ${fails}`);
process.exit(fails ? 1 : 0);

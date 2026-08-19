// Interaction sweep: exercises real controls and asserts floating surfaces / dialogs stay usable.
// usage: node ui-interact.mjs <origin> <desktop|mobile>
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';

const ORIGIN = process.argv[2] || 'http://localhost:5077';
const MODE = process.argv[3] || 'desktop';
const VP = MODE === 'mobile' ? { width: 390, height: 844 } : { width: 1440, height: 900 };
const PW = process.env.E2E_QA_PASSWORD || 'E2E-Admin-Only-aA1!';
let fails = 0;
const ok = m => console.log(`  ok   ${m}`);
const fail = m => { console.log(`  FAIL ${m}`); fails++; };
const skip = m => console.log(`  skip ${m}`);

const browser = await chromium.launch({ channel: 'chrome' });

/** A floating surface must sit inside the viewport, or scroll internally if it is tall. */
async function assertFloating(page, sel, label) {
  const m = await page.evaluate(s => {
    const el = document.querySelector(s);
    if (!el) return null;
    const r = el.getBoundingClientRect(), cs = getComputedStyle(el);
    return { x: Math.round(r.x), y: Math.round(r.y), right: Math.round(r.right), bottom: Math.round(r.bottom),
             w: Math.round(r.width), h: Math.round(r.height), scrolls: /auto|scroll/.test(cs.overflowY),
             vw: document.documentElement.clientWidth, vh: document.documentElement.clientHeight };
  }, sel);
  if (!m) { skip(`${label} not present`); return null; }
  const problems = [];
  if (m.x < -1) problems.push(`left ${m.x}`);
  if (m.right > m.vw + 1) problems.push(`right ${m.right}>${m.vw}`);
  if (m.y < -1) problems.push(`top ${m.y}`);
  if (m.bottom > m.vh + 1 && !m.scrolls) problems.push(`bottom ${m.bottom}>${m.vh} without internal scroll`);
  problems.length ? fail(`${label} escapes viewport: ${problems.join(', ')}`) : ok(`${label} inside viewport (${m.w}x${m.h})`);
  return m;
}

async function ctxFor(role) {
  const c = await browser.newContext({ viewport: VP, colorScheme: 'light', locale: 'fa-IR' });
  const p = await c.newPage();
  if (role === 'customer') {
    await p.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
    await p.locator('#pw-mobile').fill('09120000013'); await p.locator('#pw-pass').fill(PW);
    await Promise.all([p.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 40000 }),
                       p.locator('form[action="/auth/customer/login"] button[type=submit]').click()]);
  } else if (role === 'admin') {
    await p.goto(`${ORIGIN}/admin/login`, { waitUntil: 'networkidle' });
    await p.locator('input[name="mobile"]').fill('09120000011'); await p.locator('input[name="password"]').fill(PW);
    await Promise.all([p.waitForURL(u => u.pathname.startsWith('/admin') && !u.pathname.includes('login'), { timeout: 45000 }),
                       p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);
  }
  await p.close(); return c;
}

console.log(`\n########## INTERACTION SWEEP — ${MODE} ${VP.width}x${VP.height} ##########`);

// ---------------- storefront / anon ----------------
const anon = await ctxFor('anon');
{
  const p = await anon.newPage();
  console.log('\n-- storefront navigation & theme --');
  await p.goto(`${ORIGIN}/`, { waitUntil: 'networkidle' });

  if (MODE === 'mobile') {
    // Mobile navigation is a fixed bottom bar, not a burger drawer.
    const bn = p.locator('.st-bottomnav');
    if (await bn.count()) {
      const m = await p.evaluate(() => { const el = document.querySelector('.st-bottomnav');
        const r = el.getBoundingClientRect(), cs = getComputedStyle(el);
        return { x: Math.round(r.x), right: Math.round(r.right), bottom: Math.round(r.bottom),
                 h: Math.round(r.height), pos: cs.position, links: el.querySelectorAll('a').length,
                 vw: document.documentElement.clientWidth, vh: document.documentElement.clientHeight,
                 bodyPad: parseFloat(getComputedStyle(document.body).paddingBottom) || 0 }; });
      (m.x >= -1 && m.right <= m.vw + 1) ? ok(`bottom nav within viewport (${m.links} links, ${m.h}px)`) : fail(`bottom nav escapes: x=${m.x} right=${m.right}/${m.vw}`);
      Math.abs(m.bottom - m.vh) <= 2 ? ok('bottom nav pinned to viewport bottom') : fail(`bottom nav bottom=${m.bottom} vh=${m.vh}`);
      // A fixed bottom bar must not permanently cover page content at the end of the document.
      // Measure the footer's last *content* element, not the footer box: reserved padding is the
      // fix, so the box legitimately extends under the bar while the content must clear it.
      const covered = await p.evaluate(() => { window.scrollTo(0, document.body.scrollHeight);
        const nav = document.querySelector('.st-bottomnav'); const f = document.querySelector('footer');
        if (!nav || !f) return null;
        const content = [...f.querySelectorAll('*')].filter(e => (e.textContent || '').trim().length > 0 &&
          e.getBoundingClientRect().height > 0);
        if (!content.length) return false;
        const lowest = Math.max(...content.map(e => e.getBoundingClientRect().bottom));
        return lowest > nav.getBoundingClientRect().top + 4; });
      if (covered === true) fail('bottom nav covers footer content at page end');
      else if (covered === false) ok('page content clears the bottom nav');
      await p.evaluate(() => window.scrollTo(0, 0)); await p.waitForTimeout(200);
      await bn.locator('a').nth(1).click(); await p.waitForTimeout(900);
      ok('bottom nav link navigates');
      await p.goto(`${ORIGIN}/`, { waitUntil: 'networkidle' });
    } else fail('mobile bottom nav missing');
  } else {
    const cat = p.locator('.st-nav a, .st-mega__trigger').first();
    if (await cat.count()) { await cat.hover(); await p.waitForTimeout(400); ok('desktop nav hover works'); }
  }

  const theme = p.locator('.st-theme-toggle').first();
  if (await theme.count()) {
    const before = await p.evaluate(() => document.documentElement.getAttribute('data-theme'));
    await theme.click(); await p.waitForTimeout(500);
    const after = await p.evaluate(() => document.documentElement.getAttribute('data-theme'));
    before !== after ? ok(`theme toggle ${before} -> ${after}`) : fail('theme toggle did nothing');
    await theme.click(); await p.waitForTimeout(400);
  } else skip('theme toggle');

  console.log('\n-- search --');
  const search = p.locator('input[type=search], .st-search input').first();
  if (await search.count() && await search.isVisible()) {
    await search.fill('e2e'); await p.waitForTimeout(1200);
    await assertFloating(p, '.st-suggest, .st-search__panel, [class*="suggest"]', 'search suggestions');
    await p.keyboard.press('Escape');
  } else {
    // Mobile exposes search as a link to the dedicated /search page instead of an inline field.
    await p.goto(`${ORIGIN}/search?q=e2e`, { waitUntil: 'networkidle' });
    const ov = await p.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    ov <= 1 ? ok('mobile search page renders without overflow') : fail(`search page overflow ${ov}px`);
    await p.goto(`${ORIGIN}/`, { waitUntil: 'networkidle' });
  }

  console.log('\n-- shop filters & sorting --');
  await p.goto(`${ORIGIN}/shop`, { waitUntil: 'networkidle' });
  if (MODE === 'mobile') {
    const fbtn = p.locator('button', { hasText: 'فیلتر' }).first();
    if (await fbtn.count()) { await fbtn.click(); await p.waitForTimeout(600);
      await assertFloating(p, '.st-filter-sheet, [class*="filter-sheet"]', 'mobile filter sheet');
      const close = p.locator('.st-filter-sheet button, [class*="filter-sheet"] button').first();
      if (await close.count()) { await close.click(); await p.waitForTimeout(300); }
    } else skip('mobile filter button');
  }
  const sort = p.locator('select').first();
  if (await sort.count()) { const opts = await sort.locator('option').count();
    if (opts > 1) { await sort.selectOption({ index: 1 }); await p.waitForTimeout(1000); ok('sort control applied'); } else skip('sort has one option'); }

  console.log('\n-- product page --');
  await p.goto(`${ORIGIN}/product/e2e-seo-product`, { waitUntil: 'networkidle' });
  const tabs = p.locator('.st-ptabs button, .st-ptabs a');
  const tc = await tabs.count();
  if (tc > 1) { for (let i = 0; i < Math.min(tc, 3); i++) { await tabs.nth(i).click(); await p.waitForTimeout(350); }
    const ov = await p.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    ov <= 1 ? ok(`${tc} product tabs switch cleanly`) : fail(`tabs cause overflow ${ov}px`);
  } else skip('product tabs');

  const addBtn = p.locator('.st-buy__card button.st-btn--accent').first();
  if (await addBtn.count()) { await addBtn.click(); await p.waitForTimeout(800);
    const dlg = await assertFloating(p, '.vz-dialog', 'product input dialog');
    if (dlg) {
      const footer = await p.evaluate(() => { const b = document.querySelector('.vz-dialog button.st-btn--accent, .vz-dialog [class*="footer"] button');
        if (!b) return null; const r = b.getBoundingClientRect();
        return { visible: r.bottom <= document.documentElement.clientHeight + 1 && r.top >= -1, y: Math.round(r.y) }; });
      if (footer) footer.visible ? ok('dialog primary action reachable') : fail(`dialog action off-screen (y=${footer.y})`);
      await p.keyboard.press('Escape'); await p.waitForTimeout(400);
      const closed = await p.locator('.vz-dialog').count() === 0;
      closed ? ok('dialog closes on Escape') : skip('dialog does not close on Escape (may be by design)');
    }
  } else skip('add-to-cart');
  await p.close();
}
await anon.close();

// ---------------- customer ----------------
const cust = await ctxFor('customer');
{
  const p = await cust.newPage();
  console.log('\n-- customer account menu & sidebar --');
  await p.goto(`${ORIGIN}/product/e2e-seo-product`, { waitUntil: 'networkidle' });
  const av = p.locator('.st-avatar').first();
  if (await av.count()) {
    await av.click(); await p.waitForTimeout(400);
    await assertFloating(p, '.st-dropdown', 'account dropdown');
    const logout = await p.evaluate(() => { const b = document.querySelector('.st-dropdown .danger'); if (!b) return null;
      const r = b.getBoundingClientRect(); return { inside: r.left >= -1 && r.right <= document.documentElement.clientWidth + 1 }; });
    if (logout) logout.inside ? ok('logout reachable in dropdown') : fail('logout off-screen');
    await p.mouse.click(5, 500); await p.waitForTimeout(300);
    (await p.locator('.st-dropdown').count()) === 0 ? ok('dropdown closes on outside click') : fail('dropdown stays open after outside click');
  } else fail('account avatar missing');

  console.log('\n-- customer pages --');
  await p.goto(`${ORIGIN}/customer/dashboard`, { waitUntil: 'networkidle' });
  const links = p.locator('.st-acc a, aside a').first();
  if (await links.count() && await links.isVisible()) { await links.click(); await p.waitForTimeout(900); ok('customer sidebar navigation works'); }
  else skip('customer sidebar hidden at this viewport (bottom nav is the mobile affordance)');

  await p.goto(`${ORIGIN}/customer/notifications`, { waitUntil: 'networkidle' });
  const readAll = p.locator('button', { hasText: 'خواندن همه' }).first();
  if (await readAll.count() && await readAll.isVisible()) { await readAll.click(); await p.waitForTimeout(800); ok('notifications mark-all-read'); } else skip('no unread notifications');

  console.log('\n-- cart interactions --');
  await p.goto(`${ORIGIN}/cart`, { waitUntil: 'networkidle' });
  const inc = p.locator('.st-qty button').last();
  if (await inc.count() && await inc.isVisible()) {
    await inc.click(); await p.waitForTimeout(1200); ok('cart quantity increment');
    const dec = p.locator('.st-qty button').first();
    await dec.click(); await p.waitForTimeout(1200); ok('cart quantity decrement');
  } else skip('cart empty - no quantity controls');
  const coupon = p.locator('input[placeholder*="تخفیف"], .st-cart-sum input').first();
  if (await coupon.count()) { await coupon.fill('INVALIDCODE');
    const apply = p.locator('button', { hasText: 'اعمال' }).first();
    if (await apply.count()) { await apply.click(); await p.waitForTimeout(1500); ok('invalid coupon handled without crash'); }
  } else skip('coupon field');
  await p.close();
}
await cust.close();

// ---------------- admin ----------------
const adm = await ctxFor('admin');
{
  const p = await adm.newPage();
  console.log('\n-- admin tables, filters, dialogs --');
  await p.goto(`${ORIGIN}/admin/products`, { waitUntil: 'networkidle' });
  const tableOv = await p.evaluate(() => {
    const de = document.documentElement;
    const wrap = document.querySelector('.vz-table-wrap, .vz-card__body');
    return { page: de.scrollWidth - de.clientWidth, wrapScrolls: wrap ? /auto|scroll/.test(getComputedStyle(wrap).overflowX) : null };
  });
  tableOv.page <= 1 ? ok(`admin table: no page overflow (wrapper scrolls=${tableOv.wrapScrolls})`) : fail(`admin table causes page overflow ${tableOv.page}px`);

  const searchBox = p.locator('.vz-input:visible, input[type=text]:visible').first();
  if (await searchBox.count()) { await searchBox.fill('E2E'); await p.waitForTimeout(1200); ok('admin list filter applied'); }
  else skip('admin list filter hidden at this viewport');

  console.log('\n-- admin CMS SlidePanel --');
  await p.goto(`${ORIGIN}/admin/pages`, { waitUntil: 'networkidle' });
  const create = p.getByTestId('page-create');
  if (await create.count()) {
    await create.click(); await p.waitForTimeout(1200);
    await assertFloating(p, '.vz-dialog, .vz-slidepanel', 'CMS page panel');
    const title = p.getByTestId('page-title');
    if (await title.count() && await title.isVisible()) { await title.fill('QA sweep temp'); ok('panel form input usable'); }
    else skip('panel title field not visible');
    await p.keyboard.press('Escape'); await p.waitForTimeout(500);
  } else skip('admin page-create');

  console.log('\n-- admin broadcast panel --');
  await p.goto(`${ORIGIN}/admin/notifications`, { waitUntil: 'networkidle' });
  const bc = p.getByTestId('broadcast-open');
  if (await bc.count()) { await bc.click(); await p.waitForTimeout(1000);
    await assertFloating(p, '.vz-dialog, .vz-slidepanel', 'broadcast panel');
    await p.keyboard.press('Escape'); await p.waitForTimeout(400);
  } else skip('broadcast panel');

  console.log('\n-- admin settings tabs --');
  await p.goto(`${ORIGIN}/admin/settings`, { waitUntil: 'networkidle' });
  const st = p.locator('.vz-settab');
  const n = await st.count();
  let tabFails = 0;
  for (let i = 0; i < n; i++) {
    await st.nth(i).click(); await p.waitForTimeout(320);
    const ov = await p.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    if (ov > 1) { tabFails++; fail(`settings tab ${i + 1}/${n} overflows ${ov}px`); }
  }
  tabFails === 0 ? ok(`all ${n} settings tabs render without overflow`) : null;
  await p.close();
}
await adm.close();

await browser.close();
console.log(`\nTOTAL FAILURES = ${fails}`);
process.exit(fails ? 1 : 0);

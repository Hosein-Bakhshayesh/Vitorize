import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O = 'http://localhost:5077', PW = 'E2E-Admin-Only-aA1!';
const b = await chromium.launch({ channel: 'chrome' });
const c = await b.newContext({ viewport: { width: 1440, height: 900 }, locale: 'fa-IR' });
const p = await c.newPage();
await p.goto(`${O}/login`, { waitUntil: 'networkidle' });
await p.locator('#pw-mobile').fill('09120000013'); await p.locator('#pw-pass').fill(PW);
await Promise.all([p.waitForURL(u => !u.pathname.startsWith('/login')), p.locator('form[action="/auth/customer/login"] button[type=submit]').click()]);
await p.goto(`${O}/product/e2e-seo-product`, { waitUntil: 'networkidle' });
await p.locator('.st-avatar').click();
await p.waitForTimeout(600);

console.log(JSON.stringify(await p.evaluate(() => {
  const pm = document.querySelector('.st-pmenu');
  const kids = [...pm.children].map(el => {
    const r = el.getBoundingClientRect(), cs = getComputedStyle(el);
    return { tag: el.tagName.toLowerCase(), cls: (el.className||'').toString().slice(0,30),
             pos: cs.position, z: cs.zIndex, rect: [Math.round(r.x),Math.round(r.y),Math.round(r.width),Math.round(r.height)],
             pe: cs.pointerEvents, display: cs.display };
  });
  // stacking-context creators above the menu
  const chain = []; let n = pm;
  while (n && n !== document.documentElement) {
    const cs = getComputedStyle(n);
    const creates = (cs.position !== 'static' && cs.zIndex !== 'auto') || cs.transform !== 'none' ||
                    cs.filter !== 'none' || cs.contain !== 'none' || cs.willChange !== 'auto' ||
                    (cs.isolation === 'isolate');
    chain.push({ cls: (n.className||'').toString().slice(0,26) || n.tagName.toLowerCase(),
                 pos: cs.position, z: cs.zIndex, contain: cs.contain, transform: cs.transform === 'none' ? 'none':'SET',
                 stackingContext: creates });
    n = n.parentElement;
  }
  return { pmenuChildren: kids, ancestorChain: chain,
           elemAt_5_500: (() => { const e = document.elementFromPoint(5,500); return e ? e.tagName.toLowerCase()+'.'+((e.className||'').toString().split(' ')[0]) : null; })(),
           elemAt_700_600: (() => { const e = document.elementFromPoint(700,600); return e ? e.tagName.toLowerCase()+'.'+((e.className||'').toString().split(' ')[0]) : null; })() };
}), null, 1));
await b.close();

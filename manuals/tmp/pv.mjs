import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O='http://localhost:5077', PW='E2E-Admin-Only-aA1!';
const b=await chromium.launch({channel:'chrome'});
const p=await (await b.newContext({viewport:{width:1440,height:900},locale:'fa-IR'})).newPage();
await p.goto(`${O}/admin/login`,{waitUntil:'networkidle'});
await p.locator('input[name="mobile"]').fill('09120000011');
await p.locator('input[name="password"]').fill(PW);
await Promise.all([p.waitForURL(u=>u.pathname.startsWith('/admin')&&!u.pathname.includes('login')),p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);
await p.goto(`${O}/admin/blog`,{waitUntil:'networkidle'});
await p.getByTestId('blog-create').click(); await p.waitForTimeout(800);
await p.getByTestId('blog-title').fill('pv probe');
const ed=p.getByTestId('blog-content-editor').locator('.ck-editor__editable');
await ed.click(); await p.keyboard.type('MARKER-XYZ'); await p.waitForTimeout(600);
const pf=await p.evaluate(()=>{const b=document.querySelector('[data-testid="blog-preview-form"]');return b?b.outerHTML.slice(0,200):'MISSING';});
console.log('preview-form btn:',pf);
await p.evaluate(()=>document.querySelector('[data-testid="blog-preview-form"]').click());
await p.waitForTimeout(1500);
console.log('JS-click result: closeVisible=',await p.getByTestId('blog-preview-close').isVisible().catch(()=>'err'));
await p.getByTestId('blog-preview-form').click({force:true}); await p.waitForTimeout(1500);

console.log('after open click: closeVisible=',await p.getByTestId('blog-preview-close').isVisible().catch(()=>'err'),
' previewBody=',await p.getByTestId('blog-preview-body').count());

console.log('preview open:', await p.getByTestId('blog-preview-body').count());
const info = await p.evaluate(()=>{const btns=[...document.querySelectorAll('[data-testid="blog-preview-close"]')];
  return btns.map(x=>({type:x.getAttribute('type'),form:x.getAttribute('form'),
    blazorEvt:[...x.attributes].filter(a=>a.name.includes('blazor')||a.name.startsWith('_bl')).map(a=>a.name).join(','),
    outer:x.outerHTML.slice(0,180)}));});
console.log('close-btn:', JSON.stringify(info,null,1));
await p.getByTestId('blog-preview-close').click(); await p.waitForTimeout(1500);
console.log('after click: previewBody=', await p.getByTestId('blog-preview-body').count(),
  'formVisible=', await p.getByTestId('blog-title').isVisible());
const errs=await p.evaluate(()=>window.__err||'none');
console.log('js err:', errs);
await b.close();

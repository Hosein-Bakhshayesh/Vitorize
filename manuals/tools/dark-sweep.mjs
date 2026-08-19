// Dark-theme representative sweep: renders each route with prefers-color-scheme dark and checks
// the page actually turns dark, plus low-contrast (invisible text) heuristics and the usual
// broken-image / overflow / console assertions.
import { chromium } from 'file:///D:/Vitorize/Vitorize/tests/Vitorize.E2E/node_modules/playwright/index.mjs';
const O=process.argv[2]||'http://localhost:5077';
const MODE=process.argv[3]||'desktop';
const VP=MODE==='mobile'?{width:390,height:844}:{width:1440,height:900};
const PW='E2E-Admin-Only-aA1!';
const ROUTES=[
 ['anon','/'],['anon','/shop'],['anon','/product/e2e-seo-product'],['anon','/cart'],['anon','/login'],
 ['anon','/blog'],['anon','/blog/sweep-blog-post'],
 ['customer','/checkout'],['customer','/customer/dashboard'],['customer','/customer/orders'],['customer','/customer/verification'],
 ['admin','/admin/dashboard'],['admin','/admin/products'],['admin','/admin/products/31000000-0000-0000-0000-000000000002'],
 ['admin','/admin/orders'],['admin','/admin/settings'],['admin','/admin/blog']];
const b=await chromium.launch({channel:'chrome'});
async function ctx(role){
 const c=await b.newContext({viewport:VP,colorScheme:'dark',locale:'fa-IR'});
 const p=await c.newPage();
 if(role==='customer'){await p.goto(`${O}/login`,{waitUntil:'networkidle'});await p.locator('#pw-mobile').fill('09120000013');await p.locator('#pw-pass').fill(PW);
  await Promise.all([p.waitForURL(u=>!u.pathname.startsWith('/login')),p.locator('form[action="/auth/customer/login"] button[type=submit]').click()]);}
 else if(role==='admin'){await p.goto(`${O}/admin/login`,{waitUntil:'networkidle'});await p.locator('input[name="mobile"]').fill('09120000011');await p.locator('input[name="password"]').fill(PW);
  await Promise.all([p.waitForURL(u=>u.pathname.startsWith('/admin')&&!u.pathname.includes('login')),p.locator('form[action="/admin/auth/login"] button[type="submit"]').click()]);}
 await p.close();return c;}
const C={anon:await ctx('anon'),customer:await ctx('customer'),admin:await ctx('admin')};
let fails=0;
for(const [role,route] of ROUTES){
 const p=await C[role].newPage(); const errs=[];
 p.on('console',m=>{if(m.type()==='error')errs.push(m.text().slice(0,100));});
 try{await p.goto(`${O}${route}`,{waitUntil:'networkidle',timeout:60000});}catch(e){console.log(`  FAIL ${route} nav: ${String(e).slice(0,80)}`);fails++;await p.close();continue;}
 await p.waitForTimeout(600);
 const d=await p.evaluate(()=>{
  const lum=c=>{const m=c.match(/\d+/g);if(!m)return 1;const[r,g,bl]=m.map(Number);return(0.299*r+0.587*g+0.114*bl)/255;};
  const theme=document.documentElement.getAttribute('data-theme');
  const bg=getComputedStyle(document.body).backgroundColor;
  // effective page ground: body or a full-size wrapper
  const shell=document.querySelector('.st-shell,.vz-shell,main')||document.body;
  const shellBg=getComputedStyle(shell).backgroundColor;
  const pageLum=Math.min(lum(bg),lum(shellBg));
  // invisible-text heuristic: visible text whose color ~ equals its own background
  let invisible=0;
  for(const el of document.querySelectorAll('p,span,a,td,th,label,h1,h2,h3,button')){
    const r=el.getBoundingClientRect(); if(r.width<4||r.height<4)continue;
    if(!(el.textContent||'').trim())continue;
    const cs=getComputedStyle(el);
    const ebg=cs.backgroundColor;
    if(ebg==='rgba(0, 0, 0, 0)')continue;
    // translucent backgrounds render as the page ground, not their nominal hue
    const am=ebg.match(/rgba\([^)]*,\s*([\d.]+)\)/); if(am&&parseFloat(am[1])<0.5)continue;
    if(Math.abs(lum(cs.color)-lum(ebg))<0.05)invisible++;
    if(invisible>3)break;
  }
  const broken=[...document.images].filter(i=>{const r=i.getBoundingClientRect();return r.width>0&&r.height>0&&!(i.complete&&i.naturalWidth>0);}).length;
  const overflow=document.documentElement.scrollWidth-document.documentElement.clientWidth;
  return{theme,pageLum:pageLum.toFixed(2),invisible,broken,overflow};});
 const bad=[];
 if(d.theme!=='dark')bad.push(`theme=${d.theme}`);
 if(parseFloat(d.pageLum)>0.5)bad.push(`page not dark lum=${d.pageLum}`);
 if(d.invisible>3)bad.push(`invisible-text x${d.invisible}`);
 if(d.broken)bad.push(`broken imgs ${d.broken}`);
 if(d.overflow>1)bad.push(`overflow ${d.overflow}px`);
 if(errs.length)bad.push(`console: ${errs[0]}`);
 if(bad.length){console.log(`  FAIL ${route}: ${bad.join(' | ')}`);fails++;}
 else console.log(`  ok   ${route} (lum=${d.pageLum})`);
 await p.close();}
for(const c of Object.values(C))await c.close();
await b.close();
console.log(`DARK ${MODE}: fails=${fails}`);process.exit(fails?1:0);

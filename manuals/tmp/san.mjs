// Sanitization: unsafe HTML posted through the admin API must be neutralised on save.
const API='http://127.0.0.1:5177/api';
const login=await fetch(`${API}/auth/login`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mobile:'09120000011',password:'E2E-Admin-Only-aA1!'})});
const token=(await login.json()).data.accessToken;
const H={'Content-Type':'application/json',Authorization:`Bearer ${token}`};
const create=await fetch(`${API}/admin/blog`,{method:'POST',headers:H,body:JSON.stringify({
  title:'san probe',slug:'san-probe-xss',summary:'s',
  contentHtml:'<h2>ok</h2><p onclick="window.x=1">t</p><script>window.x=1</scr'+'ipt><a href="javascript:alert(1)">bad</a><ul><li>fine</li></ul>',
  isPublished:true})});
const body=await create.json();
if(!create.ok){console.log('create failed',create.status,JSON.stringify(body).slice(0,200));process.exit(1);}
const html=body.data.contentHtml;
console.log('script tag   :', html.includes('<script')?'PRESENT(FAIL)':'stripped');
console.log('onclick      :', html.includes('onclick')?'PRESENT(FAIL)':'stripped');
console.log('javascript:  :', html.includes('javascript:')?'PRESENT(FAIL)':'stripped');
console.log('safe h2/ul   :', html.includes('<h2>')&&html.includes('<li>')?'preserved':'LOST(FAIL)');
// public read path
const pub=await fetch(`${API}/blog/san-probe-xss`); const pd=await pub.json();
const ph=pd.data?.contentHtml||'';
console.log('public script:', ph.includes('<script')||ph.includes('onclick')||ph.includes('javascript:')?'PRESENT(FAIL)':'clean');
await fetch(`${API}/admin/blog/${body.data.id}`,{method:'DELETE',headers:H});
console.log('cleaned up');

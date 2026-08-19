const API='http://127.0.0.1:5177/api';
async function tok(m){const r=await fetch(`${API}/auth/login`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mobile:m,password:'E2E-Admin-Only-aA1!'})});return (await r.json()).data.accessToken;}
const cust=await tok('09120000013');
const r1=await fetch(`${API}/admin/blog`,{headers:{Authorization:`Bearer ${cust}`}});
const r2=await fetch(`${API}/admin/blog`);
console.log('customer -> admin/blog:', r1.status, r1.status===403?'(blocked ok)':'FAIL');
console.log('anonymous -> admin/blog:', r2.status, r2.status===401?'(blocked ok)':'FAIL');
